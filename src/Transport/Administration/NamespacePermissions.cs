namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Messaging.ServiceBus.Administration;

    class NamespacePermissions
    {
        readonly string connectionString;
        readonly TokenCredential tokenCredential;
        readonly SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        Task manageTask;

        public NamespacePermissions(string connectionString, TokenCredential tokenCredential)
        {
            this.connectionString = connectionString;
            this.tokenCredential = tokenCredential;
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
            var client = new ServiceBusAdministrationClient(connectionString, tokenCredential);

            try
            {
                await client.QueueExistsAsync("$nservicebus-verification-queue", cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new Exception("Management rights are required to run this endpoint. Verify that the SAS policy has the Manage claim.", e);
            }
        }
    }
}