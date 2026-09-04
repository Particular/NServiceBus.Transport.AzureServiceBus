namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

using System;

static class AcceptanceTestConnectionString
{
    public static string Get() => Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");

    public static string RestrictedConnectionString => Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString_Restricted");
}
