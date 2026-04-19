using System.Collections.Specialized;
using System.Globalization;
using UnityEngine;
using UnityScope.Inspection;
using UnityScope.Json;
using UnityScope.Server;

namespace UnityScope.Endpoints
{
    // GET /node?id=<instance_id>   OR   GET /node?path=/Canvas/Foo/Bar
    // Returns a single GameObject's full component dump including reflected
    // public fields and properties. Heavy — use sparingly after /tree drill-down.
    internal static class NodeEndpoint
    {
        public static Response Handle(NameValueCollection q)
        {
            string idRaw = q?["id"];
            string path = q?["path"];

            GameObject go = null;

            if (!string.IsNullOrEmpty(idRaw)
                && int.TryParse(idRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
            {
                foreach (var candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (candidate.GetInstanceID() == id) { go = candidate; break; }
                }
            }
            else if (!string.IsNullOrEmpty(path))
            {
                go = GameObject.Find(path);
            }
            else
            {
                return Response.BadRequest("provide 'id' (instance id) or 'path' (hierarchy path).");
            }

            if (go == null) return Response.NotFound("gameobject");

            var t = go.transform;
            var rt = t as RectTransform;

            var w = new JsonWriter().BeginObject()
                .Field("name", go.name)
                .Field("instance_id", go.GetInstanceID())
                .Field("path", TreeEndpoint.GetPath(t))
                .Field("active", go.activeInHierarchy)
                .Field("active_self", go.activeSelf)
                .Field("layer", go.layer)
                .Field("tag", go.tag);

            if (rt != null)
            {
                w.Key("rect").BeginObject()
                    .Field("anchored_x", rt.anchoredPosition.x)
                    .Field("anchored_y", rt.anchoredPosition.y)
                    .Field("width", rt.rect.width)
                    .Field("height", rt.rect.height)
                    .Field("pivot_x", rt.pivot.x)
                    .Field("pivot_y", rt.pivot.y)
                .EndObject();
            }

            w.Key("components").BeginArray();
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                ComponentSerializer.WriteDetail(w, c);
            }
            w.EndArray();

            w.Key("children").BeginArray();
            for (int i = 0; i < t.childCount; i++)
            {
                var child = t.GetChild(i);
                w.BeginObject()
                    .Field("name", child.gameObject.name)
                    .Field("instance_id", child.gameObject.GetInstanceID())
                    .Field("active", child.gameObject.activeInHierarchy)
                .EndObject();
            }
            w.EndArray();

            return Response.Ok(w.EndObject().ToString());
        }
    }
}
