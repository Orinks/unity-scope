using System.Collections.Specialized;
using UnityScope.Inspection;
using UnityScope.Json;
using UnityScope.Server;

namespace UnityScope.Endpoints
{
    // GET  /text-extractors            List currently registered rules + auto-detect status.
    // POST /text-extractors?type=<sub>&member=<name>&kind=property|method[&persist=true]
    //   Register a rule at runtime. persist=true (default) writes it to
    //   text-extractors.txt next to the plugin DLL so it survives restarts.
    internal static class TextExtractorsEndpoint
    {
        public static Response List()
        {
            var w = new JsonWriter().BeginObject()
                .Field("auto_detect", TextExtractor.AutoDetectEnabled)
                .Field("rule_count", TextExtractor.RuleCount)
                .Field("persistence_file", RulePersistence.FilePath)
                .Key("rules").BeginArray();

            foreach (var (typeSub, member, kind) in TextExtractor.ListRules())
            {
                w.BeginObject()
                    .Field("type", typeSub)
                    .Field("kind", kind == TextExtractor.MemberKind.Method ? "method" : "property")
                    .Field("member", member)
                .EndObject();
            }
            return Response.Ok(w.EndArray().EndObject().ToString());
        }

        public static Response Register(NameValueCollection q)
        {
            string typeSub = q?["type"];
            string member  = q?["member"];
            string kindStr = (q?["kind"] ?? "property").ToLowerInvariant();
            bool persist   = (q?["persist"] ?? "true").ToLowerInvariant() != "false";

            if (string.IsNullOrEmpty(typeSub) || string.IsNullOrEmpty(member))
                return Response.BadRequest("provide 'type' (substring of component type name) and 'member' (property/method name)");

            TextExtractor.MemberKind kind;
            switch (kindStr)
            {
                case "property": kind = TextExtractor.MemberKind.Property; break;
                case "method":   kind = TextExtractor.MemberKind.Method;   break;
                default: return Response.BadRequest("'kind' must be 'property' or 'method'");
            }

            TextExtractor.Register(typeSub, member, kind);
            if (persist) RulePersistence.Append(typeSub, member, kind);

            return Response.Ok(new JsonWriter().BeginObject()
                .Field("ok", true)
                .Field("type", typeSub)
                .Field("kind", kindStr)
                .Field("member", member)
                .Field("persisted", persist)
                .Field("total_rules", TextExtractor.RuleCount)
            .EndObject().ToString());
        }
    }
}
