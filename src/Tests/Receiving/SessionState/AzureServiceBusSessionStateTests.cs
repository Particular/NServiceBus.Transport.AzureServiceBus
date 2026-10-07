#nullable enable
namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving.SessionState;

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

[TestFixture]
public partial class AzureServiceBusSessionStateTests
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
    public async Task Set_writes_a_versioned_envelope_with_a_user_section()
    {
        var store = new FakeSessionStateStore();
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Set(new CustomerState { ProcessedMessages = 1 });

        using var doc = JsonDocument.Parse(store.Current!.ToMemory());
        var root = doc.RootElement;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(root.GetProperty("version").GetInt32(), Is.EqualTo(SessionStateEnvelope.CurrentVersion));
            Assert.That(root.GetProperty("user").GetProperty("type").GetString(), Does.StartWith(typeof(CustomerState).FullName!));
            Assert.That(root.GetProperty("user").GetProperty("data").GetProperty("processedMessages").GetInt32(), Is.EqualTo(1));
            Assert.That(root.TryGetProperty("transport", out _), Is.False, "transport section should not be emitted when empty");
        }
    }

    [Test]
    public async Task Set_writes_to_the_broker_immediately()
    {
        var store = new FakeSessionStateStore();
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Set(new CustomerState { ProcessedMessages = 1 });

        Assert.That(store.Writes, Is.EqualTo(1),
            "Set must write through immediately - a deferred write could never be flushed if the " +
            "message is later retried through a path that does not re-invoke the handler.");
    }

    [Test]
    public async Task Clear_writes_to_the_broker_immediately()
    {
        var store = new FakeSessionStateStore();
        await Seed(store, new CustomerState { ProcessedMessages = 1 });

        var sessionState = new AzureServiceBusSessionState(store, SessionId);
        await sessionState.Clear();

        Assert.That(store.Writes, Is.EqualTo(2), "the seed write, plus the Clear's own immediate write.");

        using var doc = JsonDocument.Parse(store.Current!.ToMemory());
        Assert.That(doc.RootElement.TryGetProperty("user", out _), Is.False);
    }

    [Test]
    public async Task A_subsequent_Set_on_a_fresh_instance_overwrites_the_previous_value()
    {
        var store = new FakeSessionStateStore();
        await Seed(store, new CustomerState { ProcessedMessages = 1, LastProduct = "Dates" });

        var secondAttempt = new AzureServiceBusSessionState(store, SessionId);
        _ = await secondAttempt.Get<CustomerState>();
        await secondAttempt.Set(new CustomerState { ProcessedMessages = 2, LastProduct = "Apples" });

        var afterSecondAttempt = await new AzureServiceBusSessionState(store, SessionId).Get<CustomerState>();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(store.Writes, Is.EqualTo(2), "the seed write, plus the second attempt's own immediate write.");
            Assert.That(afterSecondAttempt!.ProcessedMessages, Is.EqualTo(2));
            Assert.That(afterSecondAttempt.LastProduct, Is.EqualTo("Apples"));
        }
    }

    [Test]
    public async Task Get_only_never_writes_to_the_broker()
    {
        var store = new FakeSessionStateStore();
        await Seed(store, new CustomerState { ProcessedMessages = 1 });

        var sessionState = new AzureServiceBusSessionState(store, SessionId);
        _ = await sessionState.Get<CustomerState>();

        Assert.That(store.Writes, Is.EqualTo(1), "a read-only pass must not write session state back.");
    }

    [Test]
    public async Task Set_never_touches_an_existing_transport_section()
    {
        var store = new FakeSessionStateStore
        {
            Current = SessionStateEnvelopeSerializer.Write(new SessionStateEnvelope { TransportState = new TransportSessionState() })
        };
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Set(new CustomerState { ProcessedMessages = 7 });

        using var doc = JsonDocument.Parse(store.Current!.ToMemory());
        Assert.That(doc.RootElement.TryGetProperty("transport", out _), Is.True,
            "Set only ever writes the user section; an already-present transport section must survive it untouched.");
    }

    [Test]
    public async Task Clearing_never_touches_an_existing_transport_section()
    {
        var store = new FakeSessionStateStore
        {
            Current = SessionStateEnvelopeSerializer.Write(new SessionStateEnvelope
            {
                TransportState = new TransportSessionState(),
                UserState = new UserSessionState
                {
                    Type = typeof(CustomerState).FullName,
                    Data = JsonSerializer.SerializeToElement(new CustomerState { ProcessedMessages = 5 })
                }
            })
        };
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Clear();

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

    [Test]
    public async Task Set_Then_Get_round_trips_with_custom_JsonSerializerOptions()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        var store = new FakeSessionStateStore();
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Set(new CustomerState { ProcessedMessages = 2, LastProduct = "Grapes" }, options);

        using var doc = JsonDocument.Parse(store.Current!.ToMemory());
        Assert.That(doc.RootElement.GetProperty("user").GetProperty("data").GetProperty("processed_messages").GetInt32(), Is.EqualTo(2),
            "the supplied JsonSerializerOptions must drive serialization, not the default options");

        var reread = await new AzureServiceBusSessionState(store, SessionId).Get<CustomerState>(options);
        Assert.That(reread!.ProcessedMessages, Is.EqualTo(2));
    }

    [Test]
    public async Task Set_Then_Get_round_trips_with_a_source_generated_JsonTypeInfo()
    {
        var store = new FakeSessionStateStore();
        var sessionState = new AzureServiceBusSessionState(store, SessionId);

        await sessionState.Set(new CustomerState { ProcessedMessages = 9, LastProduct = "Pears" }, CustomerStateJsonContext.Default.CustomerState);

        var reread = await new AzureServiceBusSessionState(store, SessionId).Get(CustomerStateJsonContext.Default.CustomerState);

        Assert.That(reread!.ProcessedMessages, Is.EqualTo(9));
        Assert.That(reread.LastProduct, Is.EqualTo("Pears"));
    }

    [Test]
    public void Set_with_null_JsonSerializerOptions_throws()
    {
        var sessionState = new AzureServiceBusSessionState(new FakeSessionStateStore(), SessionId);

        Assert.That(async () => await sessionState.Set(new CustomerState(), (JsonSerializerOptions)null!), Throws.ArgumentNullException);
    }

    [Test]
    public void Get_with_null_JsonSerializerOptions_throws()
    {
        var sessionState = new AzureServiceBusSessionState(new FakeSessionStateStore(), SessionId);

        Assert.That(async () => await sessionState.Get<CustomerState>((JsonSerializerOptions)null!), Throws.ArgumentNullException);
    }

    [Test]
    public void Set_with_null_JsonTypeInfo_throws()
    {
        var sessionState = new AzureServiceBusSessionState(new FakeSessionStateStore(), SessionId);

        Assert.That(async () => await sessionState.Set(new CustomerState(), (System.Text.Json.Serialization.Metadata.JsonTypeInfo<CustomerState>)null!),
            Throws.ArgumentNullException);
    }

    [Test]
    public void Get_with_null_JsonTypeInfo_throws()
    {
        var sessionState = new AzureServiceBusSessionState(new FakeSessionStateStore(), SessionId);

        Assert.That(async () => await sessionState.Get((System.Text.Json.Serialization.Metadata.JsonTypeInfo<CustomerState>)null!),
            Throws.ArgumentNullException);
    }

    static async Task Seed(FakeSessionStateStore store, CustomerState state)
    {
        var seed = new AzureServiceBusSessionState(store, SessionId);
        await seed.Set(state);
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

    [JsonSerializable(typeof(CustomerState))]
    partial class CustomerStateJsonContext : JsonSerializerContext;
}
