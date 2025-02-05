// TODO re-enable some of these tests
// namespace NServiceBus.Transport.AzureServiceBus.Tests;
//
// using System.ComponentModel.DataAnnotations;
// using NUnit.Framework;
// using Particular.Approvals;
//
// [TestFixture]
// public class MigrationTopologyTests
// {
//     [Test]
//     public void SubscribeDestinations_Should_return_migration_topic_when_event_is_mapped_for_migration()
//     {
//         var topologyOptions = new MigrationTopologyOptions
//         {
//             TopicToPublishTo = "PublishTopic",
//             TopicToSubscribeOn = "SubscribeTopic",
//             EventsToMigrateMap = { typeof(MyEvent).FullName },
//             QueueNameToSubscriptionNameMap = { { "SubscribingQueue", "MySubscriptionName" } },
//             SubscribedEventToRuleNameMap = { { typeof(MyEvent).FullName, "MyRuleName" } }
//         };
//
//         var topology = TopicTopology.FromOptions(topologyOptions);
//
//         var result = topology.GetSubscribeDestinations(typeof(MyEvent), "SubscribingQueue");
//
//         Assert.That(result, Has.Length.EqualTo(1));
//         Assert.Multiple(() =>
//         {
//             Assert.That(result[0].Topic, Is.EqualTo("SubscribeTopic"));
//             Assert.That(result[0].SubscriptionName, Is.EqualTo("MySubscriptionName"));
//             Assert.That(result[0].Rule, Is.Not.Null);
//             Assert.That(result[0].Rule.Value.Name, Is.EqualTo("MyRuleName"));
//             Assert.That(result[0].Rule.Value.Filter, Is.EqualTo("[NServiceBus.EnclosedMessageTypes] LIKE '%NServiceBus.Transport.AzureServiceBus.Tests.MigrationTopologyTests+MyEvent%'"));
//         });
//     }
//
//     [Test]
//     public void SubscribeDestinations_with_migration_options_Should_throw_when_event_is_not_mapped()
//     {
//         var topologyOptions = new MigrationTopologyOptions
//         {
//             TopicToPublishTo = "TopicToPublishTo",
//             TopicToSubscribeOn = "TopicToSubscribeOn",
//         };
//
//         var topology = TopicTopology.FromOptions(topologyOptions);
//
//         Assert.That(() => topology.GetSubscribeDestinations(typeof(MyEvent), "SubscribingQueue"), Throws.Exception);
//     }
//
//     [Test]
//     public void Should_self_validate()
//     {
//         var topologyOptions = new MigrationTopologyOptions
//         {
//             TopicToPublishTo = new string('a', 261),
//             TopicToSubscribeOn = new string('a', 261),
//             PublishedEventToTopicsMap = { { typeof(MyEvent).FullName, new string('c', 261) } },
//             SubscribedEventToTopicsMap = { { typeof(MyEvent).FullName, [new string('d', 261), new string('e', 261)] } },
//             QueueNameToSubscriptionNameMap = { { "SubscribingQueue", new string('f', 51) } },
//             SubscribedEventToRuleNameMap = { { typeof(MyEvent).FullName, new string('g', 51) } }
//         };
//
//         var topology = TopicTopology.FromOptions(topologyOptions);
//
//         var validationException = Assert.Catch<ValidationException>(() => topology.Validate());
//
//         Approver.Verify(validationException.Message);
//     }
//
//     class MyEvent;
// }