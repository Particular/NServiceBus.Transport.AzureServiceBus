namespace NServiceBus.Transport.AzureServiceBus.AcceptanceTests;

using System;
using System.IO.Hashing;
using System.Text;

public static class AcceptanceTestExtensions
{
    public static string ToTopicName(this Type eventType) =>
        eventType.FullName.Replace("+", ".").Shorten(maxLength: 260);

    // The idea here is to preserve part of the text and append a non-cryptographic hash to it.
    // This way, we can have a deterministic and unique names without harming much the readability.
    // The chance of collisions should be very low but definitely not zero. We can always switch to
    // using more bits in the hash or even back to a cryptographic hash if needed.
    public static string Shorten(this string name, int maxLength = 50)
    {
        if (name.Length <= maxLength)
        {
            return name;
        }

        var nameBytes = Encoding.UTF8.GetBytes(name);
        var hashValue = XxHash32.Hash(nameBytes);
        string hashHex = Convert.ToHexString(hashValue);

        int prefixLength = maxLength - hashHex.Length;

        if (prefixLength < 0)
        {
            return hashHex.Length > maxLength
                ? hashHex[..maxLength] // in case even the hash is too long
                : hashHex;
        }

        string prefix = name[..Math.Min(prefixLength, name.Length)];
        return $"{prefix}{hashHex}";
    }
}