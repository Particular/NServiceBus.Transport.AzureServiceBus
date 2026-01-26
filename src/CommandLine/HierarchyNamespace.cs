namespace NServiceBus.Transport.AzureServiceBus.CommandLine;

using System;
using McMaster.Extensions.CommandLineUtils;

static class HierarchyNamespaceExtensions
{
    internal static string ToHierarchyNamespaceAwareDestination(this CommandArgument destinationArg, CommandOption hierarchyNamespaceOption)
        => destinationArg.Value.ToHierarchyNamespaceAwareDestination(hierarchyNamespaceOption);

    internal static string ToHierarchyNamespaceAwareDestination(this CommandOption destinationOption, CommandOption hierarchyNamespaceOption)
        => destinationOption.Value().ToHierarchyNamespaceAwareDestination(hierarchyNamespaceOption);

    internal static string ToHierarchyNamespaceAwareDestination(this string destination, CommandOption hierarchyNamespaceOption)
    {
        var hierarchyNamespace = hierarchyNamespaceOption.HasValue() ? hierarchyNamespaceOption.Value() : null;

        if (string.IsNullOrEmpty(destination) || string.IsNullOrEmpty(hierarchyNamespace))
        {
            return destination;
        }

        var prefix = string.Concat(hierarchyNamespace, '/');

        if (destination.StartsWith(prefix, StringComparison.Ordinal))
        {
            return destination;
        }

        return string.Concat(prefix, destination);
    }
}