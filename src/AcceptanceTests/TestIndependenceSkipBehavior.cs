﻿namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NUnit.Framework;
    using Pipeline;

    class TestIndependenceSkipBehavior : IBehavior<ITransportReceiveContext, ITransportReceiveContext>
    {
        readonly string testRunId;

        public TestIndependenceSkipBehavior(ScenarioContext scenarioContext)
        {
            testRunId = scenarioContext.TestRunId.ToString();
        }

        public Task Invoke(ITransportReceiveContext context, Func<ITransportReceiveContext, Task> next)
        {
            if (context.Message.Headers.TryGetValue("$AcceptanceTesting.TestRunId", out var runId) && runId != testRunId)
            {
                TestContext.Out.WriteLine($"Skipping message {context.Message.MessageId} from previous test run");
                return Task.CompletedTask;
            }

            return next(context);
        }
    }
}