namespace NServiceBus.Transport.AzureServiceBus.Diagnostics;

using System.Diagnostics;
using System.Reflection;

static class ActivitySources
{
    public const string Name = "NServiceBus.Transport.AzureServiceBus";

    // Activity names
    public const string Receive = "NServiceBus.Transport.AzureServiceBus.Receive";
    public const string Dispatch = "NServiceBus.Transport.AzureServiceBus.Dispatch";
    public const string SubscriptionForwarder = "NServiceBus.Transport.AzureServiceBus.SessionEnabled.ForwardFromSubscriptionToInputQueue";

    // OTel messaging semantic convention tags
    public const string TagMessagingSystem = "messaging.system";
    public const string TagMessagingSystemValue = "AzureServiceBus";
    public const string TagDestinationName = "messaging.destination.name";
    public const string TagOperationType = "messaging.operation.type";
    public const string TagMessageId = "messaging.message.id";
    public const string TagBatchMessageCount = "messaging.batch.message_count";
    public const string TagSessionId = "messaging.session.id";

    public const string SessionEnabled = "NServiceBus.Transport.AzureServiceBus.SessionEnabled";

    // OTel messaging operation types
    public const string OperationSend = "send";
    public const string OperationPublish = "publish";
    public const string OperationReceive = "receive";

    // Vendor-specific tags
    public const string TagTopicString = "nservicebus.transport.AzureServiceBus.topic_string";
    public const string TagFailureCount = "nservicebus.transport.AzureServiceBus.failure_count";

    // Activity event names
    public const string CommitEvent = "AzureServiceBus.commit";


    public static bool HasListeners() => activitySource.HasListeners();

    static readonly string Version = GetVersion();

    static string GetVersion()
    {
        var informationalVersion = typeof(ActivitySources).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var plusIndex = informationalVersion.IndexOf('+');
        return plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion;
    }

    public static readonly ActivitySource activitySource = new(Name, Version);
}