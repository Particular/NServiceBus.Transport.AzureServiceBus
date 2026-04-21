using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using NServiceBus;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
public partial class TransportSettingsConventionTests
{
    [Test]
    public void Transport_properties_have_PreObsolete_extension_method_mappings()
    {
        var transportType = typeof(AzureServiceBusTransport);

        var transportProperties = transportType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(p => !IsObsolete(p))
            .Where(p => p.SetMethod is not null && p.SetMethod.IsPublic)
            .ToList();

        var extensionSource = File.ReadAllText(ResolveTransportExtensionSourcePath());

        var replacements = PreObsoleteReplacementPattern()
            .Matches(extensionSource)
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

        var mapped = new List<string>();
        var unmapped = new List<string>();

        foreach (var property in transportProperties)
        {
            var expectedReplacement = $"AzureServiceBusTransport.{property.Name}";
            if (replacements.Contains(expectedReplacement))
            {
                mapped.Add(property.Name);
            }
            else
            {
                unmapped.Add(property.Name);
            }
        }

        var builder = new StringBuilder();
        builder.AppendLine("Mapped properties:");
        foreach (var name in mapped.OrderBy(x => x))
        {
            builder.AppendLine($"  - {name}");
        }

        builder.AppendLine();
        builder.AppendLine("Unmapped properties:");
        foreach (var name in unmapped.OrderBy(x => x))
        {
            builder.AppendLine($"  - {name}");
        }

        Approver.Verify(builder.ToString());
    }

    [GeneratedRegex(@"ReplacementTypeOrMember\s*=\s*""([^""]*)""", RegexOptions.Compiled)]
    private static partial Regex PreObsoleteReplacementPattern();

    static string ResolveTransportExtensionSourcePath([CallerFilePath] string callerFilePath = "")
    {
        var callerDir = new FileInfo(callerFilePath).Directory!;
        var fileName = $"{nameof(AzureServiceBusTransportSettingsExtensions)}.cs";
        var candidate = Path.Combine(callerDir.FullName, "..", "..", "Transport", fileName);
        var fullPath = Path.GetFullPath(candidate);

        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException($"Could not locate {fileName}. Searched: {fullPath}");
    }

    static bool IsObsolete(PropertyInfo property)
    {
        var obsoleteAttr = property.GetCustomAttribute<ObsoleteAttribute>();
        if (obsoleteAttr is not null)
        {
            return true;
        }

        return property.GetCustomAttributesData()
            .Any(a => a.AttributeType.Name == "ObsoleteMetadataAttribute");
    }
}
