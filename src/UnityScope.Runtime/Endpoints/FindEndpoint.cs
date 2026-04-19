using System.Collections.Specialized;
using System.Globalization;
using UnityEngine;
using UnityScope.Inspection;
using UnityScope.Json;
using UnityScope.Server;

namespace UnityScope.Endpoints
{
    // GET /find?selector=<sel>&max=<N>
    //   selector ::= name-glob ([attr] [attr]...)
    //   examples: *Continue*  |  *[type=BlackoutButton][active]  |  *[text*=Tutorial]
    // Returns matching GameObjects with id, path, active, and first-text snippet.
    internal static class FindEndpoint
    {
        private const int DefaultMax = 50;
        private const int HardCap = 500;

        public static Response Handle(NameValueCollection q)
        {
            string raw = q?["selector"];
            if (string.IsNullOrEmpty(raw)) return Response.BadRequest("provide 'selector'");

            int max = ParseInt(q?["max"], DefaultMax);
            if (max > HardCap) max = HardCap;

            var sel = Selector.Parse(raw);
            int matched = 0, returned = 0;

            var w = new JsonWriter().BeginObject()
                .Key("query").BeginObject()
                    .Field("selector", raw)
                    .Field("max", max)
                .EndObject()
                .Key("results").BeginArray();

            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null || go.scene.rootCount == 0) continue; // skip prefab assets, only scene objects
                if (!sel.Matches(go)) continue;
                matched++;
                if (returned >= max) continue;

                var text = TextExtractor.ForGameObject(go);
                w.BeginObject()
                    .Field("instance_id", go.GetInstanceID())
                    .Field("path", BuildPath(go.transform))
                    .Field("name", go.name)
                    .Field("active", go.activeInHierarchy);
                if (text != null) w.Field("text", Truncate(text, 120));
                w.EndObject();
                returned++;
            }

            return Response.Ok(w.EndArray()
                .Field("matched", matched)
                .Field("returned", returned)
                .Field("truncated", matched > returned)
                .EndObject().ToString());
        }

        private static string BuildPath(Transform t)
        {
            var parts = new System.Collections.Generic.List<string>();
            while (t != null) { parts.Add(t.gameObject.name); t = t.parent; }
            parts.Reverse();
            return "/" + string.Join("/", parts);
        }

        private static string Truncate(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "...";

        private static int ParseInt(string s, int def)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : def;
    }
}
