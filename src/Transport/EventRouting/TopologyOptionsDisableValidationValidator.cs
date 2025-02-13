namespace NServiceBus.Transport.AzureServiceBus;

using Microsoft.Extensions.Options;

/// <summary>
/// Does not validate the provided <see cref="TopologyOptions"/>.
/// </summary>
public sealed class TopologyOptionsDisableValidationValidator : IValidateOptions<TopologyOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, TopologyOptions options) => ValidateOptionsResult.Success;
}