namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Configuration;
using Microsoft.Extensions.Options;

/// <summary>
/// Validates whether the <see cref="MigrationTopologyOptions"/> are valid and do not contain conflicting mapped event types.
/// </summary>
[Experimental(DiagnosticDescriptors.ExperimentalValidMigrationTopologyAttribute)]
[AttributeUsage(AttributeTargets.Property)]
public sealed class ValidMigrationTopologyAttribute : ValidationAttribute
{
    /// <inheritdoc />
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        validationContext.ObjectInstance switch
        {
            MigrationTopologyOptions options => ValidateMigrationTopology(options),
            _ => ValidationResult.Success,
        };

    static ValidationResult? ValidateMigrationTopology(MigrationTopologyOptions options)
    {
        var builder = new ValidateOptionsResultBuilder();
        foreach (var eventTypeFullname in options.EventsToMigrateMap)
        {
            if (options.PublishedEventToTopicsMap.ContainsKey(eventTypeFullname))
            {
                builder.AddResult(new ValidationResult(
                    $"Event '{eventTypeFullname}' is in the migration map and in the published event to topics map. An event type cannot be marked for migration and mapped to a topic at the same time.", [nameof(options.PublishedEventToTopicsMap)]));
            }

            if (options.SubscribedEventToTopicsMap.ContainsKey(eventTypeFullname))
            {
                builder.AddResult(new ValidationResult($"Event '{eventTypeFullname}' is in the migration map and in the subscribed event to topics map. An event type cannot be marked for migration and mapped to a topic at the same time.", [nameof(options.SubscribedEventToTopicsMap)]));
            }
        }

        var result = builder.Build();
        return result.Succeeded ? ValidationResult.Success : new ValidationResult(result.FailureMessage, [nameof(MigrationTopologyOptions.EventsToMigrateMap)]);
    }
}