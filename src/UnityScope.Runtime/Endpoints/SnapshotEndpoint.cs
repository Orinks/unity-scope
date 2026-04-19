using System.Collections.Specialized;
using UnityScope.Inspection;
using UnityScope.Json;
using UnityScope.Server;

namespace UnityScope.Endpoints
{
    // GET /snapshot?label=<label>           Take a snapshot of the current scene.
    // GET /snapshot/list                    List stored snapshots.
    internal static class SnapshotEndpoint
    {
        public static Response Capture(NameValueCollection q)
        {
            var snap = SnapshotStore.Capture(q?["label"]);
            return Response.Ok(new JsonWriter().BeginObject()
                .Field("id", snap.Id)
                .Field("label", snap.Label)
                .Field("created_utc", snap.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                .Field("node_count", snap.Nodes.Count)
                .Field("truncated", snap.Truncated)
            .EndObject().ToString());
        }

        public static Response List()
        {
            var w = new JsonWriter().BeginObject().Key("snapshots").BeginArray();
            foreach (var s in SnapshotStore.All())
            {
                w.BeginObject()
                    .Field("id", s.Id)
                    .Field("label", s.Label)
                    .Field("created_utc", s.CreatedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"))
                    .Field("node_count", s.Nodes.Count)
                    .Field("truncated", s.Truncated)
                .EndObject();
            }
            return Response.Ok(w.EndArray().EndObject().ToString());
        }
    }
}
