namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Threading.Tasks;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
public class MigrationTopologyCreatorTests
{
    [Test]
    public async Task Should_create_single_topic_topology()
    {
        var topology = TopicTopology.MigrateFromNamedSingleTopic("bundle-1");
        var transportSettings = new AzureServiceBusTransport("connection-string", topology);

        var recordingAdministrationClient = new RecordingServiceBusAdministrationClient();
        var creator = new MigrationTopologyCreator(transportSettings);

        await creator.Create(recordingAdministrationClient);

        Approver.Verify(recordingAdministrationClient.ToString());
    }

    [Test]
    public async Task Should_create_default_single_topic_topology()
    {
        var topology = TopicTopology.MigrateFromSingleDefaultTopic();
        var transportSettings = new AzureServiceBusTransport("connection-string", topology);

        var recordingAdministrationClient = new RecordingServiceBusAdministrationClient();
        var creator = new MigrationTopologyCreator(transportSettings);

        await creator.Create(recordingAdministrationClient);

        Approver.Verify(recordingAdministrationClient.ToString());
    }

    [Test]
    public async Task Should_hierarchy()
    {
        var topology = TopicTopology.MigrateFromTopicHierarchy("bundle-1", "bundle-2");
        var transportSettings = new AzureServiceBusTransport("connection-string", topology);

        var recordingAdministrationClient = new RecordingServiceBusAdministrationClient();
        var creator = new MigrationTopologyCreator(transportSettings);

        await creator.Create(recordingAdministrationClient);

        Approver.Verify(recordingAdministrationClient.ToString());
    }
}