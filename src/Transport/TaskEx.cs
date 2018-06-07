namespace NServiceBus.Transport.AzureServiceBus
{
    using System.Threading.Tasks;

    static class TaskEx
    {
        public static void Ignore(this Task task)
        {
        }
    }
}