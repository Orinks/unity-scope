using UnityScope.Server;

namespace UnityScope.Transport
{
    internal interface ITransport
    {
        string EndpointDescription { get; }
        void Start();
        void Stop();
    }

    internal static class TransportFactory
    {
        public static ITransport Create(string kind, RequestRouter router)
        {
            switch ((kind ?? "http").ToLowerInvariant())
            {
                case "pipe":
                    return new NamedPipeTransport(router);
                case "http":
                default:
                    return new HttpTransport(router);
            }
        }
    }
}
