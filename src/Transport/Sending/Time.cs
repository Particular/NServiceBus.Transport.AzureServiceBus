namespace NServiceBus.Transport.AzureServiceBus
{
    using System;

    static class Time
    {
        public static Func<DateTime> UtcNow = () => DateTime.UtcNow;
    }
}