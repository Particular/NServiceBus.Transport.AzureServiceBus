namespace NServiceBus.Transport.AzureServiceBus.Configuration;

static class DiagnosticDescriptors
{
    // https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/choosing-diagnostic-ids
    public const string ExperimentalQueuesAttribute = "NSBASBEXP0001";
    public const string ExperimentalRulesAttribute = "NSBASBEXP0002";
    public const string ExperimentalSubscriptionsAttribute = "NSBASBEXP0003";
    public const string ExperimentalTopicsAttribute = "NSBASBEXP0004";
    public const string ExperimentalValidMigrationTopologyAttribute = "NSBASBEXP0005";
}