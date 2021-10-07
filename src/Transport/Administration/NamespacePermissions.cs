namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;

    class NamespacePermissions
    {
        readonly ServiceBusAdministrationClient administrativeClient;

        public NamespacePermissions(ServiceBusAdministrationClient administrativeClient)
        {
            this.administrativeClient = administrativeClient;
        }

        public async Task<StartupCheckResult> CanManage()
        {
            try
            {
                await administrativeClient.QueueExistsAsync("$nservicebus-verification-queue").ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException)
            {
                return StartupCheckResult.Failed("Management rights are required to run this endpoint. Verify that the SAS policy has the Manage claim.");
            }
            catch (Exception exception)
            {
                return StartupCheckResult.Failed(exception.Message);
            }

            return StartupCheckResult.Success;
        }
        public Task<StartupCheckResult> CanSend()
        {
            // TODO: blocked by https://github.com/Azure/azure-service-bus-dotnet/issues/520
            return Task.FromResult(StartupCheckResult.Success);
        }

        public Task<StartupCheckResult> CanReceive()
        {
            // TODO: blocked by https://github.com/Azure/azure-service-bus-dotnet/issues/520
            return Task.FromResult(StartupCheckResult.Success);
        }
    }
}