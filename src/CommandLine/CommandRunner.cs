namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Azure.Identity;
    using Azure.Messaging.ServiceBus.Administration;
    using McMaster.Extensions.CommandLineUtils;

    static class CommandRunner
    {
        public static async Task Run(CommandOption connectionString, CommandOption fullyQualifiedNamespace, Func<ServiceBusAdministrationClient, Task> func)
        {
            ServiceBusAdministrationClient client;
            if (fullyQualifiedNamespace.HasValue())
            {
                client = new ServiceBusAdministrationClient(fullyQualifiedNamespace.Value(), new DefaultAzureCredential());
            }
            else
            {
                var connectionStringToUse = connectionString.HasValue() ? connectionString.Value() : Environment.GetEnvironmentVariable(EnvironmentVariableName);
                client = new ServiceBusAdministrationClient(connectionStringToUse);
            }
            await func(client);
        }

        public const string EnvironmentVariableName = "AzureServiceBus_ConnectionString";
    }
}