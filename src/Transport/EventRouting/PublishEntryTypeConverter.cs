namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.ComponentModel;
using System.Globalization;

sealed class PublishEntryTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string topic)
        {
            return new PublishEntry(topic);
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is PublishEntry entry)
        {
            return entry.Topic;
        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}