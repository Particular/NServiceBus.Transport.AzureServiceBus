#nullable enable
namespace NServiceBus.Transport.AzureServiceBus;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates the <see cref="MigrationTopologyOptions"/>.
/// </summary>
[OptionsValidator]
public partial class MigrationTopologyOptionsValidator : IValidateOptions<MigrationTopologyOptions>;