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

        // "/Canvas/Safe Area/Continue" — walk segment-by-segment from any matching root.
        // Handles inactive ancestors (GameObject.Find can't), and tolerates spaces.
        private static Transform ResolveAbsolutePath(string path)
        {
            var segments = path.Substring(1).Split('/');
            if (segments.Length == 0) return null;

            foreach (var root in AllRootCanvases())
            {
                if (root.gameObject.name != segments[0]) continue;
                var t = root;
                bool ok = true;
                for (int i = 1; i < segments.Length; i++)
                {
                    var next = FindChildByName(t, segments[i]);
                    if (next == null) { ok = false; break; }
                    t = next;
                }
                if (ok) return t;
            }

            // Not a canvas root — try ALL root GameObjects in active scene.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name != segments[0]) continue;
                var t = go.transform;
                bool ok = true;
                for (int i = 1; i < segments.Length; i++)
                {
                    var next = FindChildByName(t, segments[i]);
                    if (next == null) { ok = false; break; }
                    t = next;
                }
                if (ok) return t;
            }
            return null;
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.gameObject.name == name) return child;
            }
            return null;
        }
    }
}
