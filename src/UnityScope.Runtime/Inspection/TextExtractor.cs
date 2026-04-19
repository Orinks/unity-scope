using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace UnityScope.Inspection
{
    // Pulls user-facing text out of any text-bearing component. Three layers:
    //
    //   1. Built-in:    UnityEngine.UI.Text, TMPro.TextMeshPro* (universal Unity types)
    //   2. Registered:  rules supplied by the agent via /text-extractors, or by
    //                   the user via cfg / text-extractors.txt
    //   3. Conventions: zero-config auto-detection. Tries common property names
    //                   (text/Text/Label/Caption/DisplayText) and no-arg method
    //                   names (GetText/GetLabel/GetCaption/GetDisplayText) on any
    //                   unknown component type. Per-type accessor is cached on
    //                   first hit (positive or negative) so reflection runs once.
    //
    // Result: most games work out of the box. Custom types only need explicit
    // rules when they don't follow naming conventions — and even then, the agent
    // can register the rule itself; no human needs to know the type names.
    internal static class TextExtractor
    {
        private static readonly List<Rule> _customRules = new List<Rule>();
        private static readonly Dictionary<Type, MemberAccessor> _conventionCache = new Dictionary<Type, MemberAccessor>();

        private static readonly string[] ConventionalProperties = { "text", "Text", "Label", "Caption", "DisplayText" };
        private static readonly string[] ConventionalMethods    = { "GetText", "GetLabel", "GetCaption", "GetDisplayText" };

        public static bool AutoDetectEnabled { get; set; } = true;

        public static void Register(string typeNameSubstring, string memberName, MemberKind kind)
        {
            _customRules.Add(new Rule { TypeNameSubstring = typeNameSubstring, MemberName = memberName, Kind = kind });
            _conventionCache.Clear(); // explicit rules take precedence; refresh cache
        }

        public static void ClearRules()
        {
            _customRules.Clear();
            _conventionCache.Clear();
        }

        public static IEnumerable<(string TypeSubstring, string Member, MemberKind Kind)> ListRules()
        {
            foreach (var r in _customRules) yield return (r.TypeNameSubstring, r.MemberName, r.Kind);
        }

        public static int RuleCount => _customRules.Count;

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
            // Layer 1: built-in.
            if (comp is Text uiText) return uiText.text;

            var type = comp.GetType();
            string fullName = type.FullName ?? "";

            if (fullName.StartsWith("TMPro.TextMeshPro", StringComparison.Ordinal))
            {
                var t = ReadStringProperty(comp, type, "text");
                if (t != null) return t;
            }

            // Layer 2: explicit rules.
            foreach (var rule in _customRules)
            {
                if (fullName.IndexOf(rule.TypeNameSubstring, StringComparison.Ordinal) < 0) continue;
                string text = rule.Kind == MemberKind.Method
                    ? ReadStringMethod(comp, type, rule.MemberName)
                    : ReadStringProperty(comp, type, rule.MemberName);
                if (text != null) return text;
            }

            // Layer 3: conventions.
            if (!AutoDetectEnabled) return null;
            return TryConventions(comp, type);
        }

        private static string TryConventions(Component comp, Type type)
        {
            if (_conventionCache.TryGetValue(type, out var cached))
                return cached?.Get(comp);

            foreach (var name in ConventionalProperties)
            {
                var accessor = TryProperty(type, name);
                if (accessor != null) { _conventionCache[type] = accessor; return accessor.Get(comp); }
            }
            foreach (var name in ConventionalMethods)
            {
                var accessor = TryMethod(type, name);
                if (accessor != null) { _conventionCache[type] = accessor; return accessor.Get(comp); }
            }

            _conventionCache[type] = null; // negative cache
            return null;
        }

        private static MemberAccessor TryProperty(Type type, string name)
        {
            for (Type ty = type; ty != null && ty != typeof(Component); ty = ty.BaseType)
            {
                var p = ty.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (p != null && p.CanRead && p.PropertyType == typeof(string))
                    return new MemberAccessor { Property = p };
            }
            return null;
        }

        private static MemberAccessor TryMethod(Type type, string name)
        {
            for (Type ty = type; ty != null && ty != typeof(Component); ty = ty.BaseType)
            {
                var m = ty.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (m != null && m.ReturnType == typeof(string))
                    return new MemberAccessor { Method = m };
            }
            return null;
        }

        private static string ReadStringProperty(Component c, Type t, string name)
        {
            for (Type ty = t; ty != null; ty = ty.BaseType)
            {
                var p = ty.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (p == null) continue;
                try { return p.GetValue(c, null) as string; } catch { return null; }
            }
            return null;
        }

        private static string ReadStringMethod(Component c, Type t, string name)
        {
            var m = t.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (m == null) return null;
            try { return m.Invoke(c, null) as string; } catch { return null; }
        }

        public enum MemberKind { Property, Method }

        private struct Rule
        {
            public string TypeNameSubstring;
            public string MemberName;
            public MemberKind Kind;
        }

        private class MemberAccessor
        {
            public PropertyInfo Property;
            public MethodInfo Method;
            public string Get(Component c)
            {
                try
                {
                    if (Property != null) return Property.GetValue(c, null) as string;
                    if (Method != null) return Method.Invoke(c, null) as string;
                }
                catch { }
                return null;
            }
        }
    }
}
