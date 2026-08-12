namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

using System;

static class AcceptanceTestConnectionString
{
    public static string Get() => Environment.GetEnvironmentVariable("AzureServiceBus_OrderedConnectionString");

    public static string RestrictedConnectionString => Environment.GetEnvironmentVariable("AzureServiceBus_OrderedConnectionString_Restricted");
}
