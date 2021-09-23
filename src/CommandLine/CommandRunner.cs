namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class CommandRunner
    {
        public static async Task Run(CommandOption connectionString, Func<ServiceBusAdministrationClient, Task> func)
        {
            var connectionStringToUse = connectionString.HasValue() ? connectionString.Value() : Environment.GetEnvironmentVariable(EnvironmentVariableName);

            var client = new ServiceBusAdministrationClient(connectionStringToUse);

            await func(client);
        }

        public const string EnvironmentVariableName = "AzureServiceBus_ConnectionString";
    }
}