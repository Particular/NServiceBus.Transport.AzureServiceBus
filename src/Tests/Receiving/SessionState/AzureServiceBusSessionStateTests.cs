#nullable enable
namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving.SessionState;

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public class AzureServiceBusSessionStateTests
{
    const string SessionId = "session-1";

    [Test]
    public async Task Get_returns_default_when_no_state_stored()
    {
        var store = new FakeSessionStateStore();
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        var result = await sessionState.Get<CustomerState>();

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Set_Then_Get_round_trips()
    {
        var store = new FakeSessionStateStore();

        await Seed(store, new CustomerState { ProcessedMessages = 3, LastProduct = "Dates" });

        var reread = await new AzureServiceBusSessionState(store, SessionId).Get<CustomerState>();

        Assert.That(reread, Is.Not.Null);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reread!.ProcessedMessages, Is.EqualTo(3));
            Assert.That(reread.LastProduct, Is.EqualTo("Dates"));
        }
    }

    [Test]
    public async Task Flush_writes_a_versioned_envelope_with_a_user_section()
    {
        var store = new FakeSessionStateStore();
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Set(new CustomerState { ProcessedMessages = 1 });
        await sessionState.Flush();

        using var doc = JsonDocument.Parse(store.Current!.ToMemory());
        var root = doc.RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.GetProperty("version").GetInt32(), Is.EqualTo(SessionStateEnvelope.CurrentVersion));
            Assert.That(root.GetProperty("user").GetProperty("contentType").GetString(), Is.EqualTo("application/json"));
            Assert.That(root.GetProperty("user").GetProperty("type").GetString(), Does.StartWith(typeof(CustomerState).FullName!));
            Assert.That(root.GetProperty("user").GetProperty("data").GetProperty("processedMessages").GetInt32(), Is.EqualTo(1));
            Assert.That(root.TryGetProperty("transport", out _), Is.False, "transport section should not be emitted when empty");
        }
    }

    [Test]
    public async Task Set_does_not_write_to_the_broker_until_flushed()
    {
        var store = new FakeSessionStateStore();
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Set(new CustomerState { ProcessedMessages = 1 });

        Assert.That(store.Writes, Is.Zero, "Set must only mutate in memory; nothing is persisted until Flush.");

        await sessionState.Flush();

        Assert.That(store.Writes, Is.EqualTo(1));
    }

    [Test]
    public async Task Clear_does_not_write_to_the_broker_until_flushed()
    {
        var store = new FakeSessionStateStore();
        await Seed(store, new CustomerState { ProcessedMessages = 1 });

        var sessionState = new AzureServiceBusSessionState(store, SessionId);
        await sessionState.Clear();

        Assert.That(store.Writes, Is.EqualTo(1), "still just the seed write; Clear has not been flushed yet.");

        await sessionState.Flush();

        using var doc = JsonDocument.Parse(store.Current!.ToMemory());
        Assert.That(doc.RootElement.TryGetProperty("user", out _), Is.False);
    }

    [Test]
    public async Task An_unflushed_Set_leaves_the_previously_stored_state_intact()
    {
        var store = new FakeSessionStateStore();
        await Seed(store, new CustomerState { ProcessedMessages = 1, LastProduct = "Dates" });

        var failedAttempt = new AzureServiceBusSessionState(store, SessionId);
        _ = await failedAttempt.Get<CustomerState>();
        await failedAttempt.Set(new CustomerState { ProcessedMessages = 2, LastProduct = "Apples" });

        var afterRetryLoad = await new AzureServiceBusSessionState(store, SessionId).Get<CustomerState>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.Writes, Is.EqualTo(1), "only the seed write happened");
            Assert.That(afterRetryLoad!.ProcessedMessages, Is.EqualTo(1));
            Assert.That(afterRetryLoad.LastProduct, Is.EqualTo("Dates"));
        }
    }

    [Test]
    public async Task Flush_with_unchanged_state_has_no_side_effects()
    {
        var store = new FakeSessionStateStore();
        await Seed(store, new CustomerState { ProcessedMessages = 1 });

        var sessionState = new AzureServiceBusSessionState(store, SessionId);
        _ = await sessionState.Get<CustomerState>();
        await sessionState.Flush();

        Assert.That(store.Writes, Is.EqualTo(1), "a read-only pass must not write session state back.");
    }

    [Test]
    public async Task Flush_never_touches_an_existing_transport_section()
    {
        var store = new FakeSessionStateStore
        {
            Current = SessionStateEnvelopeSerializer.Write(new SessionStateEnvelope { TransportState = new TransportSessionState() })
        };
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Set(new CustomerState { ProcessedMessages = 7 });
        await sessionState.Flush();

        using var doc = JsonDocument.Parse(store.Current!.ToMemory());
        Assert.That(doc.RootElement.TryGetProperty("transport", out _), Is.True,
            "Flush only ever writes the user section; an already-present transport section must survive it untouched.");
    }

    [Test]
    public async Task Clearing_and_flushing_never_touches_an_existing_transport_section()
    {
        var store = new FakeSessionStateStore
        {
            Current = SessionStateEnvelopeSerializer.Write(new SessionStateEnvelope
            {
                TransportState = new TransportSessionState(),
                UserState = new UserSessionState
                {
                    ContentType = "application/json",
                    Type = typeof(CustomerState).FullName,
                    Data = JsonSerializer.SerializeToElement(new CustomerState { ProcessedMessages = 5 })
                }
            })
        };
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Clear();
        await sessionState.Flush();

        using var doc = JsonDocument.Parse(store.Current!.ToMemory());
        using (Assert.EnterMultipleScope())
        {
            Assert.That(doc.RootElement.TryGetProperty("transport", out _), Is.True);
            Assert.That(doc.RootElement.TryGetProperty("user", out _), Is.False);
        }
    }

    [Test]
    public async Task Get_after_Set_on_the_same_instance_does_not_re_fetch_from_the_broker()
    {
        var store = new FakeSessionStateStore();
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Set(new CustomerState { ProcessedMessages = 4 });
        store.ThrowOnGet = true; // prove no additional GetSessionStateAsync round-trip happens

        var result = await sessionState.Get<CustomerState>();

        Assert.That(result!.ProcessedMessages, Is.EqualTo(4));
    }

    [Test]
    public async Task Repeated_Get_on_the_same_instance_only_fetches_from_the_broker_once()
    {
        var store = new FakeSessionStateStore();
        await Seed(store, new CustomerState { ProcessedMessages = 6 });
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        var first = await sessionState.Get<CustomerState>();
        store.ThrowOnGet = true; // prove the second call reuses the envelope loaded by the first
        var second = await sessionState.Get<CustomerState>();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(first!.ProcessedMessages, Is.EqualTo(6));
            Assert.That(second!.ProcessedMessages, Is.EqualTo(6));
            Assert.That(second, Is.Not.SameAs(first), "each Get deserializes its own instance from the cached envelope");
        }
    }

    [Test]
    public void Get_throws_when_existing_state_was_not_written_by_this_api()
    {
        var store = new FakeSessionStateStore { Current = BinaryData.FromString("not json at all") };
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        Assert.That(async () => await sessionState.Get<CustomerState>(), Throws.Exception);
    }

    [Test]
    public void Set_throws_instead_of_silently_overwriting_state_not_written_by_this_api()
    {
        var store = new FakeSessionStateStore { Current = BinaryData.FromString("not json at all") };
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        Assert.That(async () => await sessionState.Set(new CustomerState { ProcessedMessages = 1 }), Throws.Exception);
        Assert.That(store.Writes, Is.Zero, "the unparseable blob must not be overwritten");
    }

    [Test]
    public void Get_throws_when_existing_state_is_the_JSON_literal_null()
    {
        // Valid JSON, but not a value this transport could have written.
        var store = new FakeSessionStateStore { Current = BinaryData.FromString("null") };
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        Assert.That(async () => await sessionState.Get<CustomerState>(), Throws.Exception);
    }

    [Test]
    public void Set_null_throws()
    {
        var sessionState = new AzureServiceBusSessionState(new FakeSessionStateStore(), SessionId);

        Assert.That(async () => await sessionState.Set<CustomerState>(null!), Throws.ArgumentNullException);
    }

    static async Task Seed(FakeSessionStateStore store, CustomerState state)
    {
        var seed = new AzureServiceBusSessionState(store, SessionId);
        await seed.Set(state);
        await seed.Flush();
    }

    sealed class FakeSessionStateStore : ISessionStateStore
    {
        public BinaryData? Current { get; set; }
        public bool ThrowOnGet { get; set; }
        public int Writes { get; private set; }

        public Task<BinaryData?> GetSessionStateAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnGet)
            {
                throw new InvalidOperationException("GetSessionStateAsync should not have been called.");
            }

            return Task.FromResult(Current);
        }

        public Task SetSessionStateAsync(BinaryData sessionState, CancellationToken cancellationToken = default)
        {
            Current = sessionState;
            Writes++;
            return Task.CompletedTask;
        }
    }

    public class CustomerState
    {
        public int ProcessedMessages { get; set; }
        public string? LastProduct { get; set; }
    }
}
