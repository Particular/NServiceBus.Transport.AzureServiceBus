namespace NServiceBus.Transport.AzureServiceBus.Tests.Receiving;

using System;
using System.Collections.Generic;
using NServiceBus.Transport.AzureServiceBus;
using NUnit.Framework;

public class DeadLetterRequestTests
{
    [Test]
    public void Should_full_control_over_dead_letter_parameters()
    {
        var reason = "reason";
        var description = "description";
        var properties = new Dictionary<string, object> { { "SomeProperty", "SomeValue" } };
        var request = new DeadLetterRequest(reason, description, properties);

        Assert.AreEqual(reason, request.DeadLetterReason, "DeadLetterReason should be set correctly");
        Assert.AreEqual(description, request.DeadLetterErrorDescription, "DeadLetterErrorDescription should be set correctly");
        Assert.IsNotNull(request.PropertiesToModify, "PropertiesToModify should not be null");
        Assert.IsTrue(request.PropertiesToModify!.ContainsKey("SomeProperty"), "PropertiesToModify should contain 'SomeProperty'");
        Assert.AreEqual("SomeValue", request.PropertiesToModify["SomeProperty"], "PropertiesToModify['SomeProperty'] should be set correctly");
    }

    [Test]
    public void Should_convert_exception_to_dead_letter_request()
    {
        var exception = SimulateException();
        var request = new DeadLetterRequest(exception);

        // Make sure we follow microsoft guidance - https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-dead-letter-queues#application-level-dead-lettering
        Assert.AreEqual("System.InvalidOperationException - Test exception", request.DeadLetterReason, "DeadLetterReason should reflect exception type and message");
        Assert.AreEqual(request.DeadLetterErrorDescription, exception.StackTrace, "DeadLetterErrorDescription should contain stack trace");
        return;

        Exception SimulateException()
        {
            try
            {
                throw new InvalidOperationException("Test exception");
            }
            catch (Exception e)
            {
                return e;
            }
        }
    }

    [Test]
    public void Should_truncate_dead_letter_reason_and_description_to_1024_characters()
    {
        var longReason = new string('A', 2000);
        var longDescription = new string('B', 3000);
        var request = new DeadLetterRequest(longReason, longDescription);

        Assert.AreEqual(new string('A', 1024), request.DeadLetterReason, "DeadLetterReason should match the first 1024 characters of the input");
        Assert.AreEqual(new string('B', 1024), request.DeadLetterErrorDescription, "DeadLetterErrorDescription should match the first 1024 characters of the input");
    }
}