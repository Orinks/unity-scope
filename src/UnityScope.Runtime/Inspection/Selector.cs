using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace UnityScope.Inspection
{
    // Tiny CSS-ish selector for /find. Grammar:
    //   selector  := namePattern? attrFilter*
    //   namePattern := <chars>  (use '*' as a 0-or-more wildcard; "*" matches anything)
    //   attrFilter := '[' name ('=' | '*=' | '^=') value? ']'
    //                 e.g. [active], [!active], [interactable], [type=BlackoutButton],
    //                      [type*=Button], [text*=Continue]
    internal class Selector
    {
        public string NamePattern;
        public List<AttrFilter> Filters = new List<AttrFilter>();

        public static Selector Parse(string s)
        {
            var sel = new Selector();
            if (string.IsNullOrEmpty(s)) { sel.NamePattern = "*"; return sel; }

            int i = 0;
            var name = new StringBuilder();
            while (i < s.Length && s[i] != '[') { name.Append(s[i]); i++; }
            sel.NamePattern = name.Length == 0 ? "*" : name.ToString();

            while (i < s.Length)
            {
                if (s[i] != '[') { i++; continue; }
                int end = s.IndexOf(']', i);
                if (end < 0) break;
                sel.Filters.Add(AttrFilter.Parse(s.Substring(i + 1, end - i - 1)));
                i = end + 1;
            }
            return sel;
        }

        public bool Matches(GameObject go)
        {
            if (!Wildcard(go.name, NamePattern)) return false;
            foreach (var f in Filters) if (!f.Matches(go)) return false;
            return true;
        }

        // Minimal '*'-only glob. No regex, no escaping; '*' = 0+ any chars.
        public static bool Wildcard(string text, string pattern)
        {
            if (pattern == null || pattern == "*") return true;
            int t = 0, p = 0, starIdx = -1, matchIdx = 0;
            while (t < text.Length)
            {
                if (p < pattern.Length && pattern[p] == '*')
                {
                    starIdx = p; matchIdx = t; p++;
                }
                else if (p < pattern.Length && pattern[p] == text[t])
                {
                    p++; t++;
                }
                else if (starIdx != -1)
                {
                    p = starIdx + 1; matchIdx++; t = matchIdx;
                }
                else return false;
            }
            while (p < pattern.Length && pattern[p] == '*') p++;
            return p == pattern.Length;
        }
    }

    internal class AttrFilter
    {
        public string Name;
        public string Op;         // null (presence), "=", "*=", "^="
        public string Value;
        public bool Negate;

        public static AttrFilter Parse(string body)
        {
            var f = new AttrFilter();
            if (body.StartsWith("!")) { f.Negate = true; body = body.Substring(1); }

            int eq = body.IndexOf('=');
            if (eq < 0) { f.Name = body; return f; }

            int opStart = eq;
            if (opStart > 0 && (body[opStart - 1] == '*' || body[opStart - 1] == '^')) opStart--;

            f.Name = body.Substring(0, opStart);
            f.Op = body.Substring(opStart, eq - opStart + 1);
            f.Value = body.Substring(eq + 1);
            return f;
        }

        public bool Matches(GameObject go)
        {
            bool result;
            switch (Name)
            {
                case "active":
                    result = go.activeInHierarchy;
                    break;
                case "interactable":
                    var sel = go.GetComponent<Selectable>();
                    result = sel != null && sel.interactable;
                    break;
                case "type":
                    result = MatchAnyComponentType(go);
                    break;
                case "text":
                    result = MatchText(go);
                    break;
                default:
                    result = false;
                    break;
            }
            return Negate ? !result : result;
        }

        private bool MatchAnyComponentType(GameObject go)
        {
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (Compare(c.GetType().FullName ?? "", Value)) return true;
            }
            return false;
        }

        private bool MatchText(GameObject go)
        {
            var text = TextExtractor.ForGameObject(go);
            if (text == null) return false;
            return Compare(text, Value);
        }

        private bool Compare(string actual, string expected)
        {
            if (Op == null) return true;                                     // presence-only filter
            switch (Op)
            {
                case "=":  return actual == expected;
                case "*=": return actual.IndexOf(expected ?? "", System.StringComparison.Ordinal) >= 0;
                case "^=": return actual.StartsWith(expected ?? "", System.StringComparison.Ordinal);
                default:   return false;
            }
        }
    }
}
