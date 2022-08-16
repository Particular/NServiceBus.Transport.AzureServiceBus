namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;

    class NamespacePermissions
    {
        readonly ServiceBusAdministrationClient administrativeClient;
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        Task manageTask;

        public NamespacePermissions(ServiceBusAdministrationClient administrativeClient)
        {
            this.administrativeClient = administrativeClient;
        }

        public async Task CanManage(CancellationToken cancellationToken = default)
        {
            if (manageTask == null)
            {
                await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    manageTask ??= CheckPermission(cancellationToken);
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
            try
            {
                await administrativeClient.QueueExistsAsync("$nservicebus-verification-queue", cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new Exception("Management rights are required to run this endpoint. Verify that the SAS policy has the Manage claim.", e);
            }
        }
    }
}