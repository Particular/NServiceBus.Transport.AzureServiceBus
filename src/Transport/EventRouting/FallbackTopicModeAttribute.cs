namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Configuration;

/// <summary>
/// Validates that a <see cref="FallbackTopicOptions.Mode"/> value is one of the supported
/// shared-topic routing modes. When a fallback topic is configured, the mode must be
/// <see cref="TopicRoutingMode.CorrelationFilter"/> or <see cref="TopicRoutingMode.SqlFilter"/>.
/// </summary>
[Experimental(DiagnosticDescriptors.ExperimentalFallbackTopicModeAttribute)]
[AttributeUsage(AttributeTargets.Property)]
public sealed class FallbackTopicModeAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        value switch
        {
            null or TopicRoutingMode.NotMultiplexed => new ValidationResult($"'{validationContext.MemberName}' must be either '{TopicRoutingMode.CorrelationFilter}' or '{TopicRoutingMode.SqlFilter}'.", validationContext.MemberName is not null ? [validationContext.MemberName] : []),
            TopicRoutingMode when Enum.GetNames<TopicRoutingMode>().Contains(value.ToString()) => ValidationResult.Success,
            _ => new ValidationResult($"'{validationContext.MemberName}' has value '{value}' which is not a recognized {nameof(TopicRoutingMode)}.", validationContext.MemberName is not null ? [validationContext.MemberName] : [])
        };
}