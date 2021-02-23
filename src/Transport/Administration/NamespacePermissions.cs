namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;
    using Microsoft.Azure.ServiceBus.Primitives;

    class NamespacePermissions
    {
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly ITokenProvider tokenProvider;

        readonly Lazy<Task> manageCheck;

        public NamespacePermissions(ServiceBusConnectionStringBuilder connectionStringBuilder, ITokenProvider tokenProvider)
        {
            this.connectionStringBuilder = connectionStringBuilder;
            this.tokenProvider = tokenProvider;

            manageCheck = new Lazy<Task>(() => CheckPermission(), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Task CanManage() => manageCheck.Value;

        async Task CheckPermission()
        {
            var client = new ManagementClient(connectionStringBuilder, tokenProvider);

            try
            {
                await client.QueueExistsAsync("$nservicebus-verification-queue").ConfigureAwait(false);
            }
            catch (UnauthorizedException e)
            {
                throw new Exception("Management rights are required to run this endpoint. Verify that the SAS policy has the Manage claim.", e);
            }
            finally
            {
                await client.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}