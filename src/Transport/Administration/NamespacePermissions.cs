namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading.Tasks;

    class NamespacePermissions
    {
        public static Task<StartupCheckResult> CanManage()
        {
            // TODO: blocked by https://github.com/Azure/azure-service-bus-dotnet/issues/519
            return Task.FromResult(StartupCheckResult.Success);
        }
        public static Task<StartupCheckResult> CanSend()
        {
            // TODO: blocked by https://github.com/Azure/azure-service-bus-dotnet/issues/520
            return Task.FromResult(StartupCheckResult.Success);
        }
    }
}