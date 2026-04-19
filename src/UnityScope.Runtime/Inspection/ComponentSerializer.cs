using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityScope.Json;

namespace UnityScope.Inspection
{
    // Writes one JSON object per Unity Component. Two modes:
    //   WriteSummary — short form for tree walks (type + extracted user-facing props).
    //   WriteDetail  — full form for /node (adds reflected public fields/properties).
    //
    // Game-specific extractors (BlackoutText/BlackoutButton/...) live here for the
    // spike. Will move to a TypeHandlerRegistry once a second game proves the API.
    internal static class ComponentSerializer
    {
        public static void WriteSummary(JsonWriter w, Component comp)
        {
            var type = comp.GetType();
            w.BeginObject().Field("type", type.FullName);

            var text = TextExtractor.FromComponent(comp);
            if (text != null) w.Field("text", Truncate(text, 200));

            switch (comp)
            {
                case Toggle t:
                    w.Field("is_on", t.isOn).Field("interactable", t.interactable);
                    break;
                case Selectable s:
                    w.Field("interactable", s.interactable);
                    break;
            }

            switch (comp)
            {
                case CanvasGroup cg:
                    w.Field("alpha", cg.alpha)
                     .Field("blocks_raycasts", cg.blocksRaycasts)
                     .Field("interactable", cg.interactable);
                    break;
                case Image img:
                    if (img.sprite != null) w.Field("sprite", img.sprite.name);
                    break;
                case ScrollRect sr:
                    w.Field("scroll_horizontal", sr.horizontal)
                     .Field("scroll_vertical", sr.vertical);
                    break;
            }

            w.EndObject();
        }

        public static void WriteDetail(JsonWriter w, Component comp)
        {
            var type = comp.GetType();
            w.BeginObject().Field("type", type.FullName);

            var text = TextExtractor.FromComponent(comp);
            if (text != null) w.Field("text", Truncate(text, 500));

            w.Key("fields").BeginObject();
            try
            {
                foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (!p.CanRead || p.GetIndexParameters().Length > 0) continue;
                    if (IsNoisyMember(p.Name)) continue;
                    TryWriteValue(w, p.Name, () => p.GetValue(comp, null));
                }
                foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (IsNoisyMember(f.Name)) continue;
                    TryWriteValue(w, f.Name, () => f.GetValue(comp));
                }
            }
            catch { }
            w.EndObject();

            w.EndObject();
        }

        private static void TryWriteValue(JsonWriter w, string name, Func<object> getter)
        {
            object v;
            try { v = getter(); } catch { return; }
            if (v == null) { w.Key(name).ValueNull(); return; }

            switch (v)
            {
                case string s:  w.Field(name, Truncate(s, 200)); return;
                case bool b:    w.Field(name, b); return;
                case int i:     w.Field(name, i); return;
                case float f:   w.Field(name, f); return;
                case Vector2 v2: w.Key(name).BeginObject().Field("x", v2.x).Field("y", v2.y).EndObject(); return;
                case Vector3 v3: w.Key(name).BeginObject().Field("x", v3.x).Field("y", v3.y).Field("z", v3.z).EndObject(); return;
                case Color c:    w.Key(name).BeginObject().Field("r", c.r).Field("g", c.g).Field("b", c.b).Field("a", c.a).EndObject(); return;
                case Enum e:    w.Field(name, e.ToString()); return;
                case UnityEngine.Object uo:
                    w.Key(name).BeginObject()
                        .Field("ref", uo.GetType().FullName)
                        .Field("name", uo.name)
                        .Field("instance_id", uo.GetInstanceID())
                    .EndObject();
                    return;
            }

            // Numeric primitives we didn't catch above
            try { w.Field(name, Convert.ToDouble(v) is double d ? (float)d : 0f); }
            catch { w.Field(name, v.ToString()); }
        }

        private static bool IsNoisyMember(string name)
        {
            // Skip Unity boilerplate that bloats the output and rarely helps an agent.
            switch (name)
            {
                case "gameObject":
                case "transform":
                case "rectTransform":
                case "hideFlags":
                case "tag":
                case "name":
                case "enabled":
                case "isActiveAndEnabled":
                case "useGUILayout":
                case "runInEditMode":
                case "allowPrefabModeInPlayMode":
                case "destroyCancellationToken":
                    return true;
            }
            return false;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return s;
            s = s.Replace("\r", "");
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
