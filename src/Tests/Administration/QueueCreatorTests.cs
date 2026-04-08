namespace NServiceBus.Transport.AzureServiceBus.Tests.Administration;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
public class QueueCreatorTests
{
    [Test]
    public async Task Should_set_AutoDeleteOnIdle_on_instance_queue_when_configured()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default) { AutoDeleteOnIdle = TimeSpan.FromMinutes(10) };

        var output = await CreateQueues(transport,
            instanceSuffix: "instance-1");

        Approver.Verify(output);
    }

    [Test]
    public async Task Should_not_set_AutoDeleteOnIdle_on_instance_queue_when_null()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default) { AutoDeleteOnIdle = null };

        var output = await CreateQueues(transport,
            instanceSuffix: "instance-1");

        Approver.Verify(output);
    }

    [Test]
    public async Task Should_set_MaxDeliveryCount_to_10_when_using_emulator()
    {
        var transport = new AzureServiceBusTransport("UseDevelopmentEmulator=true", TopicTopology.Default);
        var output = await CreateQueues(transport);

        Approver.Verify(output);
    }

    [Test]
    public async Task Should_auto_forward_dlq_messages_for_receive_queues_to_error_queue_when_enabled()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default) { AutoForwardDeadLetteredMessagesToErrorQueue = true };
        var output = await CreateQueues(transport,
            instanceSuffix: "instance-1",
            errorQueue: "my-error-queue");

        Approver.Verify(output);
    }

    [Test]
    public async Task Should_create_all_sending_queues()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default);

        var output = await CreateQueues(transport,
            sendingAddresses: ["audit", "error", "some-destination-queue"]);

        Approver.Verify(output);
    }

    async Task<string> CreateQueues(AzureServiceBusTransport transport,
        string receiveAddress = "test-queue",
        string instanceSuffix = null,
        string errorQueue = "error",
        string[] sendingAddresses = null)
    {
        sendingAddresses ??= [errorQueue]; // core adds the error queue as a sending address automatically

        var recordingClient = new RecordingServiceBusAdministrationClient();
        var receiveSettings = new List<ReceiveSettings> { new("Main", new QueueAddress(receiveAddress), false, false, errorQueue) };

        if (instanceSuffix != null)
        {
            receiveSettings.Add(new ReceiveSettings("InstanceSpecific", new QueueAddress($"{receiveAddress}-{instanceSuffix}"), false, false, errorQueue));
        }

        var creator = new QueueCreator();

        await creator.Create(recordingClient, transport.BuildQueueCreationPlan(receiveSettings.ToArray(), sendingAddresses));

        return recordingClient.ToString();
    }
}