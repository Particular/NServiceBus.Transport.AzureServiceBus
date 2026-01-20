namespace NServiceBus.Transport.AzureServiceBus.Tests;

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Azure.Messaging.ServiceBus.Administration;

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CreateSubscriptionOptions))]
[JsonSerializable(typeof(CreateTopicOptions))]
[JsonSerializable(typeof(CreateRuleOptions))]
public partial class SkdJsonSerializerContext : JsonSerializerContext
{
#if !NET10_0_OR_GREATER
    static JsonSerializerOptions options;
#endif

    public static JsonSerializerOptions PolymorphicOptions =>
#if NET10_0_OR_GREATER
        field ??= new JsonSerializerOptions(Default.Options)
#else
        options ??= new JsonSerializerOptions(Default.Options)
#endif
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers =
                {
                    typeInfo =>
                    {
                        if (typeInfo.Type == typeof(RuleFilter))
                        {
                            typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
                            {
                                TypeDiscriminatorPropertyName = "filter-type",
                                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                                DerivedTypes =
                                {
                                    new JsonDerivedType(typeof(CorrelationRuleFilter), "correlation"),
                                    new JsonDerivedType(typeof(SqlRuleFilter), "sql"),
                                    new JsonDerivedType(typeof(TrueRuleFilter), "true"),
                                    new JsonDerivedType(typeof(FalseRuleFilter), "false")
                                }
                            };
                        }
                    }
                }
            }
        };
}