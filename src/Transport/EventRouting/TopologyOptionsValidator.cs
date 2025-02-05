namespace NServiceBus.Transport.AzureServiceBus;

using Microsoft.Extensions.Options;

/// <summary>
/// Validates the <see cref="TopologyOptions"/>.
/// </summary>
[OptionsValidator]
public partial class TopologyOptionsValidator : IValidateOptions<TopologyOptions>;