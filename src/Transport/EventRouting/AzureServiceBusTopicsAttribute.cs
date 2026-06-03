namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Configuration;

/// <summary>
/// Validates whether the string value, the values in a dictionary or the values in a dictionary of hashsets
/// passed to the property are valid Azure Service Bus topics.
/// </summary>
[Experimental(DiagnosticDescriptors.ExperimentalTopicsAttribute)]
[AttributeUsage(AttributeTargets.Property)]
public sealed class AzureServiceBusTopicsAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var hierarchyOptions = (validationContext.ObjectInstance as IHierarchyNamespaceAwareOptions)?.HierarchyNamespaceOptions ?? HierarchyNamespaceOptions.None;
        return value switch
        {
            string topic => EntityValidator.ValidateTopics([topic], validationContext.MemberName, hierarchyOptions),
            Dictionary<string, string> dic => EntityValidator.ValidateTopics(dic, validationContext.MemberName, hierarchyOptions),
            Dictionary<string, HashSet<string>> set => EntityValidator.ValidateTopics(set, validationContext.MemberName, hierarchyOptions),
            Dictionary<string, HashSet<SubscriptionEntry>> set => EntityValidator.ValidateTopics(set, validationContext.MemberName, hierarchyOptions),
            Dictionary<string, PublishEntry> dic => EntityValidator.ValidateTopics(dic, validationContext.MemberName, hierarchyOptions),
            _ => ValidationResult.Success,
        };
    }
}