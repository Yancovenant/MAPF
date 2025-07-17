/**
* === Scripts/MiniJSON.cs ===
* Unity-compatible public domain implementation
* Source: https://gist.github.com/darktable/1411710
*/

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace MiniJSON {
    public static class Json {
        public static object Deserialize(string json) {
            if (json == null) return null;
            return Parser.Parse(json);
        }
        public static string Serialize(object obj) {
            return Serializer.Serialize(obj);
        }
        sealed class Parser : IDisposable {
            const string WORD_BREAK = "{}[],:\"";
            public static bool IsWordBreak(char c) {
                return char.IsWhiteSpace(c) || WORD_BREAK.IndexOf(c) != -1;
            }
            StringReader json;
            Parser(string jsonString) { json = new StringReader(jsonString); }
            public static object Parse(string jsonString) {
                using (var instance = new Parser(jsonString)) {
                    return instance.ParseValue();
                }
            }
            public void Dispose() { json.Dispose(); }
            Dictionary<string, object> ParseObject() {
                var table = new Dictionary<string, object>();
                json.Read();
                while (true) {
                    switch (NextToken) {
                        case TOKEN.NONE: return null;
                        case TOKEN.CURLY_CLOSE: return table;
                        default:
                            string name = ParseString();
                            if (NextToken != TOKEN.COLON) return null;
                            json.Read();
                            table[name] = ParseValue();
                            break;
                    }
                }
            }
            List<object> ParseArray() {
                var array = new List<object>();
                json.Read();
                var parsing = true;
                while (parsing) {
                    TOKEN nextToken = NextToken;
                    switch (nextToken) {
                        case TOKEN.NONE: return null;
                        case TOKEN.SQUARE_CLOSE: parsing = false; break;
                        default: array.Add(ParseValue()); break;
                    }
                }
                return array;
            }
            object ParseValue() {
                switch (NextToken) {
                    case TOKEN.STRING: return ParseString();
                    case TOKEN.NUMBER: return ParseNumber();
                    case TOKEN.CURLY_OPEN: return ParseObject();
                    case TOKEN.SQUARE_OPEN: return ParseArray();
                    case TOKEN.TRUE: return true;
                    case TOKEN.FALSE: return false;
                    case TOKEN.NULL: return null;
                    default: return null;
                }
            }
            string ParseString() {
                var s = new StringBuilder();
                char c;
                json.Read();
                bool parsing = true;
                while (parsing) {
                    if (json.Peek() == -1) break;
                    c = NextChar;
                    switch (c) {
                        case '"': parsing = false; break;
                        case '\\':
                            if (json.Peek() == -1) parsing = false;
                            c = NextChar;
                            switch (c) {
                                case '"': s.Append('"'); break;
                                case '\\': s.Append('\\'); break;
                                case '/': s.Append('/'); break;
                                case 'b': s.Append('\b'); break;
                                case 'f': s.Append('\f'); break;
                                case 'n': s.Append('\n'); break;
                                case 'r': s.Append('\r'); break;
                                case 't': s.Append('\t'); break;
                                case 'u':
                                    var hex = new char[4];
                                    for (int i = 0; i < 4; i++) hex[i] = NextChar;
                                    s.Append((char)Convert.ToInt32(new string(hex), 16));
                                    break;
                            }
                            break;
                        default: s.Append(c); break;
                    }
                }
                return s.ToString();
            }
            object ParseNumber() {
                string number = NextWord;
                if (number.IndexOf('.') == -1) {
                    long parsedInt;
                    Int64.TryParse(number, out parsedInt);
                    return parsedInt;
                }
                double parsedDouble;
                Double.TryParse(number, out parsedDouble);
                return parsedDouble;
            }
            void EatWhitespace() {
                while (char.IsWhiteSpace(PeekChar)) json.Read();
            }
            char PeekChar { get { return Convert.ToChar(json.Peek()); } }
            char NextChar { get { return Convert.ToChar(json.Read()); } }
            string NextWord {
                get {
                    var word = new StringBuilder();
                    while (!IsWordBreak(PeekChar)) word.Append(NextChar);
                    return word.ToString();
                }
            }
            TOKEN NextToken {
                get {
                    EatWhitespace();
                    if (json.Peek() == -1) return TOKEN.NONE;
                    switch (PeekChar) {
                        case '{': return TOKEN.CURLY_OPEN;
                        case '}': json.Read(); return TOKEN.CURLY_CLOSE;
                        case '[': return TOKEN.SQUARE_OPEN;
                        case ']': json.Read(); return TOKEN.SQUARE_CLOSE;
                        case ',': json.Read(); return NextToken;
                        case '"': return TOKEN.STRING;
                        case ':': return TOKEN.COLON;
                        case '0': case '1': case '2': case '3': case '4':
                        case '5': case '6': case '7': case '8': case '9':
                        case '-': return TOKEN.NUMBER;
                    }
                    string word = NextWord;
                    switch (word) {
                        case "false": return TOKEN.FALSE;
                        case "true": return TOKEN.TRUE;
                        case "null": return TOKEN.NULL;
                    }
                    return TOKEN.NONE;
                }
            }
            enum TOKEN {
                NONE, CURLY_OPEN, CURLY_CLOSE, SQUARE_OPEN, SQUARE_CLOSE,
                COLON, COMMA, STRING, NUMBER, TRUE, FALSE, NULL
            }
        }
        sealed class Serializer {
            StringBuilder builder;
            Serializer() { builder = new StringBuilder(); }
            public static string Serialize(object obj) {
                var instance = new Serializer();
                instance.SerializeValue(obj);
                return instance.builder.ToString();
            }
            void SerializeValue(object value) {
                if (value == null) builder.Append("null");
                else if (value is string) SerializeString((string)value);
                else if (value is bool) builder.Append((bool)value ? "true" : "false");
                else if (value is IList) SerializeArray((IList)value);
                else if (value is IDictionary) SerializeObject((IDictionary)value);
                else if (value is char) SerializeString(new string((char)value, 1));
                else SerializeOther(value);
            }
            void SerializeObject(IDictionary obj) {
                bool first = true;
                builder.Append('{');
                foreach (object e in obj.Keys) {
                    if (!first) builder.Append(',');
                    SerializeString(e.ToString());
                    builder.Append(':');
                    SerializeValue(obj[e]);
                    first = false;
                }
                builder.Append('}');
            }
            void SerializeArray(IList anArray) {
                builder.Append('[');
                bool first = true;
                foreach (object obj in anArray) {
                    if (!first) builder.Append(',');
                    SerializeValue(obj);
                    first = false;
                }
                builder.Append(']');
            }
            void SerializeString(string str) {
                builder.Append('"');
                foreach (var c in str) {
                    switch (c) {
                        case '\\': builder.Append("\\\\"); break;
                        case '"': builder.Append("\\\""); break;
                        case '\b': builder.Append("\\b"); break;
                        case '\f': builder.Append("\\f"); break;
                        case '\n': builder.Append("\\n"); break;
                        case '\r': builder.Append("\\r"); break;
                        case '\t': builder.Append("\\t"); break;
                        default:
                            int codepoint = Convert.ToInt32(c);
                            if ((codepoint >= 32) && (codepoint <= 126)) builder.Append(c);
                            else builder.AppendFormat("\\u{0:X4}", codepoint);
                            break;
                    }
                }
                builder.Append('"');
            }
            void SerializeOther(object value) {
                if (value is float || value is double || value is decimal)
                    builder.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
                else builder.Append(value.ToString());
            }
        }
    }
}