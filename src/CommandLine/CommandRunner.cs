namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.ServiceBus.Management;

    static class CommandRunner
    {
        public static async Task Run(CommandOption connectionString, Func<ManagementClient, Task> func)
        {
            var connectionStringToUse = connectionString.HasValue() ? connectionString.Value() : Environment.GetEnvironmentVariable(EnvironmentVariableName);

            var client = new ManagementClient(connectionStringToUse);

            await func(client);

            await client.CloseAsync();
        }

        public const string EnvironmentVariableName = "AzureServiceBus_ConnectionString";
    }
}