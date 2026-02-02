namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Configuration;

/// <summary>
/// Validates whether the values in a dictionary passed to the property are valid Azure Service Bus queues.
/// </summary>
[Experimental(DiagnosticDescriptors.ExperimentalQueuesAttribute)]
[AttributeUsage(AttributeTargets.Property)]
public sealed class AzureServiceBusQueuesAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        var hierarchyOptions = (validationContext.ObjectInstance as TopologyOptions)?.HierarchyNamespaceOptions ?? HierarchyNamespaceOptions.None;
        return value switch
        {
            Dictionary<string, string> dic => EntityValidator.ValidateQueues(dic.Keys, validationContext.MemberName, hierarchyOptions),
            _ => ValidationResult.Success,
        };
    }
}