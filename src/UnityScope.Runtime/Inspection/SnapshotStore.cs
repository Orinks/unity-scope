using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityScope.Inspection
{
    // Captures and stores hierarchy fingerprints so /diff can answer
    // "what changed since X". Snapshots are per-GameObject state digests
    // keyed by instance id. Capped LRU; older snapshots evict.
    internal class NodeState
    {
        public string Name;
        public string Path;
        public bool Active;
        public string Text;
        public string Sprite;
        public bool HasCanvasGroup;
        public float Alpha;
        public bool HasSelectable;
        public bool Interactable;
    }

    internal class Snapshot
    {
        public string Id;
        public DateTime CreatedUtc;
        public string Label;
        public Dictionary<int, NodeState> Nodes;
        public bool Truncated;
    }

    internal static class SnapshotStore
    {
        private const int MaxNodesPerSnapshot = 5000;
        private const int MaxStoredSnapshots = 10;

        private static readonly object _lock = new object();
        private static readonly LinkedList<Snapshot> _snaps = new LinkedList<Snapshot>();

        public static Snapshot Capture(string label)
        {
            var snap = new Snapshot
            {
                Id = Guid.NewGuid().ToString("N").Substring(0, 12),
                CreatedUtc = DateTime.UtcNow,
                Label = label ?? "",
                Nodes = new Dictionary<int, NodeState>(),
            };

            foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
            {
                if (c.transform.parent != null) continue;
                Walk(c.transform, snap);
                if (snap.Truncated) break;
            }

            lock (_lock)
            {
                _snaps.AddLast(snap);
                while (_snaps.Count > MaxStoredSnapshots) _snaps.RemoveFirst();
            }
            return snap;
        }

        public static Snapshot Get(string id)
        {
            lock (_lock)
            {
                foreach (var s in _snaps) if (s.Id == id) return s;
            }
            return null;
        }

        public static List<Snapshot> All()
        {
            lock (_lock) { return new List<Snapshot>(_snaps); }
        }

        private static void Walk(Transform t, Snapshot snap)
        {
            if (snap.Nodes.Count >= MaxNodesPerSnapshot) { snap.Truncated = true; return; }

            var go = t.gameObject;
            var state = new NodeState
            {
                Name = go.name,
                Path = BuildPath(t),
                Active = go.activeInHierarchy,
                Text = TextExtractor.ForGameObject(go),
            };

            var img = go.GetComponent<Image>();
            if (img != null && img.sprite != null) state.Sprite = img.sprite.name;

            var cg = go.GetComponent<CanvasGroup>();
            if (cg != null) { state.HasCanvasGroup = true; state.Alpha = cg.alpha; }

            var sel = go.GetComponent<Selectable>();
            if (sel != null) { state.HasSelectable = true; state.Interactable = sel.interactable; }

            snap.Nodes[go.GetInstanceID()] = state;

            for (int i = 0; i < t.childCount; i++)
            {
                Walk(t.GetChild(i), snap);
                if (snap.Truncated) return;
            }
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
