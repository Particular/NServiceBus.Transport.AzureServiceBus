namespace NServiceBus.Transport.AzureServiceBus.CommandLine
{
    using System;
    using System.ComponentModel.DataAnnotations;
    using Azure.Messaging.ServiceBus;
    using McMaster.Extensions.CommandLineUtils;

    class Program
    {
        static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "asb-transport"
            };

            var connectionString = new CommandOption("-c|--connection-string", CommandOptionType.SingleValue)
            {
                Description = $"Overrides environment variable '{CommandRunner.EnvironmentVariableName}'"
            };

            var fullyQualifiedNamespace = new CommandOption("-n|--namespace", CommandOptionType.SingleValue)
            {
                Description = "Sets the fully qualified namespace for connecting with cached credentials, such as those from Azure PowerShell or CLI"
            };
            fullyQualifiedNamespace.OnValidate(v => ValidateConnectionAndNamespaceNotUsedTogether(connectionString, fullyQualifiedNamespace));
            connectionString.OnValidate(v => ValidateConnectionAndNamespaceNotUsedTogether(connectionString, fullyQualifiedNamespace));

            var size = new CommandOption<int>(app.ValueParsers.GetParser<int>(), "-s|--size", CommandOptionType.SingleValue)
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
                    createCommand.Command("migration", createMigrationCommand =>
                    {
                        createMigrationCommand.Description = "Creates required infrastructure for an endpoint that uses migration topology.";
                        var name = createMigrationCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                        createMigrationCommand.AddOption(connectionString);
                        createMigrationCommand.AddOption(fullyQualifiedNamespace);
                        createMigrationCommand.AddOption(size);
                        createMigrationCommand.AddOption(partitioning);

                        var topicName = createMigrationCommand.Option("-st|--topic", "Topic name (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                        var topicToPublishTo = createMigrationCommand.Option("-tp|--topic-to-publish-to", "The topic name to publish to", CommandOptionType.SingleValue);
                        var topicToSubscribeOn = createMigrationCommand.Option("-ts|--topic-to-subscribe-on", "The topic name to subscribe on", CommandOptionType.SingleValue);

                        topicName.OnValidate(v => ValidateTopicInformationIsCorrect(topicName, topicToPublishTo, topicToSubscribeOn));
                        topicToPublishTo.OnValidate(v => ValidateTopicInformationIsCorrect(topicName, topicToPublishTo, topicToSubscribeOn));
                        topicToSubscribeOn.OnValidate(v => ValidateTopicInformationIsCorrect(topicName, topicToPublishTo, topicToSubscribeOn));

                        var subscriptionName = createMigrationCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);

                        createMigrationCommand.OnExecuteAsync(async ct =>
                        {
                            // Unfortunately the default value cannot be set outside the execute because it would then
                            // trigger the validation. There seems to be no way in the command handling library to
                            // differentiate defaults from user inputs we set a default for the topicName here.
                            if (!topicName.HasValue() && !topicToPublishTo.HasValue() && !topicToSubscribeOn.HasValue())
                            {
                                topicName.DefaultValue = Topic.DefaultTopicName;
                            }

                            await CommandRunner.Run(connectionString, fullyQualifiedNamespace,
                                client => MigrationTopologyEndpoint.Create(client, name, topicName, topicToPublishTo, topicToSubscribeOn,
                                    subscriptionName, size, partitioning));


                            Console.WriteLine($"Endpoint '{name.Value}' is ready.");
                        });
                    });

                    createCommand.Command("topic-per-event", createTopicPerEventCommand =>
                    {
                        createTopicPerEventCommand.Description = "Creates required infrastructure for an endpoint that uses topic-per-event topology.";
                        var name = createTopicPerEventCommand.Argument("name", "Name of the endpoint (required)").IsRequired();

                        createTopicPerEventCommand.AddOption(connectionString);
                        createTopicPerEventCommand.AddOption(fullyQualifiedNamespace);
                        createTopicPerEventCommand.AddOption(size);
                        createTopicPerEventCommand.AddOption(partitioning);

                        createTopicPerEventCommand.OnExecuteAsync(async ct =>
                        {
                            await CommandRunner.Run(connectionString, fullyQualifiedNamespace,
                                client => TopicPerEventTopologyEndpoint.Create(client, name, size, partitioning));

                            Console.WriteLine($"Endpoint '{name.Value}' is ready.");
                        });
                    });
                });

                endpointCommand.Command("subscribe", subscribeCommand =>
                {
                    subscribeCommand.Description = "Subscribes an endpoint to an event using topic-per-event topology.";
                    var name = subscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                    var topicName = subscribeCommand.Argument("topic", "Topic name to subscribe. Unless configured otherwise on the publisher defaults to full name of the event (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                    subscribeCommand.AddOption(connectionString);
                    subscribeCommand.AddOption(fullyQualifiedNamespace);
                    subscribeCommand.AddOption(size);
                    subscribeCommand.AddOption(partitioning);
                    var subscriptionName = subscribeCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);

                    subscribeCommand.OnExecuteAsync(async ct =>
                    {
                        await CommandRunner.Run(connectionString, fullyQualifiedNamespace, client => TopicPerEventTopologyEndpoint.Subscribe(client, name, topicName, subscriptionName, size, partitioning));

                        Console.WriteLine($"Endpoint '{name.Value}' subscribed to '{topicName.Value}'.");
                    });
                });

                endpointCommand.Command("unsubscribe", unsubscribeCommand =>
                {
                    unsubscribeCommand.Description = "Unsubscribes an endpoint from an event using topic-per-event topology.";
                    var name = unsubscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                    var topicName = unsubscribeCommand.Argument("topic", "Topic name to subscribe. Unless configured otherwise on the publisher defaults to full name of the event (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                    unsubscribeCommand.AddOption(connectionString);
                    unsubscribeCommand.AddOption(fullyQualifiedNamespace);
                    var subscriptionName = unsubscribeCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);

                    unsubscribeCommand.OnExecuteAsync(async ct =>
                    {
                        await CommandRunner.Run(connectionString, fullyQualifiedNamespace, client => TopicPerEventTopologyEndpoint.Unsubscribe(client, name, topicName, subscriptionName));

                        Console.WriteLine($"Endpoint '{name.Value}' unsubscribed from '{topicName.Value}'.");
                    });
                });

                endpointCommand.Command("non-migrated", nonMigratedSubCommand =>
                {
                    nonMigratedSubCommand.Command("subscribe", subscribeCommand =>
                    {
                        subscribeCommand.Description = "Subscribes an endpoint to an event using legacy forwarding topology.";
                        var name = subscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                        var eventType = subscribeCommand.Argument("event-type", "Full name of the event to subscribe to (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                        subscribeCommand.AddOption(connectionString);
                        subscribeCommand.AddOption(fullyQualifiedNamespace);
                        var topicName = subscribeCommand.Option("-t|--topic", "Topic name to subscribe on (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                        topicName.DefaultValue = Topic.DefaultTopicName;
                        var subscriptionName = subscribeCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);
                        var shortenedRuleName = subscribeCommand.Option("-r|--rule-name", "Rule name (defaults to event type) ", CommandOptionType.SingleValue);

                        subscribeCommand.OnExecuteAsync(async ct =>
                        {
                            await CommandRunner.Run(connectionString, fullyQualifiedNamespace, client => MigrationTopologyEndpoint.Subscribe(client, name, topicName, subscriptionName, eventType, shortenedRuleName));

                            Console.WriteLine($"Endpoint '{name.Value}' subscribed to '{eventType.Value}'.");
                        });
                    });

                    nonMigratedSubCommand.Command("unsubscribe", unsubscribeCommand =>
                    {
                        unsubscribeCommand.Description = "Unsubscribes an endpoint from an event using legacy forwarding topology.";
                        var name = unsubscribeCommand.Argument("name", "Name of the endpoint (required)").IsRequired();
                        var eventType = unsubscribeCommand.Argument("event-type", "Full name of the event to unsubscribe from (e.g. MyNamespace.MyMessage) (required)").IsRequired();

                        unsubscribeCommand.AddOption(connectionString);
                        unsubscribeCommand.AddOption(fullyQualifiedNamespace);
                        var topicName = unsubscribeCommand.Option("-t|--topic", "Topic name to unsubscribe from (defaults to 'bundle-1')", CommandOptionType.SingleValue);
                        topicName.DefaultValue = Topic.DefaultTopicName;
                        var subscriptionName = unsubscribeCommand.Option("-b|--subscription", "Subscription name (defaults to endpoint name) ", CommandOptionType.SingleValue);
                        var shortenedRuleName = unsubscribeCommand.Option("-r|--rule-name", "Rule name (defaults to event type) ", CommandOptionType.SingleValue);

                        unsubscribeCommand.OnExecuteAsync(async ct =>
                        {
                            await CommandRunner.Run(connectionString, fullyQualifiedNamespace, client => MigrationTopologyEndpoint.Unsubscribe(client, name, topicName, subscriptionName, eventType, shortenedRuleName));

                            Console.WriteLine($"Endpoint '{name.Value}' unsubscribed from '{eventType.Value}'.");
                        });
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
                    createCommand.Description = "Creates a queue with the settings required by the transport";
                    var name = createCommand.Argument("name", "Name of the queue (required)").IsRequired();

                    createCommand.AddOption(connectionString);
                    createCommand.AddOption(fullyQualifiedNamespace);
                    createCommand.AddOption(size);
                    createCommand.AddOption(partitioning);

                    createCommand.OnExecuteAsync(async ct =>
                    {
                        try
                        {
                            await CommandRunner.Run(connectionString, fullyQualifiedNamespace, client => Queue.Create(client, name, size, partitioning));
                            Console.WriteLine($"Queue name '{name.Value}', size '{(size.HasValue() ? size.ParsedValue : 5)}GB', partitioned '{partitioning.HasValue()}' created");
                        }
                        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessagingEntityAlreadyExists)
                        {
                            Console.WriteLine($"Queue '{name.Value}' already exists, skipping creation");
                        }
                    });
                });

                queueCommand.Command("delete", deleteCommand =>
                {
                    deleteCommand.AddOption(connectionString);
                    deleteCommand.AddOption(fullyQualifiedNamespace);

                    deleteCommand.Description = "Deletes a queue";
                    var name = deleteCommand.Argument("name", "Name of the queue (required)").IsRequired();

                    deleteCommand.OnExecuteAsync(async ct =>
                    {
                        await CommandRunner.Run(connectionString, fullyQualifiedNamespace, client => Queue.Delete(client, name));

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

            try
            {
                return app.Execute(args);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Command failed with exception ({exception.GetType().Name}): {exception.Message}");
                return 1;
            }
        }

        static ValidationResult ValidateTopicInformationIsCorrect(CommandOption topicName, CommandOption topicToPublishTo, CommandOption topicToSubscribeOn)
        {
            if (topicName.HasValue() && topicToPublishTo.HasValue())
            {
                return new ValidationResult(
                    "The --topic option and the --topic-to-publish-to option cannot be combined. Choose either a single topic name by specifying the --topic option or a hierarchy by specifying both the --topic-to-publish-to option, and --topic-to-subscribe-on option.");
            }

            if (topicName.HasValue() && topicToSubscribeOn.HasValue())
            {
                return new ValidationResult(
                    "The --topic option and the--topic-to-subscribe-on option cannot be combined. Choose either a single topic name by specifying the --topic option or a hierarchy by specifying both the --topic-to-publish-to option, and --topic-to-subscribe-on option.");
            }

            if (topicToPublishTo.HasValue() && topicToSubscribeOn.HasValue() && string.Equals(topicToPublishTo.Value(), topicToSubscribeOn.Value(), StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult(
                    "A valid topic hierarchy requires the topic-to-publish-to option and the topic-to-subscribe-on option to be different.");
            }

            return ValidationResult.Success;
        }

        static ValidationResult ValidateConnectionAndNamespaceNotUsedTogether(CommandOption connectionString,
            CommandOption fullyQualifiedNamespace)
        {
            if (connectionString.HasValue() && fullyQualifiedNamespace.HasValue())
            {
                return new ValidationResult(
                    "The connection string and the namespace option cannot be used together. Choose either the connection string or the namespace option to establish the connection");
            }

            return ValidationResult.Success;
        }
    }
}