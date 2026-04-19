using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using UnityScope.Inspection;
using UnityScope.Json;
using UnityScope.Server;

namespace UnityScope.Endpoints
{
    // GET /diff?since=<id>&max=<N>
    // Captures a fresh snapshot, compares it against the stored 'since' snapshot,
    // returns added/removed/modified node lists with per-field before/after values.
    // The killer feature for agentic exploration: lets an agent observe what an
    // action *did* to the UI rather than guessing from a flat dump.
    internal static class DiffEndpoint
    {
        private const int DefaultMax = 200;

        public static Response Handle(NameValueCollection q)
        {
            string since = q?["since"];
            if (string.IsNullOrEmpty(since))
                return Response.BadRequest("provide 'since' (snapshot id)");

            var prior = SnapshotStore.Get(since);
            if (prior == null) return Response.NotFound("snapshot");

            int max = ParseInt(q?["max"], DefaultMax);

            var now = SnapshotStore.Capture(null);
            return Response.Ok(SerializeDiff(prior, now, max));
        }

        private static string SerializeDiff(Snapshot prior, Snapshot now, int max)
        {
            var added = new List<KeyValuePair<int, NodeState>>();
            var removed = new List<KeyValuePair<int, NodeState>>();
            var modified = new List<ModifiedEntry>();

            foreach (var kv in now.Nodes)
            {
                if (!prior.Nodes.TryGetValue(kv.Key, out var before))
                {
                    added.Add(kv);
                }
                else
                {
                    var changes = CompareStates(before, kv.Value);
                    if (changes.Count > 0) modified.Add(new ModifiedEntry { Id = kv.Key, After = kv.Value, Changes = changes });
                }
            }

            foreach (var kv in prior.Nodes)
                if (!now.Nodes.ContainsKey(kv.Key)) removed.Add(kv);

            var w = new JsonWriter().BeginObject();

            WriteSnapshotMeta(w, "since", prior);
            WriteSnapshotMeta(w, "now", now);

            w.Key("summary").BeginObject()
                .Field("added", added.Count)
                .Field("removed", removed.Count)
                .Field("modified", modified.Count)
                .Field("max", max)
            .EndObject();

            bool truncated = false;

            w.Key("added").BeginArray();
            for (int i = 0; i < added.Count && i < max; i++)
            {
                var a = added[i];
                w.BeginObject()
                    .Field("instance_id", a.Key)
                    .Field("path", a.Value.Path)
                    .Field("name", a.Value.Name)
                    .Field("active", a.Value.Active);
                if (a.Value.Text != null) w.Field("text", Truncate(a.Value.Text, 120));
                w.EndObject();
            }
            if (added.Count > max) truncated = true;
            w.EndArray();

            w.Key("removed").BeginArray();
            for (int i = 0; i < removed.Count && i < max; i++)
            {
                var r = removed[i];
                w.BeginObject()
                    .Field("instance_id", r.Key)
                    .Field("path", r.Value.Path)
                    .Field("name", r.Value.Name)
                .EndObject();
            }
            if (removed.Count > max) truncated = true;
            w.EndArray();

            w.Key("modified").BeginArray();
            for (int i = 0; i < modified.Count && i < max; i++)
            {
                var m = modified[i];
                w.BeginObject()
                    .Field("instance_id", m.Id)
                    .Field("path", m.After.Path)
                    .Field("name", m.After.Name)
                    .Key("changes").BeginObject();
                foreach (var c in m.Changes)
                {
                    w.Key(c.Key).BeginArray().Value(c.Value.before).Value(c.Value.after).EndArray();
                }
                w.EndObject().EndObject();
            }
            if (modified.Count > max) truncated = true;
            w.EndArray();

            w.Field("truncated", truncated);

            return w.EndObject().ToString();
        }

        private static Dictionary<string, (string before, string after)> CompareStates(NodeState a, NodeState b)
        {
            var d = new Dictionary<string, (string, string)>();
            if (a.Active != b.Active) d["active"] = (a.Active.ToString(), b.Active.ToString());
            if (!StringEq(a.Text, b.Text)) d["text"] = (Truncate(a.Text, 120), Truncate(b.Text, 120));
            if (!StringEq(a.Sprite, b.Sprite)) d["sprite"] = (a.Sprite, b.Sprite);
            if (a.HasCanvasGroup && b.HasCanvasGroup && Math.Abs(a.Alpha - b.Alpha) > 0.001f)
                d["alpha"] = (a.Alpha.ToString("F2", CultureInfo.InvariantCulture), b.Alpha.ToString("F2", CultureInfo.InvariantCulture));
            if (a.HasSelectable && b.HasSelectable && a.Interactable != b.Interactable)
                d["interactable"] = (a.Interactable.ToString(), b.Interactable.ToString());
            if (!StringEq(a.Path, b.Path)) d["path"] = (a.Path, b.Path);
            return d;
        }

        private static bool StringEq(string a, string b) => string.Equals(a ?? "", b ?? "", StringComparison.Ordinal);

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        private static void WriteSnapshotMeta(JsonWriter w, string key, Snapshot s)
        {
            w.Key(key).BeginObject()
                .Field("id", s.Id)
                .Field("created_utc", s.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                .Field("node_count", s.Nodes.Count)
                .Field("truncated", s.Truncated)
            .EndObject();
        }

        private static int ParseInt(string s, int def)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : def;

        private class ModifiedEntry
        {
            public int Id;
            public NodeState After;
            public Dictionary<string, (string before, string after)> Changes;
        }
    }
}
