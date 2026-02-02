namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public class EntityValidatorTests
{
    public static IEnumerable<TestCaseData> ValidTopicsData
    {
        get
        {
            yield return new TestCaseData([new[] { "A" }, null])
                .SetName("Topics_Valid_SingleCharacter");
            yield return new TestCaseData([new[] { "Topic123", "Topic.Name-123", "1Topic", "Topic/Name", "T_N" }, null])
                .SetName("Topics_Valid_MultipleNames");
            yield return new TestCaseData([new[] { new string('t', 260) }, null])
                .SetName("Topics_Max_Length");
            yield return new TestCaseData([new[] { "Topic123" }, new HierarchyNamespaceOptions { HierarchyNamespace = "SomeNamespace" }])
                .SetName("Topics_Valid_HierarchicalNamespace");
        }
    }

    public static IEnumerable<TestCaseData> InvalidTopicsData
    {
        get
        {
            yield return new TestCaseData([new[] { " topic" }, null])
                .SetName("Topics_Invalid_LeadingSpace");
            yield return new TestCaseData([new[] { "Topic?" }, null])
                .SetName("Topics_Invalid_ContainsQuestionMark");
            yield return new TestCaseData([new[] { "Topic#" }, null])
                .SetName("Topics_Invalid_ContainsHash");
            yield return new TestCaseData([new[] { "Topic\\" }, null])
                .SetName("Topics_Invalid_ContainsBackslash");
            yield return new TestCaseData([new[] { "Topic_" }, null])
                .SetName("Topics_Invalid_TrailingUnderscore");
            yield return new TestCaseData([new[] { "" }, null])
                .SetName("Topics_Invalid_EmptyString");
            yield return new TestCaseData([new[] { new string('t', 261) }, null])
                .SetName("Topics_Too_Long");
            yield return new TestCaseData([new[] { "Topic123" }, new HierarchyNamespaceOptions { HierarchyNamespace = "Some bad Namespace" }])
                .SetName("Topics_Invalid_HierarchicalNamespace");
        }
    }

    [Test, TestCaseSource(nameof(ValidTopicsData))]
    public void Should_accept_valid_topics(IEnumerable<string> topicNames, HierarchyNamespaceOptions hierarchyNamespaceOptions)
    {
        var result = EntityValidator.ValidateTopics(topicNames.Select((t, i) => (t, i)).ToDictionary(item => $"MyEvent{item.i}", item => item.t), "Topics", hierarchyNamespaceOptions);

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test, TestCaseSource(nameof(InvalidTopicsData))]
    public void Should_reject_invalid_topics(IEnumerable<string> topicNames, HierarchyNamespaceOptions hierarchyNamespaceOptions)
    {
        var result = EntityValidator.ValidateTopics(topicNames.Select((t, i) => (t, i)).ToDictionary(item => $"MyEvent{item.i}", item => item.t), "Topics", hierarchyNamespaceOptions);

        Assert.That(result, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(result?.ErrorMessage, Does.Contain("do not comply with the Azure Service Bus topic limits"));
    }

    public static IEnumerable<TestCaseData> ValidQueuesData
    {
        get
        {
            yield return new TestCaseData([new[] { "A" }, null])
                .SetName("Queues_Valid_SingleCharacter");
            yield return new TestCaseData([new[] { "Queue123", "Queue.Name-123", "1Queue", "Queue/Name", "Q_N" }, null])
                .SetName("Queues_Valid_MultipleNames");
            yield return new TestCaseData([new[] { new string('q', 260) }, null])
                .SetName("Queues_Max_Length");
            yield return new TestCaseData([new[] { "Queue123" }, new HierarchyNamespaceOptions { HierarchyNamespace = "SomeNamespace" }])
                .SetName("Queues_Valid_HierarchicalNamespace");
        }
    }

    public static IEnumerable<TestCaseData> InvalidQueuesData
    {
        get
        {
            yield return new TestCaseData([new[] { " queue" }, null])
                .SetName("Queues_Invalid_LeadingSpace");
            yield return new TestCaseData([new[] { "Queue?" }, null])
                .SetName("Queues_Invalid_ContainsQuestionMark");
            yield return new TestCaseData([new[] { "Queue#" }, null])
                .SetName("Queues_Invalid_ContainsHash");
            yield return new TestCaseData([new[] { "Queue\\" }, null])
                .SetName("Queues_Invalid_ContainsBackslash");
            yield return new TestCaseData([new[] { "Queue_" }, null])
                .SetName("Queues_Invalid_TrailingUnderscore");
            yield return new TestCaseData([new[] { "" }, null])
                .SetName("Queues_Invalid_EmptyString");
            yield return new TestCaseData([new[] { new string('q', 261) }, null])
                .SetName("Queues_Too_Long");
            yield return new TestCaseData([new[] { "Queue123" }, new HierarchyNamespaceOptions { HierarchyNamespace = "Some bad Namespace" }])
                .SetName("Queues_Invalid_HierarchicalNamespace");
        }
    }

    [Test, TestCaseSource(nameof(ValidQueuesData))]
    public void Should_accept_valid_queues(IEnumerable<string> queueNames, HierarchyNamespaceOptions hierarchyNamespaceOptions)
    {
        var result = EntityValidator.ValidateQueues(queueNames, "Queues", hierarchyNamespaceOptions);

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test, TestCaseSource(nameof(InvalidQueuesData))]
    public void Should_reject_invalid_queues(IEnumerable<string> queueNames, HierarchyNamespaceOptions hierarchyNamespaceOptions)
    {
        var result = EntityValidator.ValidateQueues(queueNames, "Queues", hierarchyNamespaceOptions);

        Assert.That(result, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(result?.ErrorMessage, Does.Contain("do not comply with the Azure Service Bus queue limits"));
    }

    public static IEnumerable<TestCaseData> ValidRulesData
    {
        get
        {
            yield return new TestCaseData([new[] { "R" }])
                .SetName("Rules_Valid_SingleCharacter");
            yield return new TestCaseData([new[] { "Rule1", "Rule.Name-1", "R_1", "Rule_Name-1" }])
                .SetName("Rules_Valid_MultipleNames");
            yield return new TestCaseData([new[] { new string('r', 50) }])
                .SetName("Rules_Max_Length");
        }
    }

    public static IEnumerable<TestCaseData> InvalidRulesData
    {
        get
        {
            yield return new TestCaseData([new[] { "$Rule" }])
                .SetName("Rules_Invalid_StartsWithDollar");
            yield return new TestCaseData([new[] { "Rule?" }])
                .SetName("Rules_Invalid_ContainsQuestionMark");
            yield return new TestCaseData([new[] { "R_" }])
                .SetName("Rules_Invalid_TrailingUnderscore");
            yield return new TestCaseData([new[] { " Rule" }])
                .SetName("Rules_Invalid_LeadingSpace");
            yield return new TestCaseData([new[] { "Rule#" }])
                .SetName("Rules_Invalid_ContainsHash");
            yield return new TestCaseData([new[] { "" }])
                .SetName("Rules_Invalid_EmptyString");
            yield return new TestCaseData([new[] { new string('r', 51) }])
                .SetName("Rules_Too_Long");
        }
    }

    [Test, TestCaseSource(nameof(ValidRulesData))]
    public void Should_accept_valid_rules(IEnumerable<string> ruleNames)
    {
        var result = EntityValidator.ValidateRules(ruleNames, "Rules");

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test, TestCaseSource(nameof(InvalidRulesData))]
    public void Should_reject_invalid_rules(IEnumerable<string> ruleNames)
    {
        var result = EntityValidator.ValidateRules(ruleNames, "Rules");

        Assert.That(result, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(result?.ErrorMessage, Does.Contain("do not comply with the Azure Service Bus rule limits"));
    }

    public static IEnumerable<TestCaseData> ValidSubscriptionsData
    {
        get
        {
            yield return new TestCaseData([new[] { "S" }])
                .SetName("Subscriptions_Valid_SingleCharacter");
            yield return new TestCaseData([new[] { "Sub1", "Sub.Name-1", "S_1", "Sub_Name-1" }])
                .SetName("Subscriptions_Valid_MultipleNames");
            yield return new TestCaseData([new[] { new string('s', 50) }])
                .SetName("Subscriptions_Max_Length");
        }
    }

    public static IEnumerable<TestCaseData> InvalidSubscriptionsData
    {
        get
        {
            yield return new TestCaseData([new[] { "$Sub" }])
                .SetName("Subscriptions_Invalid_StartsWithDollar");
            yield return new TestCaseData([new[] { "Sub?" }])
                .SetName("Subscriptions_Invalid_ContainsQuestionMark");
            yield return new TestCaseData([new[] { "S_" }])
                .SetName("Subscriptions_Invalid_TrailingUnderscore");
            yield return new TestCaseData([new[] { " Sub" }])
                .SetName("Subscriptions_Invalid_LeadingSpace");
            yield return new TestCaseData([new[] { "Sub#" }])
                .SetName("Subscriptions_Invalid_ContainsHash");
            yield return new TestCaseData([new[] { "" }])
                .SetName("Subscriptions_Invalid_EmptyString");
            yield return new TestCaseData([new[] { new string('s', 51) }])
                .SetName("Subscriptions_Too_Long");
        }
    }

    [Test, TestCaseSource(nameof(ValidSubscriptionsData))]
    public void Should_accept_valid_subscriptions(IEnumerable<string> subscriptionNames)
    {
        var result = EntityValidator.ValidateSubscriptions(subscriptionNames, "Subscriptions");

        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test, TestCaseSource(nameof(InvalidSubscriptionsData))]
    public void Should_reject_invalid_subscriptions(IEnumerable<string> subscriptionNames)
    {
        var result = EntityValidator.ValidateSubscriptions(subscriptionNames, "Subscriptions");

        Assert.That(result, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(result?.ErrorMessage,
            Does.Contain("do not comply with the Azure Service Bus subscription limits"));
    }
}