using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace bLua.JSON
{
    public static class bLuaJSONConverter
	{
		enum TokenType
		{
			Boolean,
			String,
			Number,
			BeginTable,
			EndTable,
			EndOfFile,
			KeySeparator,
			ValueSeparator,
			Null,
			None
		}

		struct Token
		{
			public TokenType type;
			public string value;


			public Token(TokenType _type)
			{
				type = _type;
				value = string.Empty;
			}
			public Token(TokenType _type, string _value)
			{
				type = _type;
				value = _value;
			}
		}

		const string nullString = "null";
		const string boolTrue = "true";
		const string boolFalse = "false";
		const char beginTable = '{';
		const char endTable = '}';
		const char valueSeparator = ',';
		const char keySeparator = ':';


		public static string BLuaTableToJSON(bLuaValue _value)
		{
			if (!_value.IsTable() || !IsCompatible(_value))
			{
				return nullString;
			}

			StringBuilder sb = new StringBuilder();
			BLuaValueToJSON(sb, _value);
			return sb.ToString();
		}

        public static bLuaValue JSONToBLuaTable(bLuaInstance _instance, string _json)
        {
			Debug.LogError("1");
			if (_instance == null || string.IsNullOrWhiteSpace(_json) || _json.Length == 0 || !_json.StartsWith(beginTable) || !_json.EndsWith(endTable))
			{
				Debug.LogError("2");
				return bLuaValue.CreateNil();
			}
			Debug.LogError("3");

			_json = _json.Substring(1, _json.Length - 1); // Cut off the { at the beginning

			Debug.LogError("4.. " + _json);
			StringReader sr = new StringReader(_json);
			bLuaValue table = JSONToBLuaTable(_instance, sr);

			Debug.LogError("5.. keys: " + table.Keys.Count);
			return table;
        }

        public static string ToJson(this bLuaValue _value)
        {
            return BLuaTableToJSON(_value);
        }

        public static bLuaValue ToBLuaTable(this string _json, bLuaInstance _instance)
        {
            return JSONToBLuaTable(_instance, _json);
        }

		#region bLuaValue to JSON
		static void BLuaValueToJSON(StringBuilder _sb, bLuaValue _value)
		{
			if (_value.IsTable())
			{
				BLuaTableToJSON(_sb, _value);
            }
			else
			{
				switch (_value.Type)
				{
					case DataType.Boolean:
						_sb.Append(_value.Boolean ? boolTrue : boolFalse);
						break;
					case DataType.String:
						_sb.Append(StringToJSON(_value.String ?? ""));
						break;
					case DataType.Number:
						_sb.Append(_value.Number.ToString("r"));
						break;
					default:
						_sb.Append(nullString);
						break;
				}
			}
		}

		static void BLuaTableToJSON(StringBuilder _sb, bLuaValue _table)
		{
			if (!_table.IsTable())
            {
				_sb.Append(beginTable);
				_sb.Append(nullString);
				_sb.Append(endTable);
				return;
            }

			_sb.Append(beginTable);
			bLuaValue.Pair[] pairs = _table.Pairs().ToArray();
			for (int i = 0; i < pairs.Length; i++)
            {
				if (pairs[i].Key.Type == DataType.String)
				{
					if (i > 0)
					{
						_sb.Append(valueSeparator);
					}

					BLuaValueToJSON(_sb, pairs[i].Key);
					_sb.Append(keySeparator);
				}
				else
                {
					continue;
                }

				if (IsCompatible(pairs[i].Value))
                {
					BLuaValueToJSON(_sb, pairs[i].Value);
                }
				else
                {
					_sb.Append(nullString);
                }
			}
			_sb.Append(endTable);
		}
		#endregion // bLuaValue to JSON

		#region JSON to bLuaValue
        /// <summary> This is called when the StringReader reads a BeginTable token, and will fill a new table with all of the containing values. </summary>
        static bLuaValue JSONToBLuaTable(bLuaInstance _instance, StringReader _sr)
		{
			bLuaValue table = bLuaValue.CreateTable(_instance);

			string key = string.Empty;
			Token token = new Token(TokenType.None);
			int i = 0;
			while (token.type != TokenType.EndOfFile)
            {
				i++;
				if (i > 100)
                {
					Debug.LogError("hit i = 100");
					return table;
                }
				token = GetNextToken(_sr);

				Debug.LogError("next token in table: " + token.type.ToString() + ", " + token.value);

				switch (token.type)
				{
					case TokenType.EndOfFile:
						return table;
					case TokenType.ValueSeparator:
						key = string.Empty;
						break;
					case TokenType.BeginTable:
						if (!string.IsNullOrEmpty(key))
                        {
							table.Set(key, JSONToBLuaTable(_instance, _sr));
                        }
						else
                        {
							Debug.LogError("e2: " + token.type.ToString() + ", " + token.value);
						}
						break;
					case TokenType.EndTable:
						return table;
					case TokenType.Boolean:
						if (!string.IsNullOrEmpty(key))
                        {
							table.Set(key, TokenToBool(token));
                        }
						else
                        {
							Debug.LogError("e3: " + token.type.ToString() + ", " + token.value);
						}
						break;
					case TokenType.String:
						if (!string.IsNullOrEmpty(key))
						{
							table.Set(key, TokenToString(token));
						}
						else
						{
							key = TokenToString(token);
						}
						break;
					case TokenType.Number:
						if (!string.IsNullOrEmpty(key))
						{
							table.Set(key, TokenToNumber(token));
						}
						else
						{
							Debug.LogError("e5: " + token.type.ToString() + ", " + token.value);
						}
						break;
					case TokenType.Null:
						if (!string.IsNullOrEmpty(key))
                        {
							table.Set(key, bLuaValue.CreateNil());
                        }
						else
                        {
							Debug.LogError("e6: " + token.type.ToString() + ", " + token.value);
						}
						break;
				}
			}

			return table;
		}

		static bool TokenToBool(Token _token)
        {
			return _token.value == boolTrue;
		}

		static string TokenToString(Token _token)
		{
			return JSONToString(_token.value);
		}

		static double TokenToNumber(Token _token)
        {
			return double.Parse(_token.value);
		}

		static Token GetNextToken(StringReader _sr)
        {
			int i = _sr.Peek();
			if (i == -1)
            {
				return new Token(TokenType.EndOfFile);
            }
			char c = (char)_sr.Read();

			if (char.IsWhiteSpace(c))
            {
				// Skip white space
				return GetNextToken(_sr);
			}
			else if (c == '"')
            {
				string s = string.Empty;
				c = (char)_sr.Read();
				while (c != '"' && c != endTable)
				{
					s += c;

					i = _sr.Peek();
					if (i == -1)
					{
						break;
					}
					else if ((char)i == '"')
					{
						_sr.Read(); // Consume the ending quotation marks
						break;
					}
					c = (char)_sr.Read();
				}
				return new Token() { type = TokenType.String, value = s };
			}
			else if (c.IsJSONNumber())
            {
				string n = string.Empty;
				while (c.IsJSONNumber() && c != endTable)
                {
					n += c;

					i = _sr.Peek();
					if (i == -1)
                    {
						break;
                    }
					else if (!((char)i).IsJSONNumber())
                    {
						break;
                    }
					c = (char)_sr.Read();
				}
				return new Token() { type = TokenType.Number, value = n };
            }
			else if (c == beginTable)
            {
				return new Token(TokenType.BeginTable, beginTable.ToString());
            }
			else if (c == endTable)
			{
				return new Token(TokenType.EndTable, endTable.ToString());
			}
			else if (c == keySeparator)
            {
				return new Token(TokenType.KeySeparator);
            }
			else if (c == valueSeparator)
            {
				return new Token(TokenType.ValueSeparator);
			}
			else if (char.IsLetter(c))
			{
				string s = string.Empty;
				while (char.IsLetter(c))
				{
					s += c;

					if (s == boolTrue)
					{
						return new Token(TokenType.Boolean, boolTrue);
					}
					else if (s == boolFalse)
					{
						return new Token(TokenType.Boolean, boolFalse);
					}
					else if (s == nullString)
                    {
						return new Token(TokenType.Null, nullString);
                    }

					if (s.Length > boolTrue.Length && s.Length > boolFalse.Length && s.Length > nullString.Length)
                    {
						break;
                    }

					i = _sr.Peek();
					if (i == -1)
					{
						break;
					}
					c = (char)_sr.Read();
				}
			}

			return new Token(TokenType.None);
        }

		static bool IsJSONNumber(this char _c)
        {
			return char.IsDigit(_c) || _c == '.';
        }
		#endregion // JSON to bLuaValue

		static string StringToJSON(string _string)
		{
			_string = _string.Replace("\b", @"\b");
			_string = _string.Replace("\f", @"\f");
			_string = _string.Replace("\n", @"\n");
			_string = _string.Replace("\r", @"\r");
			_string = _string.Replace("\t", @"\t");
			_string = _string.Replace(@"\", @"\\");
			_string = _string.Replace(@"/", @"\/");
			_string = _string.Replace("\"", "\\\"");
			return "\"" + _string + "\"";
		}

		static string JSONToString(string _json)
		{
			if (string.IsNullOrEmpty(_json))
            {
				return string.Empty;
            }

			_json = _json.Replace(@"\b", "\b");
			_json = _json.Replace(@"\f", "\f");
			_json = _json.Replace(@"\n", "\n");
			_json = _json.Replace(@"\r", "\r");
			_json = _json.Replace(@"\t", "\t");
			_json = _json.Replace(@"\\", @"\");
			_json = _json.Replace(@"\/", @"/");
			_json = _json.Replace("\\\"", "\"");
			return _json;
		}

		static bool IsCompatible(bLuaValue _value)
		{
			return _value.IsNil()
				|| _value.Type == DataType.Boolean
				|| _value.Type == DataType.String
				|| _value.Type == DataType.Number
				|| _value.Type == DataType.Table;
		}
	}
} // bLua.JSON namespace
