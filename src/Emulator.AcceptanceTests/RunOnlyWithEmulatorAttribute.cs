namespace NServiceBus.Transport.AzureServiceBus.Emulator.AcceptanceTests;

using System;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly)]
public sealed class RunOnlyWithEmulatorAttribute : Attribute, IApplyToContext
{
    public void ApplyToContext(TestExecutionContext context)
    {
        var connectionString = Environment.GetEnvironmentVariable("AzureServiceBus_Emulator_ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            Assert.Ignore("No emulator connection string found. Set the AzureServiceBus_Emulator_ConnectionString environment variable to run these tests.");
        }
        else
        {
            context.CurrentTest.Properties.Set("AzureServiceBus_Emulator_ConnectionString", connectionString);
        }
    }
}