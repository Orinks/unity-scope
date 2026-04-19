using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;
using UnityScope.Inspection;
using UnityScope.Json;
using UnityScope.Server;

namespace UnityScope.Endpoints
{
    // POST /invoke?target=<id|path>&member=<name>[&component=<typeNameSubstring>]
    //              [&action=call|set|get][&arg0=...&arg1=...][&value=...]
    //
    // V1 scope: primitive args (string, bool, int, float, enum) only. No generics,
    // no ref/out, no Object args. Gated behind UnityScope:AllowInvoke = true in
    // BepInEx config (router enforces). Every successful invocation is logged.
    internal static class InvokeEndpoint
    {
        public static Response Handle(NameValueCollection q)
        {
            string target = q?["target"];
            string member = q?["member"];
            if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(member))
                return Response.BadRequest("provide 'target' (id|path) and 'member' (name)");

            var hits = TransformResolver.Resolve(target);
            if (hits.Count == 0) return Response.NotFound("target");
            var go = hits[0].gameObject;

            string compHint = q?["component"];
            string action = (q?["action"] ?? "").ToLowerInvariant();
            var args = CollectArgs(q);
            string value = q?["value"];

            // Pick action when the caller didn't say:
            //   args present       → call
            //   value present only → set
            //   neither            → call (no-arg method) preferred over get
            if (string.IsNullOrEmpty(action))
                action = (args.Count > 0) ? "call" : (value != null ? "set" : "call");

            try
            {
                switch (action)
                {
                    case "call": return DoCall(go, compHint, member, args);
                    case "set":  return DoSet(go, compHint, member, value);
                    case "get":  return DoGet(go, compHint, member);
                    default:     return Response.BadRequest($"unknown action '{action}' (call|set|get)");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log?.LogWarning($"/invoke failed: target={target} member={member}: {ex.Message}");
                return new Response
                {
                    Status = 500,
                    Body = new JsonWriter().BeginObject()
                        .Field("ok", false)
                        .Field("error", ex.Message)
                        .Field("error_type", ex.GetType().FullName)
                    .EndObject().ToString()
                };
            }
        }

        private static Response DoCall(GameObject go, string compHint, string member, List<string> args)
        {
            foreach (var c in CandidateComponents(go, compHint))
            {
                var m = FindMethod(c.GetType(), member, args.Count);
                if (m == null) continue;

                object[] coerced = CoerceArgs(m.GetParameters(), args);
                object result = m.Invoke(c, coerced);
                Plugin.Log?.LogInfo($"/invoke call {c.GetType().FullName}.{member}({args.Count} args) on instance {go.GetInstanceID()}");

                return Response.Ok(new JsonWriter().BeginObject()
                    .Field("ok", true)
                    .Field("kind", "method")
                    .Field("target_id", go.GetInstanceID())
                    .Field("component", c.GetType().FullName)
                    .Field("member", m.Name)
                    .Field("returned", FormatResult(result, m.ReturnType))
                .EndObject().ToString());
            }
            return MemberNotFound(go, compHint, member, "method");
        }

        private static Response DoSet(GameObject go, string compHint, string member, string value)
        {
            foreach (var c in CandidateComponents(go, compHint))
            {
                var t = c.GetType();
                var prop = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanWrite)
                {
                    var coerced = Coerce(value, prop.PropertyType);
                    prop.SetValue(c, coerced, null);
                    Plugin.Log?.LogInfo($"/invoke set {t.FullName}.{member} = {value} on instance {go.GetInstanceID()}");
                    return SetGetOk(go, c, "property", member, FormatResult(coerced, prop.PropertyType));
                }

                var field = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    var coerced = Coerce(value, field.FieldType);
                    field.SetValue(c, coerced);
                    Plugin.Log?.LogInfo($"/invoke set {t.FullName}.{member} = {value} on instance {go.GetInstanceID()}");
                    return SetGetOk(go, c, "field", member, FormatResult(coerced, field.FieldType));
                }
            }
            return MemberNotFound(go, compHint, member, "writable property/field");
        }

        private static Response DoGet(GameObject go, string compHint, string member)
        {
            foreach (var c in CandidateComponents(go, compHint))
            {
                var t = c.GetType();
                var prop = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.CanRead)
                    return SetGetOk(go, c, "property", member, FormatResult(prop.GetValue(c, null), prop.PropertyType));

                var field = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                    return SetGetOk(go, c, "field", member, FormatResult(field.GetValue(c), field.FieldType));
            }
            return MemberNotFound(go, compHint, member, "readable property/field");
        }

        private static Response SetGetOk(GameObject go, Component c, string kind, string member, string value)
            => Response.Ok(new JsonWriter().BeginObject()
                .Field("ok", true)
                .Field("kind", kind)
                .Field("target_id", go.GetInstanceID())
                .Field("component", c.GetType().FullName)
                .Field("member", member)
                .Field("value", value)
            .EndObject().ToString());

        private static Response MemberNotFound(GameObject go, string compHint, string member, string what)
        {
            var w = new JsonWriter().BeginObject()
                .Field("ok", false)
                .Field("error", $"no {what} '{member}' on any component" + (compHint != null ? $" matching '{compHint}'" : ""))
                .Field("target_id", go.GetInstanceID())
                .Key("components").BeginArray();
            foreach (var c in CandidateComponents(go, compHint))
                w.Value(c.GetType().FullName);
            return new Response { Status = 404, Body = w.EndArray().EndObject().ToString() };
        }

        private static IEnumerable<Component> CandidateComponents(GameObject go, string hint)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (string.IsNullOrEmpty(hint)) { yield return c; continue; }
                var n = c.GetType().FullName ?? "";
                if (n == hint || n.IndexOf(hint, StringComparison.Ordinal) >= 0) yield return c;
            }
        }

        private static MethodInfo FindMethod(Type t, string name, int argCount)
        {
            // Public instance, then non-public — many Unity event handlers are private.
            foreach (var bf in new[] {
                BindingFlags.Public | BindingFlags.Instance,
                BindingFlags.NonPublic | BindingFlags.Instance })
            {
                foreach (var m in t.GetMethods(bf))
                {
                    if (m.Name != name) continue;
                    if (m.IsGenericMethodDefinition) continue;
                    if (m.GetParameters().Length != argCount) continue;
                    return m;
                }
            }
            return null;
        }

        private static List<string> CollectArgs(NameValueCollection q)
        {
            var list = new List<string>();
            for (int i = 0; ; i++)
            {
                var v = q?["arg" + i];
                if (v == null) break;
                list.Add(v);
            }
            return list;
        }

        private static object[] CoerceArgs(ParameterInfo[] parameters, List<string> raw)
        {
            var result = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                result[i] = Coerce(raw[i], parameters[i].ParameterType);
            return result;
        }

        private static object Coerce(string raw, Type t)
        {
            if (t == typeof(string)) return raw;
            if (t == typeof(bool))   return bool.Parse(raw);
            if (t == typeof(int))    return int.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (t == typeof(long))   return long.Parse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture);
            if (t == typeof(float))  return float.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (t == typeof(double)) return double.Parse(raw, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (t.IsEnum)            return Enum.Parse(t, raw, ignoreCase: true);
            throw new InvalidOperationException($"unsupported arg type {t.FullName}");
        }

        private static string FormatResult(object v, Type t)
        {
            if (t == typeof(void)) return "void";
            if (v == null) return "null";
            if (v is UnityEngine.Object uo)
                return $"{uo.GetType().FullName}#{uo.GetInstanceID()}({uo.name})";
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }
    }
}
