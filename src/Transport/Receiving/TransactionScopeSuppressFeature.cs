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
            context.Container.ConfigureComponent(b => new TransactionScopeSuppressBehavior(), DependencyLifecycle.InstancePerCall);

            context.Pipeline.Register(new TransactionScopeSuppressBehavior.Registration());
        }
    }
}