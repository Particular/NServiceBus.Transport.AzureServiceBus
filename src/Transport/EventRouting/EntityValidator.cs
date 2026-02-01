namespace NServiceBus.Transport.AzureServiceBus;

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using EventRouting;

static partial class EntityValidator
{
    public static ValidationResult? ValidateTopics(Dictionary<string, string> typeToTopicNameMap, string? memberName, HierarchyNamespaceOptions? hierarchyNamespaceOptions = null)
    {
        hierarchyNamespaceOptions ??= HierarchyNamespaceOptions.None;
        var destinationManager = new DestinationManager(hierarchyNamespaceOptions);

        var topicNames = typeToTopicNameMap.Select(m => destinationManager.GetDestination(m.Value, m.Key)).ToArray();
        return ValidateTopics(topicNames, memberName);
    }

    public static ValidationResult? ValidateTopics(Dictionary<string, HashSet<string>> typeToTopicNamesMap, string? memberName, HierarchyNamespaceOptions? hierarchyNamespaceOptions = null)
    {
        hierarchyNamespaceOptions ??= HierarchyNamespaceOptions.None;
        var destinationManager = new DestinationManager(hierarchyNamespaceOptions);

        var topicNames = typeToTopicNamesMap.SelectMany(set => set.Value.Select(m => destinationManager.GetDestination(m, set.Key)));
        return ValidateTopics(topicNames, memberName);
    }

    public static ValidationResult? ValidateTopics(IEnumerable<string> topicNames, string? memberName, HierarchyNamespaceOptions? hierarchyNamespaceOptions = null)
    {
        if (hierarchyNamespaceOptions is not null)
        {
            var destinationManager = new DestinationManager(hierarchyNamespaceOptions);
            topicNames = topicNames.Select(m => destinationManager.GetDestination(m));
        }
        var topicNameRegex = TopicNameRegex();
        var invalidTopics = topicNames.Where(t => !topicNameRegex.IsMatch(t)).ToArray();

        return invalidTopics.Any()
            ? new ValidationResult(
                $"The following topic name(s) do not comply with the Azure Service Bus topic limits: {string.Join(", ", invalidTopics)}",
                memberName is not null ? [memberName] : [])
            : ValidationResult.Success;
    }

    // Enforces naming according to the specification https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules#microsoftservicebus
    [GeneratedRegex(@"^(?=.{1,260}$)(?=^[A-Za-z0-9])(?!.*[\\?#])(?:[A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9./_-]*[A-Za-z0-9])$")]
    private static partial Regex TopicNameRegex();

    public static ValidationResult? ValidateQueues(IEnumerable<string> queueNames, string? memberName, HierarchyNamespaceOptions? hierarchyNamespaceOptions = null)
    {
        hierarchyNamespaceOptions ??= HierarchyNamespaceOptions.None;
        var destinationManager = new DestinationManager(hierarchyNamespaceOptions);

        queueNames = queueNames.Select(m => destinationManager.GetDestination(m));

        var queueNameRegex = QueueNameRegex();
        var invalidQueues = queueNames.Where(t => !queueNameRegex.IsMatch(t)).ToArray();

        return invalidQueues.Any()
            ? new ValidationResult(
                $"The following queue name(s) do not comply with the Azure Service Bus queue limits: {string.Join(", ", invalidQueues)}",
                memberName is not null ? [memberName] : [])
            : ValidationResult.Success;
    }

    // Enforces naming according to the specification https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules#microsoftservicebus
    // Note the queue pattern includes the $ optional prefix to allow dead-letter queues.
    [GeneratedRegex(@"^(?=.{1,260}$)(?=^[A-Za-z0-9\$])(?!.*[\\?#])(?:[A-Za-z0-9\$]|[A-Za-z0-9\$][A-Za-z0-9./_-]*[A-Za-z0-9])$")]
    private static partial Regex QueueNameRegex();

    public static ValidationResult? ValidateRules(IEnumerable<string> ruleNames, string? memberName)
    {
        var ruleNameRegex = RuleNameRegex();
        var invalidRules = ruleNames.Where(t => !ruleNameRegex.IsMatch(t)).ToArray();

        return invalidRules.Any()
            ? new ValidationResult(
                $"The following rule name(s) do not comply with the Azure Service Bus rule limits: {string.Join(", ", invalidRules)}",
                memberName is not null ? [memberName] : [])
            : ValidationResult.Success;
    }

    // Enforces naming according to the specification https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules#microsoftservicebus
    [GeneratedRegex(@"^(?!\$)(?=.{1,50}$)(?=^[A-Za-z0-9])(?!.*[\/\\?#])[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$")]
    private static partial Regex RuleNameRegex();

    public static ValidationResult? ValidateSubscriptions(IEnumerable<string> subscriptionNames,
        string? memberName)
    {
        var subscriptionNameRegex = SubscriptionNameRegex();
        var invalidSubscriptions = subscriptionNames.Where(t => !subscriptionNameRegex.IsMatch(t)).ToArray();

        return invalidSubscriptions.Any()
            ? new ValidationResult(
                $"The following subscription name(s) do not comply with the Azure Service Bus subscription limits: {string.Join(", ", invalidSubscriptions)}",
                memberName is not null ? [memberName] : [])
            : ValidationResult.Success;
    }

    //enforces naming according to the specification https://learn.microsoft.com/en-us/azure/azure-resource-manager/management/resource-name-rules#microsoftservicebus
    // Note the subscription pattern is the same as the rule pattern. Deliberately kept separate for future extensibility.
    [GeneratedRegex(@"^(?!\$)(?=.{1,50}$)(?=^[A-Za-z0-9])(?!.*[\/\\?#])[A-Za-z0-9](?:[A-Za-z0-9._-]*[A-Za-z0-9])?$")]
    private static partial Regex SubscriptionNameRegex();
}