namespace NServiceBus.Transport.AzureServiceBus;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates the <see cref="MigrationTopologyOptions"/>.
/// </summary>
[ObsoleteEx(Message = MigrationTopology.ObsoleteMessage, TreatAsErrorFromVersion = "7", RemoveInVersion = "8")]
[OptionsValidator]
public partial class MigrationTopologyOptionsValidator : IValidateOptions<MigrationTopologyOptions>;