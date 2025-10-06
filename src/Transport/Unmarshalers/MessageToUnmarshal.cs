namespace NServiceBus.Transport.AzureServiceBus.Unmarshalers;

using System;
using System.Collections.Generic;

class MessageToUnmarshal
{
    public MessageToUnmarshal(Dictionary<string, string?> headers, ReadOnlyMemory<byte> body)
    {
        Headers = headers;
        Body = body;
    }

    public Dictionary<string, string?> Headers { get; }
    public ReadOnlyMemory<byte> Body { get; }
}