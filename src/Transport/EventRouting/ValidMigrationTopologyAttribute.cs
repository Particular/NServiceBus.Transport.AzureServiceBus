namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
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
#pragma warning disable CS0618 // Type or member is obsolete
            MigrationTopologyOptions options => ValidateMigrationTopology(options),
#pragma warning restore CS0618 // Type or member is obsolete
            _ => ValidationResult.Success,
        };

#pragma warning disable CS0618 // Type or member is obsolete
    static ValidationResult? ValidateMigrationTopology(MigrationTopologyOptions options)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        var builder = new ValidateOptionsResultBuilder();

        foreach ((string? eventTypeFullName, string? topic) in options.PublishedEventToTopicsMap)
        {
            if (options.EventsToMigrateMap.Contains(eventTypeFullName))
            {
                builder.AddResult(new ValidationResult(
                    $"Event '{eventTypeFullName}' is in the migration map and in the published event to topics map. An event type cannot be marked for migration and mapped to a topic at the same time.",
                    [nameof(options.PublishedEventToTopicsMap)]));
            }

            if (topic.Equals(options.TopicToPublishTo))
            {
                builder.AddResult(new ValidationResult(
                    $"The topic to publish '{topic}' for '{eventTypeFullName}' cannot be the same as the topic to publish to '{options.TopicToPublishTo}' for the migration topology.",
                    [nameof(options.TopicToPublishTo), nameof(options.PublishedEventToTopicsMap)]));
            }
        }

        foreach ((string? eventTypeFullName, HashSet<string> topics) in options.SubscribedEventToTopicsMap)
        {
            if (options.EventsToMigrateMap.Contains(eventTypeFullName))
            {
                builder.AddResult(new ValidationResult(
                    $"Event '{eventTypeFullName}' is in the migration map and in the subscribed event to topics map. An event type cannot be marked for migration and mapped to a topic at the same time.",
                    [nameof(options.SubscribedEventToTopicsMap)]));
            }

            foreach (string topic in topics)
            {
                if (topic.Equals(options.TopicToSubscribeOn))
                {
                    builder.AddResult(new ValidationResult(
                        $"The topic to subscribe '{topic}' for '{eventTypeFullName}' cannot be the same as the topic to subscribe to '{options.TopicToSubscribeOn}' for the migration topology.",
                        [nameof(options.TopicToSubscribeOn), nameof(options.SubscribedEventToTopicsMap)]));
                }
            }
        }

        var result = builder.Build();
#pragma warning disable CS0618 // Type or member is obsolete
        return result.Succeeded ? ValidationResult.Success : new ValidationResult(result.FailureMessage, [nameof(MigrationTopologyOptions.EventsToMigrateMap)]);
#pragma warning restore CS0618 // Type or member is obsolete
    }
}