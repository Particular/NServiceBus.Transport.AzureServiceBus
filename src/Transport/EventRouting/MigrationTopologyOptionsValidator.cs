namespace NServiceBus.Transport.AzureServiceBus;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates the <see cref="MigrationTopologyOptions"/>.
/// </summary>
[ObsoleteEx(Message = MigrationTopology.ObsoleteMessage, TreatAsErrorFromVersion = "6", RemoveInVersion = "7")]
[OptionsValidator]
public partial class MigrationTopologyOptionsValidator : IValidateOptions<MigrationTopologyOptions>;