namespace NServiceBus.Transport.AzureServiceBus.Tests.Administration;

using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
public class QueueCreatorTests
{
    [Test]
    public async Task Should_set_AutoDeleteOnIdle_when_configured()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default)
        {
            AutoDeleteOnIdle = TimeSpan.FromMinutes(10)
        };

        var recordingClient = new RecordingServiceBusAdministrationClient();
        var creator = new QueueCreator(transport);

        await creator.Create(recordingClient, ["test-queue"], "test-queue");

        var output = recordingClient.ToString();

        Approver.Verify(output);
    }

    [Test]
    public async Task Should_not_set_AutoDeleteOnIdle_when_null()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default)
        {
            AutoDeleteOnIdle = null
        };

        var recordingClient = new RecordingServiceBusAdministrationClient();
        var creator = new QueueCreator(transport);

        await creator.Create(recordingClient, ["test-queue"], "test-queue");

        var output = recordingClient.ToString(); // AutoDeleteOnIdle should be default - TimeSpan.MaxValue

        Approver.Verify(output);
    }

    [Test]
    public async Task Should_only_set_AutoDeleteOnIdle_on_instance_specific_queue()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default)
        {
            AutoDeleteOnIdle = TimeSpan.FromMinutes(10)
        };

        var recordingClient = new RecordingServiceBusAdministrationClient();
        var creator = new QueueCreator(transport);

        await creator.Create(recordingClient, ["instance-queue", "error"], "instance-queue");

        // AutoDeleteOnIdle should be default for shared queues - TimeSpan.MaxValue
        var output = recordingClient.ToString();

        Approver.Verify(output);
    }

    [Test]
    public async Task Should_not_set_AutoDeleteOnIdle_when_instance_name_is_null()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default)
        {
            AutoDeleteOnIdle = TimeSpan.FromMinutes(10)
        };

        var recordingClient = new RecordingServiceBusAdministrationClient();
        var creator = new QueueCreator(transport);

        // Create queue without specifying an instance name
        await creator.Create(recordingClient, ["some-queue"], null);

        var output = recordingClient.ToString();

        Approver.Verify(output);
    }

    [Test]
    public async Task Should_not_set_AutoDeleteOnIdle_when_queue_name_does_not_match_instance_name()
    {
        var transport = new AzureServiceBusTransport("connectionString", TopicTopology.Default)
        {
            AutoDeleteOnIdle = TimeSpan.FromMinutes(10)
        };

        var recordingClient = new RecordingServiceBusAdministrationClient();
        var creator = new QueueCreator(transport);

        // Create queue with different instance name
        await creator.Create(recordingClient, ["some-queue"], "different-instance");

        var output = recordingClient.ToString();

        Approver.Verify(output);
    }
}