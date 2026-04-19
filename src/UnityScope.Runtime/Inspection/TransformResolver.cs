using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UnityScope.Inspection
{
    // Centralizes "given a string, find the matching Transform(s)" so /tree, /node,
    // /find, and /invoke all use one resolution policy. GameObject.Find is unreliable
    // for absolute paths in some scene setups (Blackout Rugby's grounds scene
    // reproduces it), so we walk transforms manually for path-style inputs.
    internal static class TransformResolver
    {
        public static Transform ByInstanceId(int id)
        {
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
                if (go.GetInstanceID() == id) return go.transform;
            return null;
        }

        public static List<Transform> AllRootCanvases()
        {
            var list = new List<Transform>();
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
                if (c.transform.parent == null) list.Add(c.transform);
            return list;
        }

        // Resolves any of: empty (all canvases), instance id, "/Canvas/Foo/Bar"
        // absolute path, or bare name match. Returns 0 or 1 result for path/id/name,
        // multiple for the empty (canvases) case.
        public static List<Transform> Resolve(string input)
        {
            var results = new List<Transform>();

            if (string.IsNullOrEmpty(input))
                return AllRootCanvases();

            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            {
                var t = ByInstanceId(id);
                if (t != null) results.Add(t);
                return results;
            }

            if (input.StartsWith("/"))
            {
                var t = ResolveAbsolutePath(input);
                if (t != null) results.Add(t);
                return results;
            }

            // Bare name — first matching active GameObject in any scene root.
            var found = GameObject.Find(input);
            if (found != null) results.Add(found.transform);
            return results;
        }

        // "/Canvas/Safe Area/Continue" — brute-force scan every Transform, compare
        // its computed full path. Slower than a segment-walk but bulletproof: works
        // across additive scene loads (where multiple GameObjects can share a name
        // at different roots) and inactive branches. Returns the first match if
        // multiple share the same path; instance IDs are the right addressing for
        // unambiguous targeting.
        private static Transform ResolveAbsolutePath(string path)
        {
            // Iterate GameObjects, not Transforms: FindObjectsOfTypeAll<Transform>()
            // is exact-type and silently excludes every UI element (they have
            // RectTransform). GameObjects cover both worlds via go.transform.
            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (go == null) continue;
                if (go.scene.rootCount == 0) continue;   // skip prefab/asset GOs
                if (BuildPath(go.transform) == path) return go.transform;
            }
            return null;
        }

        private static string BuildPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.gameObject.name); t = t.parent; }
            parts.Reverse();
            return "/" + string.Join("/", parts);
        }
    }
}
