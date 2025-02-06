namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Configuration;

/// <summary>
/// Validates whether the values in a dictionary passed to the property are valid Azure Service Bus subscriptions.
/// </summary>
[Experimental(DiagnosticDescriptors.ExperimentalSubscriptionsAttribute)]
[AttributeUsage(AttributeTargets.Property)]
public sealed class AzureServiceBusSubscriptionsAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        value switch
        {
            Dictionary<string, string> dic => EntityValidator.ValidateSubscriptions(dic.Values, validationContext.MemberName),
            _ => ValidationResult.Success,
        };
}