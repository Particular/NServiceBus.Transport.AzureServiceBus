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
#pragma warning disable CS0618 // Type or member is obsolete
        var topology = TopicTopology.MigrateFromNamedSingleTopic("bundle-1");
#pragma warning restore CS0618 // Type or member is obsolete
        var transportSettings = new AzureServiceBusTransport("connection-string", topology);

        var recordingAdministrationClient = new RecordingServiceBusAdministrationClient();
        var creator = new MigrationTopologyCreator(transportSettings);

        await creator.Create(recordingAdministrationClient);

        Approver.Verify(recordingAdministrationClient.ToString());
    }

    [Test]
    public async Task Should_create_default_single_topic_topology()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var topology = TopicTopology.MigrateFromSingleDefaultTopic();
#pragma warning restore CS0618 // Type or member is obsolete
        var transportSettings = new AzureServiceBusTransport("connection-string", topology);

        var recordingAdministrationClient = new RecordingServiceBusAdministrationClient();
        var creator = new MigrationTopologyCreator(transportSettings);

        await creator.Create(recordingAdministrationClient);

        Approver.Verify(recordingAdministrationClient.ToString());
    }

    [Test]
    public async Task Should_hierarchy()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        var topology = TopicTopology.MigrateFromTopicHierarchy("bundle-1", "bundle-2");
#pragma warning restore CS0618 // Type or member is obsolete
        var transportSettings = new AzureServiceBusTransport("connection-string", topology);

        var recordingAdministrationClient = new RecordingServiceBusAdministrationClient();
        var creator = new MigrationTopologyCreator(transportSettings);

        await creator.Create(recordingAdministrationClient);

        Approver.Verify(recordingAdministrationClient.ToString());
    }
}