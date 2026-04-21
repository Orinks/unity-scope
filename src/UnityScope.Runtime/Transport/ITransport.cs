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
        public static ITransport Create(string kind, RequestRouter router, int httpPort = HttpTransport.DefaultPort)
        {
            switch ((kind ?? "http").ToLowerInvariant())
            {
                case "pipe":
                    return new NamedPipeTransport(router);
                case "http":
                default:
                    return new HttpTransport(router, httpPort);
            }
        }
    }
}
