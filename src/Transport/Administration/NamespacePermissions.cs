namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;

    static class NamespacePermissions
    {
        public static async Task AssertNamespaceManageRightsAvailable(this ServiceBusAdministrationClient administrationClient, CancellationToken cancellationToken = default)
        {
            try
            {
                await administrationClient.QueueExistsAsync("$nservicebus-verification-queue", cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new Exception("Management rights are required to run this endpoint. Verify that the SAS policy has the Manage claim.", e);
            }
        }
    }
}