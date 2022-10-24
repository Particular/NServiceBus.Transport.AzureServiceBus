namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Extensions.Logging;

    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "asb-transport"
            };

            var connectionString = new CommandOption("-c|--connection-string", CommandOptionType.SingleValue)
            {
                Description = $"Overrides environment variable '{CommandRunner.EnvironmentVariableName}'"
            };

            var size = new CommandOption<int>(app.ValueParsers.GetParser<int>(), "-s|--size", CommandOptionType.SingleValue)
            {
                Description = "Queue size in GB (defaults to 5)"
            };

            var partitioning = new CommandOption("-p|--partitioned", CommandOptionType.NoValue)
            {
                Description = "Enable partitioning"
            };

            app.HelpOption(inherited: true);

            var ignoreUpdates = app.Option("-i|--ignore-updates", "Ignore tool updates.", CommandOptionType.NoValue, true);
            var verboseOption = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue, true);
            var versionOption = app.Option("--version", "Show the current version of the tool", CommandOptionType.NoValue, true);

            var logger = new ConsoleLogger(verboseOption.HasValue());

            app.Command("endpoint", endpointCommand =>
            {
                endpointCommand.OnExecute(() =>
                {
                    logger.LogWarning("Specify a subcommand");
                    endpointCommand.ShowHelp();
                    return 1;
                });

                endpointCommand.Command("create", createCommand =>
                {
                    createCommand.Description = "Creates required infrastructure for an endpoint.";
                    var name = createCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                    createCommand.AddOption(connectionString);
                    createCommand.AddOption(size);
                    createCommand.AddOption(partitioning);
                    var topicName = createCommand.Option("-t|--topic", "Topic name (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                    var subscriptionName = createCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);

                    createCommand.OnExecuteAsync(async ct =>
                    {
                        await CommandRunner.Run(connectionString, client => Endpoint.Create(logger, client, name, topicName, subscriptionName, size, partitioning));

                        logger.LogInformation($"Endpoint '{name.Value}' is ready.");
                    });
                });

                endpointCommand.Command("subscribe", subscribeCommand =>
                {
                    subscribeCommand.Description = "Subscribes an endpoint to an event.";
                    var name = subscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                    var eventType = subscribeCommand.Argument("event-type", "Full name of the event to subscribe to (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                    subscribeCommand.AddOption(connectionString);
                    var topicName = subscribeCommand.Option("-t|--topic", "Topic name (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                    var subscriptionName = subscribeCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);
                    var shortenedRuleName = subscribeCommand.Option("-r|--rule-name", "Rule name (defaults to event type) ", CommandOptionType.SingleValue);

                    subscribeCommand.OnExecuteAsync(async ct =>
                    {
                        await CommandRunner.Run(connectionString, client => Endpoint.Subscribe(logger, client, name, topicName, subscriptionName, eventType, shortenedRuleName));

                        logger.LogInformation($"Endpoint '{name.Value}' subscribed to '{eventType.Value}'.");
                    });
                });

                endpointCommand.Command("unsubscribe", unsubscribeCommand =>
                {
                    unsubscribeCommand.Description = "Unsubscribes an endpoint from an event.";
                    var name = unsubscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                    var eventType = unsubscribeCommand.Argument("event-type", "Full name of the event to unsubscribe from (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                    unsubscribeCommand.AddOption(connectionString);
                    var topicName = unsubscribeCommand.Option("-t|--topic", "Topic name (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                    var subscriptionName = unsubscribeCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);
                    var shortenedRuleName = unsubscribeCommand.Option("-r|--rule-name", "Rule name (defaults to event type) ", CommandOptionType.SingleValue);

                    unsubscribeCommand.OnExecuteAsync(async ct =>
                    {
                        await CommandRunner.Run(connectionString, client => Endpoint.Unsubscribe(logger, client, name, topicName, subscriptionName, eventType, shortenedRuleName));

                        logger.LogInformation($"Endpoint '{name.Value}' unsubscribed from '{eventType.Value}'.");
                    });
                });
            });

            app.Command("queue", queueCommand =>
            {
                queueCommand.OnExecute(() =>
                {
                    logger.LogWarning("Specify a subcommand");
                    queueCommand.ShowHelp();
                    return 1;
                });

                queueCommand.Command("create", createCommand =>
                {
                    createCommand.Description = "Creates a queue with the settings required by the transport";
                    var name = createCommand.Argument("name", "Name of the queue (required)").IsRequired();

                    createCommand.AddOption(connectionString);
                    createCommand.AddOption(size);
                    createCommand.AddOption(partitioning);

                    createCommand.OnExecuteAsync(async ct =>
                    {
                        try
                        {
                            await CommandRunner.Run(connectionString, client => Queue.Create(client, name, size, partitioning));
                            logger.LogInformation($"Queue name '{name.Value}', size '{(size.HasValue() ? size.ParsedValue : 5)}GB', partitioned '{partitioning.HasValue()}' created");
                        }
                        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                        {
                            logger.LogInformation($"Queue '{name.Value}' already exists, skipping creation");
                        }
                    });
                });

                queueCommand.Command("delete", deleteCommand =>
                {
                    deleteCommand.AddOption(connectionString);

                    deleteCommand.Description = "Deletes a queue";
                    var name = deleteCommand.Argument("name", "Name of the queue (required)").IsRequired();

                    deleteCommand.OnExecuteAsync(async ct =>
                    {
                        await CommandRunner.Run(connectionString, client => Queue.Delete(client, name));

                        logger.LogInformation($"Queue name '{name.Value}' deleted");
                    });
                });
            });

            app.OnExecuteAsync(async cancellationToken =>
            {
                logger.LogInformation(ToolVersion.GetVersionInfo());

                if (versionOption.HasValue())
                {
                    return 0;
                }

                if (!await ToolVersion.CheckIsLatestVersion(logger, ignoreUpdates.HasValue(), cancellationToken).ConfigureAwait(false))
                {
                    return 1;
                }

                logger.LogWarning("Specify a subcommand");
                app.ShowHelp();
                return 1;
            });

            try
            {
                return await app.ExecuteAsync(args).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogError($"Command failed with exception ({exception.GetType().Name}): {exception.Message}");
                return 1;
            }
        }
    }
}