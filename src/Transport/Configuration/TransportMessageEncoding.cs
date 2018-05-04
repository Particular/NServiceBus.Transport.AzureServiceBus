namespace NServiceBus.Transport.AzureServiceBus.Configuration
{
    /// <summary>
    /// Physical message encoding.
    /// </summary>
    public enum TransportMessageEncoding
    {
        /// <summary>Serialized byte array.</summary>
        ByteArray,
        /// <summary>Stream of bytes.</summary>
        Stream
    }
}