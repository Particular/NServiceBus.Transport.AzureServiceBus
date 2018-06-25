namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using McMaster.Extensions.CommandLineUtils.Abstractions;
    using Microsoft.Azure.ServiceBus.Management;

    class Program
    {
        const string EnvironmentVariableName = "AzureServiceBus_ConnectionString";

        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "asb-transport"
            };

            var connectionString = new CommandOption("-c|--connection-string", CommandOptionType.SingleValue)
            {
                Description = $"Overrides environment variable '{EnvironmentVariableName}'"
            };

            // TODO: change to user ValueParserProvider when https://github.com/natemcmaster/CommandLineUtils/issues/109 is fixed
            var size = new CommandOption<int>(Int32ValueParser.Singleton, "-s|--size", CommandOptionType.SingleValue)
            {
                Description = "Queue size in GB (defaults to 5)"
            };

            var partitioning = new CommandOption("-p|--partitioned", CommandOptionType.NoValue)
            {
                Description = "Enable partitioning"
            };
            
            app.HelpOption(inherited: true);
            
            app.Command("endpoint", endpointCommand =>
            {
                endpointCommand.OnExecute(() =>
                {
                    Console.WriteLine("Specify a subcommand");
                    endpointCommand.ShowHelp();
                    return 1;
                });

                endpointCommand.Command("create", createCommand =>
                {
                    createCommand.Description = "Creates required infrastructure for an endpoint.";
                    var name = createCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    createCommand.Options.Add(connectionString);
                    createCommand.Options.Add(size);
                    createCommand.Options.Add(partitioning);
                    var topicName = createCommand.Option("-t|--topic", "Topic name (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                    var subscriptionName = createCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);

                    createCommand.OnExecute(async () =>
                    {
                        await Run(async client => await Endpoint.Create(client, name, topicName, subscriptionName, size, partitioning));

                        Console.WriteLine($"Endpoint '{name.Value}' is ready.");
                    });
                });

                // TODO: implement "endpoint show subscriptions"
            });

            app.Command("queue", queueCommand =>
            {
                queueCommand.OnExecute(() =>
                {
                    Console.WriteLine("Specify a subcommand");
                    queueCommand.ShowHelp();
                    return 1;
                });

                queueCommand.Command("create", createCommand =>
                {
                    createCommand.Description = "Creates a queue with the settings required by the transport";
                    var name = createCommand.Argument("name", "Name of the queue (required)").IsRequired();

                    createCommand.Options.Add(connectionString);
                    createCommand.Options.Add(size);
                    createCommand.Options.Add(partitioning);

                    createCommand.OnExecute(async () =>
                    {
                        await Run(client => Queue.Create(client, name, size, partitioning));

                        Console.WriteLine($"Queue name '{name.Value}', size '{(size.HasValue() ? size.ParsedValue : 5)}GB', partitioned '{partitioning.HasValue()}' created");
                    });
                });

                queueCommand.Command("delete", deleteCommand =>
                {
                    deleteCommand.Options.Add(connectionString);

                    deleteCommand.Description = "Deletes a queue";
                    var name = deleteCommand.Argument("name", "Name of the queue (required)").IsRequired();

                    deleteCommand.OnExecute(async () =>
                    {
                        await Run(client => Queue.Delete(client, name));

                        Console.WriteLine($"Queue name '{name.Value}' deleted");
                    });
                });
            });

            app.OnExecute(() =>
            {
                Console.WriteLine("Specify a subcommand");
                app.ShowHelp();
                return 1;
            });

            return app.Execute(args);

            async Task Run(Func<ManagementClient, Task> func)
            {
                var connectionStringToUse = connectionString.HasValue() ? connectionString.Value() : Environment.GetEnvironmentVariable(EnvironmentVariableName);

                var client = new ManagementClient(connectionStringToUse);

                await func(client);

                await client.CloseAsync();
            }
        }

    }
}