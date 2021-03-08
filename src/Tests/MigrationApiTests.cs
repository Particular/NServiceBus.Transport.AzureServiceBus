#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CS0618
namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using System;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Primitives;
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
            endpointConfiguration.UseTransport<AzureServiceBusTransport>("ConnectionString");

            var configuredTransport = (AzureServiceBusTransport)endpointConfiguration.GetSettings().Get<TransportDefinition>();
            Assert.AreEqual(defaultSettings.EnablePartitioning, configuredTransport.EnablePartitioning);
            Assert.AreEqual(defaultSettings.PrefetchCount, configuredTransport.PrefetchCount);
            Assert.AreEqual(defaultSettings.PrefetchMultiplier, configuredTransport.PrefetchMultiplier);
            Assert.AreEqual(defaultSettings.RetryPolicy, configuredTransport.RetryPolicy);
            Assert.AreEqual(defaultSettings.TimeToWaitBeforeTriggeringCircuitBreaker, configuredTransport.TimeToWaitBeforeTriggeringCircuitBreaker);
            Assert.AreEqual(defaultSettings.TokenProvider, configuredTransport.TokenProvider);
            Assert.AreEqual(defaultSettings.UseWebSockets, configuredTransport.UseWebSockets);
            Assert.AreEqual(defaultSettings.ConnectionString, configuredTransport.ConnectionString);
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

            var settings = endpointConfiguration.UseTransport<AzureServiceBusTransport>("ConnectionString");

            settings.EnablePartitioning();
            RetryPolicy customRetryPolicy = RetryPolicy.NoRetry;
            settings.CustomRetryPolicy(customRetryPolicy);
            var customTokenProvider = new ManagedIdentityTokenProvider(null);
            settings.CustomTokenProvider(customTokenProvider);
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
            Assert.AreEqual(customRetryPolicy, configuredTransport.RetryPolicy);
            Assert.AreEqual(customTokenProvider, configuredTransport.TokenProvider);
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
    }
}
#pragma warning restore CS0618
#pragma warning restore IDE0079 // Remove unnecessary suppression