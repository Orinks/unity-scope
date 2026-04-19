using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UnityScope.Json
{
    // Minimal JSON writer. Avoids a Newtonsoft dependency that can clash with the host
    // game's bundled version. Spike-phase only — replace with a real serializer if/when
    // we need round-tripping or schema validation.
    internal class JsonWriter
    {
        private readonly StringBuilder _sb = new StringBuilder(512);
        private readonly Stack<bool> _firstChild = new Stack<bool>();

        public JsonWriter BeginObject() { Sep(); _sb.Append('{'); _firstChild.Push(true); return this; }
        public JsonWriter EndObject()   { _sb.Append('}'); _firstChild.Pop(); return this; }
        public JsonWriter BeginArray()  { Sep(); _sb.Append('['); _firstChild.Push(true); return this; }
        public JsonWriter EndArray()    { _sb.Append(']'); _firstChild.Pop(); return this; }

        public JsonWriter Key(string name) { Sep(); WriteString(name); _sb.Append(':'); _firstChild.Push(true); return this; }

        public JsonWriter Field(string name, string value) { Key(name); WriteString(value); _firstChild.Pop(); return this; }
        public JsonWriter Field(string name, int value)    { Key(name); _sb.Append(value.ToString(CultureInfo.InvariantCulture)); _firstChild.Pop(); return this; }
        public JsonWriter Field(string name, bool value)   { Key(name); _sb.Append(value ? "true" : "false"); _firstChild.Pop(); return this; }
        public JsonWriter Field(string name, float value)  { Key(name); _sb.Append(value.ToString("R", CultureInfo.InvariantCulture)); _firstChild.Pop(); return this; }

        private void Sep()
        {
            if (_firstChild.Count == 0) return;
            if (_firstChild.Peek()) { _firstChild.Pop(); _firstChild.Push(false); }
            else _sb.Append(',');
        }

        private void WriteString(string s)
        {
            if (s == null) { _sb.Append("null"); return; }
            _sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) _sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }

        public override string ToString() => _sb.ToString();
    }
}
