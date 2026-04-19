using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UnityScope.Json
{
    // Streaming JSON writer with proper container-state tracking. Avoids a Newtonsoft
    // dependency that can clash with the host game's bundled version.
    internal class JsonWriter
    {
        private readonly StringBuilder _sb = new StringBuilder(1024);
        private readonly Stack<Frame> _stack = new Stack<Frame>();

        public JsonWriter() { _stack.Push(new Frame(FrameKind.Root)); }

        public JsonWriter BeginObject() { BeforeValue(); _sb.Append('{'); _stack.Push(new Frame(FrameKind.Object)); return this; }
        public JsonWriter EndObject()   { Pop(FrameKind.Object); _sb.Append('}'); return this; }
        public JsonWriter BeginArray()  { BeforeValue(); _sb.Append('['); _stack.Push(new Frame(FrameKind.Array));  return this; }
        public JsonWriter EndArray()    { Pop(FrameKind.Array);  _sb.Append(']'); return this; }

        public JsonWriter Key(string name)
        {
            var f = _stack.Peek();
            if (f.Kind != FrameKind.Object) throw new InvalidOperationException("Key only valid inside object.");
            if (!f.First) _sb.Append(',');
            f.First = false;
            WriteString(name);
            _sb.Append(':');
            f.ExpectValue = true;
            return this;
        }

        public JsonWriter Value(string s) { BeforeValue(); WriteString(s); return this; }
        public JsonWriter Value(int i)    { BeforeValue(); _sb.Append(i.ToString(CultureInfo.InvariantCulture)); return this; }
        public JsonWriter Value(bool b)   { BeforeValue(); _sb.Append(b ? "true" : "false"); return this; }
        public JsonWriter Value(float f)
        {
            BeforeValue();
            bool finite = !float.IsNaN(f) && !float.IsInfinity(f);
            _sb.Append(finite ? f.ToString("R", CultureInfo.InvariantCulture) : "null");
            return this;
        }
        public JsonWriter ValueNull()     { BeforeValue(); _sb.Append("null"); return this; }

        public JsonWriter Field(string name, string value) => Key(name).Value(value);
        public JsonWriter Field(string name, int value)    => Key(name).Value(value);
        public JsonWriter Field(string name, bool value)   => Key(name).Value(value);
        public JsonWriter Field(string name, float value)  => Key(name).Value(value);

        public override string ToString() => _sb.ToString();

        private void BeforeValue()
        {
            var f = _stack.Peek();
            if (f.Kind == FrameKind.Object)
            {
                if (!f.ExpectValue) throw new InvalidOperationException("Call Key() before value in object.");
                f.ExpectValue = false;
            }
            else if (f.Kind == FrameKind.Array)
            {
                if (!f.First) _sb.Append(',');
                f.First = false;
            }
            // Root: just write
        }

        private void Pop(FrameKind expected)
        {
            var f = _stack.Pop();
            if (f.Kind != expected) throw new InvalidOperationException($"Expected to close {expected}, was in {f.Kind}.");
        }

        private void WriteString(string s)
        {
            if (s == null) { _sb.Append("null"); return; }
            _sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"':  _sb.Append("\\\""); break;
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

        private enum FrameKind { Root, Object, Array }
        private class Frame
        {
            public readonly FrameKind Kind;
            public bool First = true;
            public bool ExpectValue;
            public Frame(FrameKind k) { Kind = k; }
        }
    }
}
