namespace NServiceBus.Transport.AzureServiceBus;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates the <see cref="MigrationTopologyOptions"/>.
/// </summary>
[ObsoleteEx(Message = MigrationTopology.ObsoleteMessage, TreatAsErrorFromVersion = MigrationTopology.TreatAsErrorFromVersion, RemoveInVersion = MigrationTopology.RemoveInVersion)]
[OptionsValidator]
public partial class MigrationTopologyOptionsValidator : IValidateOptions<MigrationTopologyOptions>;