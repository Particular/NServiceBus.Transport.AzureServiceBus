namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.ServiceBus.Management;

    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "asb-transport"
            };

            app.HelpOption(inherited: true);

            var connectionString = app.Option("-c|--connection-string", "Connection string to Azure Service Bus (defaults to value from environment variable 'x')", CommandOptionType.SingleValue, inherited: true);

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

                    var topicName = createCommand.Option("-t|--topic", "Topic name (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                    var subscriptionName = createCommand.Option("-s|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);

                    createCommand.OnExecute(() =>
                    {
                        Console.WriteLine($"Endpoint name '{name.Value}', topic name '{(topicName.HasValue() ? topicName.Value() : "bundle-1")}', " +
                                          $"subscription name '{(subscriptionName.HasValue() ? subscriptionName.Value() : name.Value)}'");

                    });
                });
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
                    createCommand.Description = "Creates a queue with the settings required by the transport.";
                    var name = createCommand.Argument("name", "Name of the queue (required)").IsRequired();
                    
                    var size = createCommand.Option<int>("-s|--size", "Queue size in GB (defaults to 5)", CommandOptionType.SingleValue);
                    var partitioning = createCommand.Option("-p|--partitioned", "Enable partitioning", CommandOptionType.NoValue);
                    
                    createCommand.OnExecute(async () =>
                    {
                        var connectionStringToUse = connectionString.HasValue() ? connectionString.Value() : Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");

                        var client = new ManagementClient(connectionStringToUse);

                        var queueDescription = new QueueDescription(name.Value)
                        {
                            EnableBatchedOperations = true,
                            LockDuration = TimeSpan.FromMinutes(5),
                            MaxDeliveryCount = int.MaxValue,
                            MaxSizeInMB = (size.HasValue() ? size.ParsedValue : 5) * 1024,
                            EnablePartitioning = partitioning.HasValue()
                        };

                        await client.CreateQueueAsync(queueDescription);
                        
                        Console.WriteLine($"Queue name '{name.Value}', size '{(size.HasValue() ? size.ParsedValue : 5)}GB', partitioned '{partitioning.HasValue()}' created");
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
        }
    }
}