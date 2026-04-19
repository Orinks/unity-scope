using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityScope.Server;
using UnityScope.Transport;

namespace UnityScope
{
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; }

        private ITransport _transport;
        private RequestRouter _router;
        private MainThreadDispatcher _dispatcher;

        private ConfigEntry<string> _transportKind;
        private ConfigEntry<bool> _allowInvoke;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} Awake()");

            _transportKind = Config.Bind("UnityScope", "Transport", "http",
                "Transport for the agent API. 'http' (loopback, default) or 'pipe' (named pipe fallback).");
            _allowInvoke = Config.Bind("UnityScope", "AllowInvoke", false,
                "If true, POST /invoke can call methods and set fields on live components. Off by default.");

            _dispatcher = gameObject.AddComponent<MainThreadDispatcher>();
            _router = new RequestRouter(_dispatcher, _allowInvoke.Value);

            try
            {
                _transport = TransportFactory.Create(_transportKind.Value, _router);
                _transport.Start();
                Logger.LogInfo($"UnityScope listening: {_transport.EndpointDescription}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Transport startup failed: {ex}");
            }
        }

        private void OnDestroy()
        {
            try { _transport?.Stop(); }
            catch (Exception ex) { Logger.LogWarning($"Transport stop error: {ex.Message}"); }
        }
    }

    internal static class PluginInfo
    {
        public const string GUID = "com.orinks.unityscope";
        public const string Name = "UnityScope";
        public const string Version = "0.1.0";
    }
}
