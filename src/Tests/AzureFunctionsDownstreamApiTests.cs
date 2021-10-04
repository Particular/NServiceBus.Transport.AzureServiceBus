namespace NServiceBus.Transport.AzureServiceBus.Tests
{
    using NUnit.Framework;

    // These tests exist to ensure that the API conforms to the expectations of the Azure Functions package
    // Before merging changes to this API, check them against NServiceBus.AzureFunctions.InProcess.ServiceBus
    [TestFixture]
    class AzureFunctionsDownstreamApiTests
    {
        //[Test] TODO
        //public void Should_have_a_parameterless_constructor()
        //{
        //    var constructor = typeof(AzureServiceBusTransport)
        //        .GetConstructor(
        //            BindingFlags.NonPublic | BindingFlags.Instance,
        //            null,
        //            Array.Empty<Type>(),
        //            null);
        //    Assert.NotNull(constructor, "Azure Functions package expects a non-public parameterless constructor");
        //    Assert.True(IsProtected(constructor), "Azure functions package expects parameterless constructor to be protected");
        //}

        //[Test]
        //public void Should_have_a_settable_connection_string_property()
        //{
        //    var property = typeof(AzureServiceBusTransport)
        //        .GetProperty(
        //            "ConnectionString",
        //            BindingFlags.NonPublic | BindingFlags.Instance
        //        );
        //    Assert.NotNull(property, "Azure Functions package expects a non-public connection string property");
        //    Assert.True(property.CanWrite, "Azure Functions package expects to be able to write to connection string property");
        //    Assert.True(IsProtected(property.SetMethod), "Azure Functions package expects connection string property setter to be protected");
        //}

        //static bool IsProtected(MethodBase method)
        //    => method.IsFamily
        //       || method.IsFamilyOrAssembly
        //       || method.IsFamilyAndAssembly;
    }
}
