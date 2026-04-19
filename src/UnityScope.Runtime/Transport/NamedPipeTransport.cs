using System;
using System.Diagnostics;
using UnityScope.Server;

namespace UnityScope.Transport
{
    // Stub. Wire format identical to HttpTransport (line-delimited JSON request/response).
    // Implement when a target environment actually blocks loopback HTTP.
    internal class NamedPipeTransport : ITransport
    {
        private readonly RequestRouter _router;
        private string _pipeName;

        public string EndpointDescription => $"\\\\.\\pipe\\{_pipeName} (named pipe, NOT YET IMPLEMENTED)";

        public NamedPipeTransport(RequestRouter router) { _router = router; }

        public void Start()
        {
            _pipeName = $"unity-scope-{Process.GetCurrentProcess().Id}";
            DiscoveryFile.Write("pipe", $"\\\\.\\pipe\\{_pipeName}", Guid.NewGuid().ToString("N"));
            throw new NotImplementedException(
                "NamedPipeTransport is scaffolded but not implemented. Set Transport=http in BepInEx config until then.");
        }

        public void Stop()
        {
            DiscoveryFile.Delete();
        }
    }
}
