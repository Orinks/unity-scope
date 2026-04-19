using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using UnityEngine;
using UnityScope.Inspection;
using UnityScope.Json;
using UnityScope.Server;

namespace UnityScope.Endpoints
{
    // GET /tree?root=<path|instanceId>&depth=N&max=M
    // Walks a partial hierarchy from the requested root(s) and returns it as JSON.
    // Defaults are conservative — agents OOM on full dumps, so always paginate
    // either by depth or by max node count.
    internal static class TreeEndpoint
    {
        private const int DefaultDepth = 3;
        private const int DefaultMaxNodes = 500;

        public static Response Handle(NameValueCollection q)
        {
            string root = q?["root"];
            int depth = ParseInt(q?["depth"], DefaultDepth);
            int maxNodes = ParseInt(q?["max"], DefaultMaxNodes);

            var roots = ResolveRoots(root);
            var ctx = new WalkContext { Depth = depth, MaxNodes = maxNodes };

            var w = new JsonWriter().BeginObject()
                .Key("query").BeginObject()
                    .Field("root", root ?? "")
                    .Field("depth", depth)
                    .Field("max", maxNodes)
                    .Field("resolved_root_count", roots.Count)
                .EndObject()
                .Key("nodes").BeginArray();

            foreach (var t in roots) Walk(w, t, 0, ctx);

            return Response.Ok(w.EndArray()
                .Field("count", ctx.Count)
                .Field("truncated", ctx.Truncated)
                .EndObject().ToString());
        }

        private static List<Transform> ResolveRoots(string root)
        {
            var list = new List<Transform>();

            if (string.IsNullOrEmpty(root))
            {
                foreach (var c in Object.FindObjectsOfType<Canvas>())
                    if (c.transform.parent == null) list.Add(c.transform);
                return list;
            }

            if (int.TryParse(root, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            {
                foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                    if (go.GetInstanceID() == id) { list.Add(go.transform); break; }
                return list;
            }

            var found = GameObject.Find(root);
            if (found != null) list.Add(found.transform);
            return list;
        }

        private static void Walk(JsonWriter w, Transform t, int currentDepth, WalkContext ctx)
        {
            if (ctx.Count >= ctx.MaxNodes) { ctx.Truncated = true; return; }
            ctx.Count++;

            var go = t.gameObject;
            var rt = t as RectTransform;

            w.BeginObject()
                .Field("name", go.name)
                .Field("instance_id", go.GetInstanceID())
                .Field("path", GetPath(t))
                .Field("active", go.activeInHierarchy);

            if (rt != null)
            {
                Vector3 pos = ScreenPosition(rt);
                w.Key("rect").BeginObject()
                    .Field("x", pos.x)
                    .Field("y", pos.y)
                    .Field("width", rt.rect.width)
                    .Field("height", rt.rect.height)
                .EndObject();
            }

            w.Key("components").BeginArray();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (IsBoringComponent(c)) continue;
                ComponentSerializer.WriteSummary(w, c);
            }
            w.EndArray();

            if (currentDepth < ctx.Depth && t.childCount > 0)
            {
                w.Key("children").BeginArray();
                for (int i = 0; i < t.childCount; i++)
                {
                    var child = t.GetChild(i);
                    // Mirror UIDumpHandler heuristic: skip deep inactive branches.
                    if (!child.gameObject.activeInHierarchy && currentDepth >= 2) continue;
                    Walk(w, child, currentDepth + 1, ctx);
                }
                w.EndArray();
            }
            else if (t.childCount > 0)
            {
                w.Field("child_count", t.childCount);
                w.Field("children_truncated", true);
            }

            w.EndObject();
        }

        private static bool IsBoringComponent(Component c)
        {
            string n = c.GetType().FullName;
            return n == "UnityEngine.RectTransform"
                || n == "UnityEngine.Transform"
                || n == "UnityEngine.CanvasRenderer";
        }

        private static Vector3 ScreenPosition(RectTransform rt)
        {
            var canvas = rt.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return rt.position;
            if (Camera.main != null)
                return Camera.main.WorldToScreenPoint(rt.position);
            return rt.position;
        }

        internal static string GetPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.gameObject.name); t = t.parent; }
            parts.Reverse();
            return "/" + string.Join("/", parts);
        }

        private static int ParseInt(string s, int def)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : def;

        private class WalkContext
        {
            public int Depth;
            public int MaxNodes;
            public int Count;
            public bool Truncated;
        }
    }
}
