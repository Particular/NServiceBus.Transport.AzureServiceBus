namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;

// The session processor events can only be wired up when the base processor is created from a client with a connection string.
// Nothing connects to the namespace because processing is never started on the base processor.
public class FakeSessionProcessorClient() : ServiceBusClient("Endpoint=sb://fake.servicebus.windows.net/;SharedAccessKeyName=fake;SharedAccessKey=fake")
{
    public List<FakeSessionProcessor> SubscriptionProcessors { get; } = [];

    public Func<Task> StartProcessing { get; init; } = () => Task.CompletedTask;

    public Func<Task> StopProcessing { get; init; } = () => Task.CompletedTask;

    public Func<Task> CloseProcessor { get; init; } = () => Task.CompletedTask;

    public List<FakeSender> Senders { get; } = [];

    public int DisposeCount { get; private set; }

    public override ValueTask DisposeAsync()
    {
        DisposeCount++;
        return default;
    }

    public override ServiceBusSessionProcessor CreateSessionProcessor(string queueName, ServiceBusSessionProcessorOptions options = null) =>
        new FakeSessionProcessor(this, queueName, options);

    public override ServiceBusSessionProcessor CreateSessionProcessor(string topicName, string subscriptionName, ServiceBusSessionProcessorOptions options = null)
    {
        var processor = new FakeSessionProcessor(this, topicName, subscriptionName, options);
        SubscriptionProcessors.Add(processor);
        return processor;
    }

    public override ServiceBusSender CreateSender(string queueOrTopicName, ServiceBusSenderOptions options)
    {
        var sender = new FakeSender();
        Senders.Add(sender);
        return sender;
    }

    public class FakeSessionProcessor : ServiceBusSessionProcessor
    {
        readonly FakeSessionProcessorClient client;

        public FakeSessionProcessor(FakeSessionProcessorClient client, string queueName, ServiceBusSessionProcessorOptions options)
            : base(client, queueName, options)
        {
            this.client = client;
            EntityPath = queueName;
        }

        public FakeSessionProcessor(FakeSessionProcessorClient client, string topicName, string subscriptionName, ServiceBusSessionProcessorOptions options)
            : base(client, topicName, subscriptionName, options)
        {
            this.client = client;
            EntityPath = $"{topicName}/{subscriptionName}";
        }

        public override string EntityPath { get; }
        public bool WasStarted { get; private set; }
        public bool WasStopped { get; private set; }
        public bool WasClosed { get; private set; }

        // Like the SDK, adding or removing handlers throws while the inner processor has an active receive task
        public bool IsActive
        {
            get => ActiveReceiveTask.GetValue(InnerProcessor) != null;
            private set => ActiveReceiveTask.SetValue(InnerProcessor, value ? new TaskCompletionSource().Task : null);
        }

        public override async Task StartProcessingAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasStarted = true;
            await client.StartProcessing();
            IsActive = true;
        }

        // Like the SDK, a canceled stop throws and leaves the processor running
        public override async Task StopProcessingAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WasStopped = true;
            await client.StopProcessing();
            IsActive = false;
        }

        public override async Task CloseAsync(CancellationToken cancellationToken = default)
        {
            WasClosed = true;
            await client.CloseProcessor();

            if (IsActive)
            {
                await StopProcessingAsync(cancellationToken);
            }
        }

        static readonly PropertyInfo ActiveReceiveTask = typeof(ServiceBusProcessor).GetProperty("ActiveReceiveTask", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("ServiceBusProcessor.ActiveReceiveTask not found");

        public Task ProcessMessage(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default) =>
            OnProcessSessionMessageAsync(new ProcessSessionMessageEventArgs(message, new FakeSessionReceiver(), cancellationToken));

        public Task RaiseError(Exception exception, ServiceBusErrorSource errorSource, CancellationToken cancellationToken = default) =>
            OnProcessErrorAsync(new ProcessErrorEventArgs(exception, errorSource, "fake.servicebus.windows.net", EntityPath, Identifier, cancellationToken));
    }

    class FakeSessionReceiver : ServiceBusSessionReceiver
    {
        public override Task CompleteMessageAsync(ServiceBusReceivedMessage message, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}