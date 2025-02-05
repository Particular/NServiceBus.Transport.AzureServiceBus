namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Configuration;

/// <summary>
/// Validates wether the string value, the values in a dictionary or the values in a dictionary of hashsets
/// passed to the property are valid Azure Service Bus topics.
/// </summary>
[Experimental(DiagnosticDescriptors.ExperimentalTopicsAttribute)]
[AttributeUsage(AttributeTargets.Property)]
public sealed class AzureServiceBusTopicsAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        value switch
        {
            string topic => EntityValidator.ValidateTopics([topic], validationContext.MemberName),
            Dictionary<string, string> dic => EntityValidator.ValidateTopics(dic.Values, validationContext.MemberName),
            Dictionary<string, HashSet<string>> set => EntityValidator.ValidateTopics(set.Values.SelectMany(x => x),
                validationContext.MemberName),
            _ => ValidationResult.Success,
        };
}