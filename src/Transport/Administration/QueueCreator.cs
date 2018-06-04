namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading.Tasks;

    class QueueCreator : ICreateQueues
    {
        public Task CreateQueueIfNecessary(QueueBindings queueBindings, string identity)
        {
            throw new System.NotImplementedException();
        }
    }
}