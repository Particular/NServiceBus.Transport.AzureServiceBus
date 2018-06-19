namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.ServiceBus;
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

                    var size = createCommand.Option<int>("-s|--size", "Queue size in GB (defaults to 5)", CommandOptionType.SingleValue);
                    var partitioning = createCommand.Option("-p|--partitioned", "Enable partitioning", CommandOptionType.NoValue);
                    var topicName = createCommand.Option("-t|--topic", "Topic name (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                    var subscriptionName = createCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);

                    createCommand.OnExecute(async () =>
                    {
                        var topicNameToUse = topicName.HasValue() ? topicName.Value() : "bundle-1";
                        var subscriptionNameToUse = subscriptionName.HasValue() ? subscriptionName.Value() : name.Value;

                        await Run(async client =>
                        {
                            try
                            {
                                await CreateQueue(client, name, size, partitioning);
                            }
                            catch (MessagingEntityAlreadyExistsException)
                            {
                                Console.WriteLine("Queue already exists, skipping creation");
                            }

                            var topicDescription = new TopicDescription(topicNameToUse)
                            {
                                EnableBatchedOperations = true,
                                EnablePartitioning = partitioning.HasValue(),
                                MaxSizeInMB = (size.HasValue() ? size.ParsedValue : 5) * 1024,
                            };

                            try
                            {
                                await client.CreateTopicAsync(topicDescription);
                            }
                            catch (MessagingEntityAlreadyExistsException)
                            {
                                Console.WriteLine("Topic already exists, skipping creation");
                            }

                            var subscriptionDescription = new SubscriptionDescription(topicNameToUse, subscriptionNameToUse)
                            {
                                LockDuration = TimeSpan.FromMinutes(5),
                                ForwardTo = name.Value,
                                EnableDeadLetteringOnFilterEvaluationExceptions = false,
                                MaxDeliveryCount = int.MaxValue,
                                // TODO: uncomment when https://github.com/Azure/azure-service-bus-dotnet/issues/499 is fixed
                                //EnableBatchedOperations = true,
                                // TODO: https://github.com/Azure/azure-service-bus-dotnet/issues/501 is fixed
                                //UserMetadata = name.Value
                            };

                            try
                            {
                                await client.CreateSubscriptionAsync(subscriptionDescription);
                            }
                            catch (MessagingEntityAlreadyExistsException)
                            {
                                Console.WriteLine("Subscription already exists, skipping creation");
                            }

                            try
                            {
                                // TODO: remove when https://github.com/Azure/azure-service-bus-dotnet/issues/502 is implemented
                                await client.DeleteRuleAsync(topicNameToUse, subscriptionNameToUse, RuleDescription.DefaultRuleName);
                            }
                            catch (MessagingEntityNotFoundException)
                            {
                            }
                        });

                        Console.WriteLine($"Endpoint name '{name.Value}', topic name '{topicNameToUse}', " +
                                          $"subscription name '{(subscriptionName.HasValue() ? subscriptionName.Value() : name.Value)}'");

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
                    
                    var size = createCommand.Option<int>("-s|--size", "Queue size in GB (defaults to 5)", CommandOptionType.SingleValue);
                    var partitioning = createCommand.Option("-p|--partitioned", "Enable partitioning", CommandOptionType.NoValue);
                    
                    createCommand.OnExecute(async () =>
                    {
                        await Run(client => CreateQueue(client, name, size, partitioning));

                        Console.WriteLine($"Queue name '{name.Value}', size '{(size.HasValue() ? size.ParsedValue : 5)}GB', partitioned '{partitioning.HasValue()}' created");
                    });
                });

                queueCommand.Command("delete", deleteCommand =>
                {
                    deleteCommand.Description = "Deletes a queue";
                    var name = deleteCommand.Argument("name", "Name of the queue (required)").IsRequired();

                    deleteCommand.OnExecute(async () =>
                    {
                        await Run(client => client.DeleteQueueAsync(name.Value));

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
                var connectionStringToUse = connectionString.HasValue() ? connectionString.Value() : Environment.GetEnvironmentVariable("AzureServiceBus_ConnectionString");

                var client = new ManagementClient(connectionStringToUse);

                await func(client);

                await client.CloseAsync();
            }
        }

        static Task CreateQueue(ManagementClient client, CommandArgument name, CommandOption<int> size, CommandOption partitioning)
        {
            var queueDescription = new QueueDescription(name.Value)
            {
                EnableBatchedOperations = true,
                LockDuration = TimeSpan.FromMinutes(5),
                MaxDeliveryCount = int.MaxValue,
                MaxSizeInMB = (size.HasValue() ? size.ParsedValue : 5) * 1024,
                EnablePartitioning = partitioning.HasValue()
            };

            return client.CreateQueueAsync(queueDescription);
        }
    }
}