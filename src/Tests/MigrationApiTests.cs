#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS0618
namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.Configuration.AdvancedExtensibility;
    using NUnit.Framework;

    [TestFixture]
    public class MigrationApiTests
    {
        [Test]
        public void UseTransport_should_configure_default_values()
        {
            var defaultSettings = new AzureServiceBusTransport("ConnectionString");
            var endpointConfiguration = new EndpointConfiguration(nameof(MigrationApiTests));
            endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            var configuredTransport = (AzureServiceBusTransport)endpointConfiguration.GetSettings().Get<TransportDefinition>();
            Assert.IsNull(configuredTransport.ConnectionString);
            Assert.AreEqual(defaultSettings.EnablePartitioning, configuredTransport.EnablePartitioning);
            Assert.AreEqual(defaultSettings.PrefetchCount, configuredTransport.PrefetchCount);
            Assert.AreEqual(defaultSettings.PrefetchMultiplier, configuredTransport.PrefetchMultiplier);
            Assert.AreEqual(defaultSettings.RetryPolicyOptions, configuredTransport.RetryPolicyOptions);
            Assert.AreEqual(defaultSettings.TimeToWaitBeforeTriggeringCircuitBreaker, configuredTransport.TimeToWaitBeforeTriggeringCircuitBreaker);
            Assert.AreEqual(defaultSettings.TokenCredential, configuredTransport.TokenCredential);
            Assert.AreEqual(defaultSettings.UseWebSockets, configuredTransport.UseWebSockets);
            Assert.AreEqual(defaultSettings.EntityMaximumSize, configuredTransport.EntityMaximumSize);
            Assert.AreEqual(defaultSettings.TopicName, configuredTransport.TopicName);
            Assert.AreEqual(defaultSettings.TransportTransactionMode, configuredTransport.TransportTransactionMode);
            Assert.AreEqual(
                defaultSettings.SubscriptionNamingConvention("subscriptionName"),
                configuredTransport.SubscriptionNamingConvention("subscriptionName"));
            Assert.AreEqual(
                defaultSettings.SubscriptionRuleNamingConvention(typeof(MigrationApiTests)),
                defaultSettings.SubscriptionRuleNamingConvention(typeof(MigrationApiTests)));
        }

        [Test]
        public void UseTransport_should_apply_settings()
        {
            var endpointConfiguration = new EndpointConfiguration(nameof(MigrationApiTests));

            var settings = endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            settings.ConnectionString("ConnectionString");
            settings.EnablePartitioning();
            var retryPolicy = new ServiceBusRetryOptions() { MaxRetries = 0 };
            settings.CustomRetryPolicy(retryPolicy);
            var tokenCredential = new TokenCredentialMock();
            settings.TokenCredential(tokenCredential);
            settings.EnablePartitioning();
            settings.EntityMaximumSize(42);
            settings.PrefetchCount(21);
            settings.PrefetchMultiplier(1337);
            settings.TimeToWaitBeforeTriggeringCircuitBreaker(TimeSpan.FromDays(365));
            settings.TopicName("CustomTopicName");
            settings.UseWebSockets();
            settings.SubscriptionNamingConvention(_ => nameof(settings.SubscriptionNamingConvention));
            settings.SubscriptionRuleNamingConvention(_ => nameof(settings.SubscriptionRuleNamingConvention));

            var configuredTransport = (AzureServiceBusTransport)endpointConfiguration.GetSettings().Get<TransportDefinition>();

            Assert.IsTrue(configuredTransport.EnablePartitioning);
            Assert.AreEqual(retryPolicy, configuredTransport.RetryPolicyOptions);
            Assert.AreEqual(tokenCredential, configuredTransport.TokenCredential);
            Assert.IsTrue(configuredTransport.EnablePartitioning);
            Assert.AreEqual(42, configuredTransport.EntityMaximumSize);
            Assert.AreEqual(21, configuredTransport.PrefetchCount);
            Assert.AreEqual(1337, configuredTransport.PrefetchMultiplier);
            Assert.AreEqual(TimeSpan.FromDays(365), configuredTransport.TimeToWaitBeforeTriggeringCircuitBreaker);
            Assert.AreEqual("CustomTopicName", configuredTransport.TopicName);
            Assert.IsTrue(configuredTransport.UseWebSockets);
            Assert.AreEqual(nameof(settings.SubscriptionNamingConvention), configuredTransport.SubscriptionNamingConvention(nameof(MigrationApiTests)));
            Assert.AreEqual(nameof(settings.SubscriptionRuleNamingConvention), configuredTransport.SubscriptionRuleNamingConvention(typeof(MigrationApiTests)));
        }

        [Test]
        public void Should_throw_when_no_connection_string_provided()
        {
            var endpointConfiguration = new EndpointConfiguration(nameof(MigrationApiTests));
            endpointConfiguration.UseTransport<AzureServiceBusTransport>();

            var configuredTransport = (AzureServiceBusTransport)endpointConfiguration.GetSettings().Get<TransportDefinition>();

            var ex = Assert.ThrowsAsync<Exception>(() => configuredTransport.Initialize(
                new HostSettings("test", "test", new StartupDiagnosticEntries(), (_, __, ___) => { }, false),
                new ReceiveSettings[0],
                new string[0]));
            StringAssert.Contains("No transport connection string has been configured via the 'ConnectionString' method. Provide a connection string using 'endpointConfig.UseTransport<AzureServiceBusTransport>().ConnectionString(connectionString)'.", ex.Message);
        }
    }

    class TokenCredentialMock : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
#pragma warning restore CS0618
#pragma warning restore IDE0079 // Remove unnecessary suppression