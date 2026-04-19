using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace UnityScope.Inspection
{
    // Persists agent-registered text-extractor rules so they survive game restarts.
    // Format: one rule per line, "Type:property|method:Member". Same grammar as
    // the BepInEx cfg string. Plain text so humans can grep / version-control /
    // share rule packs across modders working on the same game.
    internal static class RulePersistence
    {
        public static string FilePath
        {
            get
            {
                // Sit alongside the plugin DLL.
                var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(asmDir ?? ".", "text-extractors.txt");
            }
        }

        public static int LoadInto(Action<string, string, TextExtractor.MemberKind> register)
        {
            var path = FilePath;
            if (!File.Exists(path)) return 0;

            int loaded = 0;
            foreach (var raw in File.ReadAllLines(path))
            {
                if (TryParse(raw, out var t, out var m, out var k))
                {
                    register(t, m, k);
                    loaded++;
                }
            }
            return loaded;
        }

        public static void Append(string typeSub, string memberName, TextExtractor.MemberKind kind)
        {
            var line = $"{typeSub}:{(kind == TextExtractor.MemberKind.Method ? "method" : "property")}:{memberName}";
            // Avoid duplicating an identical line.
            if (File.Exists(FilePath))
            {
                foreach (var existing in File.ReadAllLines(FilePath))
                    if (string.Equals(existing.Trim(), line, StringComparison.Ordinal)) return;
            }
            File.AppendAllText(FilePath, line + Environment.NewLine, new UTF8Encoding(false));
        }

        public static bool TryParse(string raw, out string typeSub, out string member, out TextExtractor.MemberKind kind)
        {
            typeSub = null; member = null; kind = TextExtractor.MemberKind.Property;
            if (string.IsNullOrWhiteSpace(raw)) return false;
            var line = raw.Trim();
            if (line.StartsWith("#")) return false; // comments

            var parts = line.Split(':');
            if (parts.Length != 3) return false;

            switch (parts[1].Trim().ToLowerInvariant())
            {
                case "property": kind = TextExtractor.MemberKind.Property; break;
                case "method":   kind = TextExtractor.MemberKind.Method;   break;
                default: return false;
            }
            typeSub = parts[0].Trim();
            member = parts[2].Trim();
            if (typeSub.Length == 0 || member.Length == 0) return false;
            return true;
        }
    }
}
