namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;
    using Microsoft.Azure.ServiceBus.Primitives;

    class NamespacePermissions
    {
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly ITokenProvider tokenProvider;

        public NamespacePermissions(ServiceBusConnectionStringBuilder connectionStringBuilder, ITokenProvider tokenProvider)
        {
            this.connectionStringBuilder = connectionStringBuilder;
            this.tokenProvider = tokenProvider;
        }

        public async Task<StartupCheckResult> CanManage()
        {
            var client = new ManagementClient(connectionStringBuilder, tokenProvider);

            try
            {
                await client.QueueExistsAsync("$nservicebus-verification-queue").ConfigureAwait(false);
            }
            catch (UnauthorizedException)
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