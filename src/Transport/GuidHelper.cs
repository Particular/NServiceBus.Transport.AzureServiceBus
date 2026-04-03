namespace NServiceBus.Transport.AzureServiceBus;

using System;
using System.Buffers.Binary;

static class GuidHelper
{
    public static Guid CreateVersion8(DateTimeOffset timestamp, long sequenceNumber)
    {
        Span<byte> guidBytes = stackalloc byte[16];
        BinaryPrimitives.WriteInt64BigEndian(guidBytes, timestamp.ToUnixTimeMilliseconds());
        BinaryPrimitives.WriteInt64BigEndian(guidBytes[8..], sequenceNumber);
        guidBytes[6] = (byte)(0x80 | (guidBytes[6] & 0xF));
        guidBytes[8] = (byte)(0x80 | (guidBytes[8] & 0x3F));
        return new Guid(guidBytes, bigEndian: true);
    }
}
