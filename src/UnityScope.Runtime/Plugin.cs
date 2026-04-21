using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;
using UnityScope.Inspection;
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
        private ConfigEntry<int> _httpPort;
        private ConfigEntry<bool> _allowInvoke;
        private ConfigEntry<string> _textExtractors;
        private ConfigEntry<bool> _autoDetectText;

        private void Awake()
        {
            Log = Logger;
            Logger.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} Awake()");

            _transportKind = Config.Bind("UnityScope", "Transport", "http",
                "Transport for the agent API. 'http' (loopback, default) or 'pipe' (named pipe fallback).");
            _httpPort = Config.Bind("UnityScope", "HttpPort", HttpTransport.DefaultPort,
                "Preferred loopback port for the http transport. Stable across restarts so MCP clients don't "
                + "have to re-resolve discovery on every launch. If the port is already in use (e.g. a second "
                + "game instance), an OS-assigned free port is used instead.");
            _allowInvoke = Config.Bind("UnityScope", "AllowInvoke", false,
                "If true, POST /invoke can call methods and set fields on live components. Off by default.");
            _textExtractors = Config.Bind("UnityScope", "TextExtractors", "",
                "Optional explicit text-extractor rules. Most games work without this thanks to "
                + "convention-based auto-detection (see AutoDetectText). Format: comma-separated "
                + "TypeNameSubstring:property:Name or TypeNameSubstring:method:Name. Agents typically "
                + "register rules at runtime via POST /text-extractors instead of editing this string.");
            _autoDetectText = Config.Bind("UnityScope", "AutoDetectText", true,
                "Try common property/method names (text, Text, Label, GetText, GetLabel, ...) on any "
                + "unknown component type. Caches per type. Disable only if it produces false positives.");

            TextExtractor.AutoDetectEnabled = _autoDetectText.Value;
            LoadTextExtractorRules(_textExtractors.Value);
            int persisted = RulePersistence.LoadInto(TextExtractor.Register);
            if (persisted > 0) Logger.LogInfo($"TextExtractors: loaded {persisted} persisted rule(s) from {RulePersistence.FilePath}");

            _dispatcher = gameObject.AddComponent<MainThreadDispatcher>();
            _router = new RequestRouter(_dispatcher, _allowInvoke.Value);

            try
            {
                _transport = TransportFactory.Create(_transportKind.Value, _router, _httpPort.Value);
                _transport.Start();
                Logger.LogInfo($"UnityScope listening: {_transport.EndpointDescription}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Transport startup failed: {ex}");
            }
        }

        private void LoadTextExtractorRules(string raw)
        {
            TextExtractor.ClearRules();
            if (string.IsNullOrWhiteSpace(raw)) return;

            int loaded = 0, skipped = 0;
            foreach (var entry in raw.Split(','))
            {
                var trimmed = entry.Trim();
                if (trimmed.Length == 0) continue;

                var parts = trimmed.Split(':');
                if (parts.Length != 3) { skipped++; Logger.LogWarning($"TextExtractors: malformed rule '{trimmed}', expected Type:property|method:Member"); continue; }

                TextExtractor.MemberKind kind;
                switch (parts[1].Trim().ToLowerInvariant())
                {
                    case "property": kind = TextExtractor.MemberKind.Property; break;
                    case "method":   kind = TextExtractor.MemberKind.Method;   break;
                    default: skipped++; Logger.LogWarning($"TextExtractors: unknown kind '{parts[1]}' in '{trimmed}'"); continue;
                }

                TextExtractor.Register(parts[0].Trim(), parts[2].Trim(), kind);
                loaded++;
            }
            if (loaded > 0 || skipped > 0)
                Logger.LogInfo($"TextExtractors: loaded {loaded} rule(s), skipped {skipped}");
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
