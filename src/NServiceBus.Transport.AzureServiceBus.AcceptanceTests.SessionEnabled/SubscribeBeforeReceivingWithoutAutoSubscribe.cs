using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Features;

// Without auto-subscribe, the mapped subscriptions must exist when the endpoint starts, as if a deployment created them.
// The shared tests subscribe after the endpoint started, so this subscribes before the receivers start. It acts like the deployment,
// so endpoints without installers, which run like a production deployment, get nothing created.
public sealed class SubscribeBeforeReceivingWithoutAutoSubscribe : Feature
{
    public SubscribeBeforeReceivingWithoutAutoSubscribe() => DependsOnOptionally<AutoSubscribe>();

    public static void Enable(EndpointConfiguration configuration, Type[] eventTypes)
    {
        configuration.GetSettings().Set(SettingsKey, eventTypes);
        configuration.EnableFeature<SubscribeBeforeReceivingWithoutAutoSubscribe>();
    }

    protected override void Setup(FeatureConfigurationContext context)
    {
        var eventTypes = context.Settings.Get<Type[]>(SettingsKey);
        if (eventTypes.Length == 0 || context.Settings.IsFeatureActive<AutoSubscribe>() || !context.Settings.GetOrDefault<bool>("Installers.Enable"))
        {
            return;
        }

        context.RegisterStartupTask(new SubscribeTask(eventTypes));
    }

    sealed class SubscribeTask(Type[] eventTypes) : FeatureStartupTask
    {
        protected override async Task OnStart(IMessageSession session, CancellationToken cancellationToken = default)
        {
            foreach (var eventType in eventTypes)
            {
                await session.Subscribe(eventType, new SubscribeOptions(), cancellationToken);
            }
        }

        protected override Task OnStop(IMessageSession session, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    const string SettingsKey = "AzureServiceBus.AcceptanceTests.EventTypesToSubscribeBeforeReceiving";
}
