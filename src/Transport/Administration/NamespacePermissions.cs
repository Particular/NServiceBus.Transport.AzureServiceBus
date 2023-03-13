#nullable enable

namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.Core;
    using Azure.Messaging.ServiceBus.Administration;

    sealed class NamespacePermissions
    {
        readonly ServiceBusAdministrationClient adminClient;

        public NamespacePermissions(TokenCredential? tokenCredential, string fullyQualifiedNamespace, string connectionString)
            => adminClient = tokenCredential != null ? new ServiceBusAdministrationClient(fullyQualifiedNamespace, tokenCredential) : new ServiceBusAdministrationClient(connectionString);

        public async ValueTask<ServiceBusAdministrationClient> CanManage(CancellationToken cancellationToken = default)
        {
            try
            {
                await adminClient.QueueExistsAsync("$nservicebus-verification-queue", cancellationToken)
                    .ConfigureAwait(false);
                return adminClient;
            }
            catch (UnauthorizedAccessException e)
            {
                throw new Exception("Management rights are required to run this endpoint. Verify that the SAS policy has the Manage claim.", e);
            }
        }
    }
}