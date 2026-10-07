namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

interface ISessionStateStore
{
    Task<BinaryData?> GetSessionStateAsync(CancellationToken cancellationToken = default);

    Task SetSessionStateAsync(BinaryData sessionState, CancellationToken cancellationToken = default);
}

sealed class SessionStateStoreThroughProcessingArgs(ProcessSessionMessageEventArgs args) : ISessionStateStore
{
    public async Task<BinaryData?> GetSessionStateAsync(CancellationToken cancellationToken = default) =>
        await args.GetSessionStateAsync(cancellationToken).ConfigureAwait(false);

    public Task SetSessionStateAsync(BinaryData sessionState, CancellationToken cancellationToken = default) =>
        args.SetSessionStateAsync(sessionState, cancellationToken);
}
