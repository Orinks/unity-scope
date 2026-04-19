using UnityEngine;
using UnityEngine.SceneManagement;
using UnityScope.Json;
using UnityScope.Server;

namespace UnityScope.Endpoints
{
    // Cheap orientation. Returns active scene name + top-level canvas summary.
    // Full hierarchy walk lives in /tree (TreeEndpoint, not yet implemented).
    internal static class SceneEndpoint
    {
        public static Response Handle()
        {
            var scene = SceneManager.GetActiveScene();
            var canvases = Object.FindObjectsOfType<Canvas>();

            var w = new JsonWriter()
                .BeginObject()
                .Field("scene", scene.name)
                .Field("scene_path", scene.path)
                .Field("is_loaded", scene.isLoaded)
                .Key("root_canvases").BeginArray();

            int count = 0;
            foreach (var c in canvases)
            {
                if (c.transform.parent != null) continue;
                count++;
                w.BeginObject()
                    .Field("name", c.gameObject.name)
                    .Field("instance_id", c.GetInstanceID())
                    .Field("active", c.gameObject.activeInHierarchy)
                    .Field("render_mode", c.renderMode.ToString())
                    .Field("sort_order", c.sortingOrder)
                    .Field("child_count", c.transform.childCount)
                    .EndObject();
            }

            return Response.Ok(w.EndArray().Field("canvas_count", count).EndObject().ToString());
        }
    }
}
