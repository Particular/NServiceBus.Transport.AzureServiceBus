#nullable enable
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
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        value switch
        {
            Dictionary<string, string> dic => EntityValidator.ValidateQueues(dic.Keys, validationContext.MemberName),
            _ => ValidationResult.Success,
        };
}