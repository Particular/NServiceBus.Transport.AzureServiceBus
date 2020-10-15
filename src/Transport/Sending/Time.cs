namespace NServiceBus.Transport.AzureServiceBus
{
    using System;

    static class Time
    {
        public static Func<DateTimeOffset> UtcNow = () => DateTimeOffset.UtcNow;
    }
}