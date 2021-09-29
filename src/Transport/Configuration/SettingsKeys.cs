﻿namespace NServiceBus.Transport.AzureServiceBus
{
    static class SettingsKeys
    {
        const string Base = "AzureServiceBus.";
        public const string TopicName = Base + nameof(TopicName);
        public const string MaximumSizeInGB = Base + nameof(MaximumSizeInGB);
        public const string EnablePartitioning = Base + nameof(EnablePartitioning);
        public const string PrefetchMultiplier = Base + nameof(PrefetchMultiplier);
        public const string PrefetchCount = Base + nameof(PrefetchCount);
        public const string TimeToWaitBeforeTriggeringCircuitBreaker = Base + nameof(TimeToWaitBeforeTriggeringCircuitBreaker);
        public const string SubscriptionNameShortener = Base + nameof(SubscriptionNameShortener);
        public const string RuleNameShortener = Base + nameof(RuleNameShortener);
        public const string SubscriptionNamingConvention = Base + nameof(SubscriptionNamingConvention);
        public const string SubscriptionRuleNamingConvention = Base + nameof(SubscriptionRuleNamingConvention);
        public const string ServiceBusTransportType = Base + nameof(ServiceBusTransportType);
        public const string CustomTokenCredential = Base + nameof(CustomTokenCredential);
        public const string CustomRetryPolicy = Base + nameof(CustomRetryPolicy);
    }
}