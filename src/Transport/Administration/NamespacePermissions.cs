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

        readonly Lazy<Task<ServiceBusAdministrationClient>> manageClient;

        public NamespacePermissions(TokenCredential? tokenCredential, string fullyQualifiedNamespace, string connectionString)
        {
            adminClient = tokenCredential != null
                ? new ServiceBusAdministrationClient(fullyQualifiedNamespace, tokenCredential)
                : new ServiceBusAdministrationClient(connectionString);

            manageClient = new Lazy<Task<ServiceBusAdministrationClient>>(async () =>
            {
                try
                {
                    await adminClient.QueueExistsAsync("$nservicebus-verification-queue")
                        .ConfigureAwait(false);
                    return adminClient;
                }
                catch (UnauthorizedAccessException e)
                {
                    throw new Exception("Management rights are required to run this endpoint. Verify that the SAS policy has the Manage claim.", e);
                }
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public Task<ServiceBusAdministrationClient> CanManage(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return manageClient.Value;
        }
    }
}