using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace UnityScope.Inspection
{
    // Pulls user-facing text out of any text-bearing component. Game-specific
    // extractors (Blackout*) live here for the spike; will move to a registry
    // once a second game's adapters prove the API.
    internal static class TextExtractor
    {
        // Walk every component on a GameObject, return the first non-empty text.
        public static string ForGameObject(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                var text = FromComponent(c);
                if (!string.IsNullOrEmpty(text)) return text;
            }
            return null;
        }

        public static string FromComponent(Component comp)
        {
            if (comp is Text uiText) return uiText.text;

            var type = comp.GetType();
            string fullName = type.FullName ?? "";

            if (fullName.Contains("TMPro.TextMeshPro"))
                return ReflectStringProperty(comp, type, "text");

            if (fullName.Contains("BlackoutText"))
                return ReflectStringProperty(comp, type, "text");

            if (fullName.Contains("BlackoutButton"))
                return ReflectStringMethod(comp, type, "GetText");

            if (fullName.Contains("BlackoutDropdown"))
                return ReflectStringMethod(comp, type, "GetLabel");

            if (fullName.Contains("BInputField"))
            {
                Type t = type;
                while (t != null)
                {
                    var p = t.GetProperty("text", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                    if (p != null) { try { return p.GetValue(comp, null) as string; } catch { } }
                    t = t.BaseType;
                }
            }
            return null;
        }

        private static string ReflectStringProperty(Component c, Type t, string prop)
        {
            var p = t.GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            if (p == null) return null;
            try { return p.GetValue(c, null) as string; } catch { return null; }
        }

        private static string ReflectStringMethod(Component c, Type t, string method)
        {
            var m = t.GetMethod(method, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (m == null) return null;
            try { return m.Invoke(c, null) as string; } catch { return null; }
        }
    }
}
