namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;

sealed class SubscriptionEntryHashSetTypeConverter : TypeConverter
{
    static readonly TypeConverter EntryConverter = TypeDescriptor.GetConverter(typeof(SubscriptionEntry));

    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string topic)
        {
            var entry = (SubscriptionEntry)EntryConverter.ConvertFromString(topic)!;
            return new HashSet<SubscriptionEntry> { entry };
        }

        return base.ConvertFrom(context, culture, value);
    }

    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    {
        if (destinationType == typeof(string) && value is HashSet<SubscriptionEntry> set)
        {
            if (set.Count != 1)
            {
                throw new NotSupportedException("Only single-element sets can be converted to a string.");
            }

            using var enumerator = set.GetEnumerator();
            enumerator.MoveNext();
            return EntryConverter.ConvertToString(enumerator.Current);

        }

        return base.ConvertTo(context, culture, value, destinationType);
    }
}
