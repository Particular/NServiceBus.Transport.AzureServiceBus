namespace NServiceBus.Transport.AzureServiceBus
{
    using Features;

    class TransactionScopeSuppressFeature : Feature
    {
        public TransactionScopeSuppressFeature()
        {
            EnableByDefault();
        }

        protected override void Setup(FeatureConfigurationContext context)
        {
            context.Pipeline.Register(new TransactionScopeSuppressBehavior.Registration());
        }
    }
}