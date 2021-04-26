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
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        Task manageTask;

        public NamespacePermissions(ServiceBusConnectionStringBuilder connectionStringBuilder, ITokenProvider tokenProvider)
        {
            this.connectionStringBuilder = connectionStringBuilder;
            this.tokenProvider = tokenProvider;
        }

        public async Task CanManage(CancellationToken cancellationToken = default)
        {
            if (manageTask == null)
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (manageTask == null)
                    {
                        manageTask = CheckPermission(cancellationToken);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
            await manageTask.ConfigureAwait(false);
        }

        async Task CheckPermission(CancellationToken cancellationToken)
        {
            var client = new ManagementClient(connectionStringBuilder, tokenProvider);

            try
            {
                await client.QueueExistsAsync("$nservicebus-verification-queue", cancellationToken).ConfigureAwait(false);
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