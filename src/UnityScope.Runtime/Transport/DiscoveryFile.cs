using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityScope.Json;

namespace UnityScope.Transport
{
    internal static class DiscoveryFile
    {
        public static string Directory
        {
            get
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(baseDir, "UnityScope");
            }
        }

        public static string PathFor(int pid)
        {
            string proc = Process.GetCurrentProcess().ProcessName;
            return Path.Combine(Directory, $"{Sanitize(proc)}_{pid}.json");
        }

        public static void Write(string transport, string endpoint, string authToken)
        {
            System.IO.Directory.CreateDirectory(Directory);
            PruneStale();

            int pid = Process.GetCurrentProcess().Id;
            string proc = Process.GetCurrentProcess().ProcessName;
            string startedUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

            var json = new JsonWriter()
                .BeginObject()
                .Field("version", PluginInfoLite.Version)
                .Field("transport", transport)
                .Field("endpoint", endpoint)
                .Field("auth_token", authToken)
                .Field("process", proc)
                .Field("pid", pid)
                .Field("started_utc", startedUtc)
                .EndObject()
                .ToString();

            File.WriteAllText(PathFor(pid), json, new UTF8Encoding(false));
        }

        public static void Delete()
        {
            try
            {
                string p = PathFor(Process.GetCurrentProcess().Id);
                if (File.Exists(p)) File.Delete(p);
            }
            catch { }
        }

        private static void PruneStale()
        {
            try
            {
                foreach (var path in System.IO.Directory.GetFiles(Directory, "*.json"))
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    int underscore = name.LastIndexOf('_');
                    if (underscore < 0) continue;
                    if (!int.TryParse(name.Substring(underscore + 1), out int pid)) continue;
                    try { Process.GetProcessById(pid); }
                    catch (ArgumentException) { File.Delete(path); }
                }
            }
            catch { }
        }

        private static string Sanitize(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '.' ? c : '_');
            return sb.ToString();
        }
    }

    internal static class PluginInfoLite
    {
        public const string Version = "0.1.0";
    }
}
