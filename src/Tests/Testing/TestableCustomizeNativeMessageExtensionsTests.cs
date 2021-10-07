namespace NServiceBus.Transport.AzureServiceBus.Tests.Testing
{
    using System.Linq;
    using System.Threading.Tasks;
    using Azure.Messaging.ServiceBus;
    using NServiceBus.Testing;
    using NUnit.Framework;

    [TestFixture]
    public class TestableCustomizeNativeMessageExtensionsTests
    {
        [Test]
        public async Task GetNativeMessageCustomization_when_session_used_should_return_customization()
        {
            var testableMessageSession = new TestableMessageSession();

            var options = new PublishOptions();
            options.CustomizeNativeMessage(m => m.Subject = "abc");
            await testableMessageSession.Publish(new MyMessage(), options);

            var publishedMessage = testableMessageSession.PublishedMessages.Single();
            var customization = publishedMessage.Options.GetNativeMessageCustomization();

            var nativeMessage = new ServiceBusMessage();
            customization(nativeMessage);

            Assert.AreEqual("abc", nativeMessage.Subject);
        }

        [Test]
        public async Task GetNativeMessageCustomization_when_session_used_and_no_customization_should_return_customization()
        {
            var testableMessageSession = new TestableMessageSession();

            var options = new PublishOptions();
            await testableMessageSession.Publish(new MyMessage(), options);

            var publishedMessage = testableMessageSession.PublishedMessages.Single();
            var customization = publishedMessage.Options.GetNativeMessageCustomization();

            Assert.IsNull(customization);
        }

        [Test]
        public async Task GetNativeMessageCustomization_when_handler_used_should_return_customization()
        {
            var testableContext = new TestableMessageHandlerContext();

            var handler = new MyHandlerUsingCustomizations();

            await handler.Handle(new MyMessage(), testableContext);

            var publishedMessage = testableContext.PublishedMessages.Single();
            var customization = publishedMessage.Options.GetNativeMessageCustomization(testableContext);

            var nativeMessage = new ServiceBusMessage();
            customization(nativeMessage);

            Assert.AreEqual("abc", nativeMessage.Subject);
        }

        [Test]
        public async Task GetNativeMessageCustomization_when_handler_used_and_no_customization_should_return_null()
        {
            var testableContext = new TestableMessageHandlerContext();

            var handler = new MyHandlerWithoutCustomization();

            await handler.Handle(new MyMessage(), testableContext);

            var publishedMessage = testableContext.PublishedMessages.Single();
            var customization = publishedMessage.Options.GetNativeMessageCustomization(testableContext);

            Assert.IsNull(customization);
        }

        class MyHandlerUsingCustomizations : IHandleMessages<MyMessage>
        {
            public async Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                var options = new PublishOptions();
                options.CustomizeNativeMessage(context, m => m.Subject = "abc");
                await context.Publish(message, options);
            }
        }

        class MyHandlerWithoutCustomization : IHandleMessages<MyMessage>
        {
            public async Task Handle(MyMessage message, IMessageHandlerContext context)
            {
                var options = new PublishOptions();
                await context.Publish(message, options);
            }
        }

        class MyMessage
        {
        }
    }


}