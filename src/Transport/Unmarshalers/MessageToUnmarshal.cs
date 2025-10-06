namespace NServiceBus.Transport.AzureServiceBus.Unmarshalers;

using System;
using System.Collections.Generic;

public class MessageToUnmarshal
{
    public string NativeMessageId { get; set; }
    public Dictionary<string, string?> Headers { get; set; }
    public ReadOnlyMemory<byte> Body { get; set; }
}