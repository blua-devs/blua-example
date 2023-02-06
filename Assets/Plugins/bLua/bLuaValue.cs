using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using bLua.NativeLua;

namespace bLua
{
    public class bLuaValue : System.IDisposable
    {
        public static void DeInit()
        {
            s_internedStrings.Clear();
            Array.Clear(s_stringCache, 0, s_stringCache.Length);
        }

#if UNITY_EDITOR
        public static int nLive = 0;
        public static int nLiveHighWater = 0;
#endif

        public static int nTotalCreated = 0;

        public static int NOREF = -2;
        public static int REFNIL = -1;

        public int refid = NOREF;
        public bLua.DataType dataType = DataType.Unknown;

        public static System.Collections.Concurrent.ConcurrentQueue<int> deleteQueue = new System.Collections.Concurrent.ConcurrentQueue<int>();

        public int ReferenceID
        {
            get
            {
                return refid;
            }
        }

        static public bLuaValue True = null;
        static public bLuaValue False = null;

        static public bLuaValue Nil = new bLuaValue()
        {
            dataType = DataType.Nil,
        };

        public static bLuaValue NewNil()
        {
            return Nil;
        }


        public static bLuaValue CreateNil()
        {
            return Nil;
        }

        public static bool IsNilOrNull(bLuaValue val)
        {
            return val == null || val.refid == REFNIL;
        }

        public static bool NotNilOrNull(bLuaValue val)
        {
            return val != null && val.refid != REFNIL;
        }

        public bool IsNil()
        {
            return refid == REFNIL;
        }

        public bool IsNotNil()
        {
            return refid != REFNIL;
        }


        public bool IsTable()
        {
            return Type == DataType.Table;
        }

        static Dictionary<string, bLuaValue> s_internedStrings = new Dictionary<string, bLuaValue>();
        public static bLuaValue InternString(string s)
        {
            bLuaValue result;
            if (s_internedStrings.TryGetValue(s, out result))
            {
                return result;
            }

            result = CreateString(s);
            s_internedStrings.Add(s, result);
            return result;
        }

        public static bLuaValue NewString(string s)
        {
            return CreateString(s);
        }

        struct StringCacheEntry
        {
            public string key;
            public bLuaValue value;
        }

        static public int s_stringCacheHit = 0, s_stringCacheMiss = 0;

        static StringCacheEntry[] s_stringCache = new StringCacheEntry[997];

        public static bLuaValue CreateString(string s)
        {
            if (s == null)
            {
                return bLuaValue.Nil;
            }

            if (s.Length < 32)
            {
                uint hash = (uint)s.GetHashCode();
                uint n = hash % (uint)s_stringCache.Length;
                var entry = s_stringCache[n];
                if (entry.key == s)
                {
                    UnityEngine.Assertions.Assert.AreEqual(entry.key, entry.value.String);
                    ++s_stringCacheHit;
                    return entry.value;
                } else
                {
                    Lua.PushObjectOntoStack(s);
                    var result = Lua.PopStackIntoValue();

                    entry.key = s;
                    entry.value = result;
                    s_stringCache[n] = entry;
                    ++s_stringCacheMiss;
                    return result;
                }
            }

            Lua.PushObjectOntoStack(s);
            return Lua.PopStackIntoValue();
        }

        public static bLuaValue UniqueString(string s)
        {
            if (s == null)
            {
                return bLuaValue.Nil;
            }

            Lua.PushObjectOntoStack(s);
            return Lua.PopStackIntoValue();
        }

        public static bLuaValue CreateNumber(double d)
        {
            Lua.PushObjectOntoStack(d);
            return Lua.PopStackIntoValue();
        }

        public static bLuaValue CreateBool(bool b)
        {
            return b ? True : False;
        }

        //easy compatibility.
        public static bLuaValue NewTable()
        {
            return CreateTable();
        }

        public static bLuaValue CreateTable(int reserveArray=0, int reserveTable=0)
        {

            Lua.PushNewTable(reserveArray, reserveTable);
            return Lua.PopStackIntoValue();
        }

        public static bLuaValue CreateFunction(Lua.LuaCFunction fn)
        {
            Lua.LuaPushCFunction(fn);
            return Lua.PopStackIntoValue();
        }

        public static bLuaValue CreateClosure(Lua.LuaCFunction fn, params bLuaValue[] upvalues)
        {
            Lua.PushClosure(fn, upvalues);
            return Lua.PopStackIntoValue();
        }

        public static bLuaValue CreateUserData(object obj)
        {
            if (obj == null)
            {
                return Nil;
            }
            bLuaUserData.PushNewUserData(obj);
            return Lua.PopStackIntoValue();
        }

        public static bLuaValue FromObject(object obj)
        {
            Lua.PushObjectOntoStack(obj);
            return Lua.PopStackIntoValue();
        }

        public bLuaValue()
        {
#if UNITY_EDITOR
            System.Threading.Interlocked.Increment(ref nLive);
            if (nLive > nLiveHighWater)
            {
                nLiveHighWater = nLive;
            }
#endif

            refid = REFNIL;
            ++nTotalCreated;
        }
        public bLuaValue(int refid_)
        {
#if UNITY_EDITOR
            System.Threading.Interlocked.Increment(ref nLive);
            if (nLive > nLiveHighWater)
            {
                nLiveHighWater = nLive;
            }
#endif


            refid = refid_;
            ++nTotalCreated;
        }

        ~bLuaValue()
        {
            Dispose(false);

#if UNITY_EDITOR
            System.Threading.Interlocked.Decrement(ref nLive);
#endif
        }

        public void Dispose()
        {
            Dispose(true);
            System.GC.SuppressFinalize(this);

#if UNITY_EDITOR
            System.Threading.Interlocked.Decrement(ref nLive);
#endif
        }

        void Dispose(bool deterministic)
        {
            if (refid != NOREF && refid != REFNIL)
            {
                if (deterministic)
                {
                    Lua.DestroyDynValue(refid);
                }
                else
                {
                    if (Feature.CSharpGarbageCollection.Enabled())
                    {
                        deleteQueue.Enqueue(refid);
                    }
                }
                refid = NOREF;
            }
        }

        

        public bLua.DataType Type
        {
            get
            {
                if (dataType == DataType.Unknown)
                {
                    Lua.PushStack(this);
                    dataType = Lua.InspectTypeOnTopOfStack();
                    Lua.PopStack();
                }

                return dataType;
            }
        }

        public double Number
        {
            get
            {
                Lua.PushStack(this);
                return Lua.PopNumber();
            }
        }

        public int Integer
        {
            get
            {
                Lua.PushStack(this);
                return Lua.PopInteger();
            }
        }

        public bool Boolean
        {
            get
            {
                Lua.PushStack(this);
                return Lua.PopBool();
            }
        }


        public string String
        {
            get
            {
                int t = Lua.PushStack(this);
                if (t == (int)DataType.String)
                {
                    return Lua.PopString();
                }

                Lua.PopStack();
                return null;
            }
        }

        public bLuaValue UserData
        {
            get
            {
                if (Type != DataType.UserData)
                {
                    return null;
                }

                return this;
            }
        }

        public object Object
        {
            get
            {
                if (Type != DataType.UserData)
                {
                    return null;
                }

                Lua.PushStack(this);
                object result = bLuaUserData.GetUserDataObject(-1);
                Lua.PopStack();
                return result;

            }
        }

        public bLuaValue Function
        {
            get
            {
                if (Type == DataType.Function)
                {
                    return this;
                }

                return null;
            }
        }

        public bLuaValue Table
        {
            get
            {
                if (Type == DataType.Table)
                {
                    return this;
                }

                return null;
            }
        }

        public bLuaValue MetaTable
        {
            get
            {
#if UNITY_EDITOR
                int nstack = LuaLibAPI.lua_gettop(bLuaNative._state);
#endif

                Lua.PushStack(this);
                int res = LuaLibAPI.lua_getmetatable(bLuaNative._state, -1);
                if (res == 0)
                {
                    Lua.PopStack();
                    return Nil;
                }

                var result = Lua.PopStackIntoValue();
                Lua.PopStack();

#if UNITY_EDITOR
                Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(bLuaNative._state));
#endif

                return result;
            }
            set
            {
                Lua.PushStack(this);
                Lua.PushStack(value);
                LuaLibAPI.lua_setmetatable(bLuaNative._state, -2);
                Lua.PopStack();
            }
        }

        public bool? CastToOptionalBool()
        {
            DataType dataType = (DataType)Lua.PushStack(this);
            switch (dataType)
            {
                case DataType.Boolean:
                    return Lua.PopBool();
                case DataType.Number:
                    return Lua.PopNumber() != 0;
                case DataType.Nil:
                    Lua.PopStack();
                    return null;
                default:
                    Lua.PopStack();
                    return null;
            }
        }

        public bool CastToBool(bool defaultValue=false)
        {
            DataType dataType = (DataType)Lua.PushStack(this);
            switch (dataType)
            {
                case DataType.Boolean:
                    return Lua.PopBool();
                case DataType.Number:
                    return Lua.PopNumber() != 0;
                case DataType.Nil:
                    Lua.PopStack();
                    return defaultValue;
                default:
                    Lua.PopStack();
                    return defaultValue;
            }
        }

        public string ToPrintString()
        {
            return CastToString();
        }

        public string ToDebugPrintString()
        {
            return CastToString();
        }

        public string CastToString(string defaultValue="")
        {
            DataType dataType = (DataType)Lua.PushStack(this);

            switch (dataType)
            {
                case DataType.String:
                    return Lua.PopString();
                case DataType.Number:
                    return Lua.PopNumber().ToString();
                case DataType.Boolean:
                    return Lua.PopBool() ? "true" : "false";
                default:
                    Lua.PopStack();
                    return defaultValue;
            }
        }

        public float? CastToOptionalFloat()
        {
            DataType dataType = (DataType)Lua.PushStack(this);
            switch (dataType)
            {
                case DataType.Number:
                    return (float)Lua.PopNumber();
                case DataType.String:
                    {
                        float f;
                        string s = Lua.PopString();
                        if (float.TryParse(s, out f))
                        {
                            return f;
                        }

                        return null;
                    }
                case DataType.Boolean:
                    return Lua.PopBool() ? 1.0f : 0.0f;
                default:
                    Lua.PopStack();
                    return null;
            }

        }

        public float CastToFloat(float defaultValue=0.0f)
        {
            DataType dataType = (DataType)Lua.PushStack(this);

            switch (dataType)
            {
                case DataType.Number:
                    return (float)Lua.PopNumber();
                case DataType.String:
                    {
                        float f;
                        string s = Lua.PopString();
                        if (float.TryParse(s, out f))
                        {
                            return f;
                        }

                        return defaultValue;
                    }
                case DataType.Boolean:
                    return Lua.PopBool() ? 1.0f : 0.0f;
                default:
                    Lua.PopStack();
                    return defaultValue;
            }
        }

        public int CastToInt(int defaultValue = 0)
        {
            DataType dataType = (DataType)Lua.PushStack(this);

            switch (dataType)
            {
                case DataType.Number:
                    return (int)Lua.PopNumber();
                case DataType.String:
                    {
                        int f;
                        string s = Lua.PopString();
                        if (int.TryParse(s, out f))
                        {
                            return f;
                        }

                        return defaultValue;
                    }
                case DataType.Boolean:
                    return Lua.PopBool() ? 1 : 0;
                default:
                    Lua.PopStack();
                    return defaultValue;
            }
        }


        public double? CastToNumber()
        {
            DataType dataType = (DataType)Lua.PushStack(this);

            switch (dataType)
            {
                case DataType.Number:
                    return Lua.PopNumber();
                case DataType.String:
                    {
                        double f;
                        string s = Lua.PopString();
                        if (double.TryParse(s, out f))
                        {
                            return f;
                        }

                        return 0.0;
                    }
                case DataType.Boolean:
                    return Lua.PopBool() ? 1.0 : 0.0;
                case DataType.Nil:
                    Lua.PopStack();
                    return null;
                default:
                    Lua.PopStack();
                    return null;
            }
        }

        public T CheckUserDataType<T>(string str) where T : class
        {
            T result = Object as T;
            if (result == null)
            {
                Debug.Log($"Could not convert to lua value to type: {str}");
            }

            return result;
        }

        public T ToObject<T>()
        {
            return (T)Object;
        }

        public object ToObject(System.Type t)
        {
            if (t == typeof(double))
            {
                return CastToNumber();
            } else if (t == typeof(float))
            {
                return CastToFloat();
            } else if (t == typeof(int))
            {
                return (int)CastToNumber();
            } else if (t == typeof(bool))
            {
                return CastToBool();
            } else if (t == typeof(string))
            {
                return CastToString();
            } else
            {
                return null;
            }
        }

        public object ToObject()
        {
            switch (Type)
            {
                case DataType.Boolean:
                    return CastToBool();
                case DataType.Nil:
                    return null;
                case DataType.Number:
                    return Number;
                case DataType.String:
                    return String;
                case DataType.Table:
                    return Dict();
                case DataType.UserData:
                    return Object;
                default:
                    return null;
            }
        }

        public bLuaValue Call(params object[] args)
        {
            return bLuaNative.Call(this, args);
        }

        //table operations.
        public int Length
        {
            get
            {
                return Lua.Length(this);
            }
        }

        public bLuaValue this[int n] {
            get
            {
                return Lua.Index(this, n+1);
            }
        }

        public bLuaValue GetNonRaw(string key)
        {
            return Lua.GetTable(this, key);
        }

        public bLuaValue GetNonRaw(object key)
        {
            return Lua.GetTable(this, key);
        }

        //synonyms with RawGet
        public bLuaValue Get(string key)
        {
            return Lua.RawGetTable(this, key);
        }

        public bLuaValue Get(object key)
        {
            return Lua.RawGetTable(this, key);
        }

        public bLuaValue RawGet(object key)
        {
            return Lua.RawGetTable(this, key);
        }

        public bLuaValue RawGet(string key)
        {
            return Lua.RawGetTable(this, key);
        }

        public void Set(bLuaValue key, bLuaValue val)
        {
            Lua.SetTable(this, key, val);
        }

        public void Set(string key, bLuaValue val)
        {
            Lua.SetTable(this, key, val);
        }

        public void Set(string key, object val)
        {
            Lua.SetTable(this, key, val);
        }

        public void Set(object key, object val)
        {
            Lua.SetTable(this, key, val);
        }

        public void Remove(object key)
        {
            Lua.SetTable(this, key, Nil);
        }

        public List<bLuaValue> List()
        {
            if (Type != DataType.Table)
            {
                return null;
            }

#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(bLuaNative._state);
#endif

            Lua.PushStack(this);
            var result = Lua.PopList();

#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(bLuaNative._state));
#endif

            return result;
        }

        public List<string> ListOfStrings()
        {
            if (Type != DataType.Table)
            {
                return null;
            }

#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(bLuaNative._state);
#endif

            Lua.PushStack(this);
            var result = Lua.PopListOfStrings();
#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(bLuaNative._state));
#endif

            return result;
        }

        public Dictionary<string,bLuaValue> Dict()
        {
#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(bLuaNative._state);
#endif

            Lua.PushStack(this);
            var result = Lua.PopDict();

#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(bLuaNative._state));
#endif

            return result;
        }

        public struct Pair
        {
            public bLuaValue Key;
            public bLuaValue Value;
        }

        public List<Pair> Pairs()
        {
#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(bLuaNative._state);
#endif

            Lua.PushStack(this);
            var result = Lua.PopFullDict();

#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(bLuaNative._state));
#endif


            return result;
        }

        public List<bLuaValue> Keys
        {
            get
            {
#if UNITY_EDITOR
                int nstack = LuaLibAPI.lua_gettop(bLuaNative._state);
#endif

                var result = Pairs();
                var values = new List<bLuaValue>();
                foreach (var p in result)
                {
                    values.Add(p.Key);
                }

#if UNITY_EDITOR
                Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(bLuaNative._state));
#endif

                return values;
            }
        }


        public List<bLuaValue> Values
        {
            get
            {
                var result = Pairs();
                var values = new List<bLuaValue>();
                foreach (var p in result)
                {
                    values.Add(p.Value);
                }
                return values;
            }
        }

        //needed?
        public void CollectDeadKeys()
        { }

        public bool TableEmpty
        {
            get
            {
#if UNITY_EDITOR
                int nstack = LuaLibAPI.lua_gettop(bLuaNative._state);
#endif

                Lua.PushStack(this);
                var result = Lua.PopTableEmpty();

#if UNITY_EDITOR
                Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(bLuaNative._state));
#endif

                return result;
            }
        }

        //has only non-ints.
        public bool IsPureArray
        {
            get
            {
                Lua.PushStack(this);
                return !Lua.PopTableHasNonInts();
            }
        }

        public void Append(bLuaValue val)
        {
            Lua.AppendArray(this, val);
        }

        public void Append(object val)
        {
            Lua.AppendArray(this, val);
        }

        public static void RunDispose(List<bLuaValue> list)
        {
            foreach (var item in list)
            {
                item.Dispose();
            }
        }


        public static void RunDispose(Dictionary<string,bLuaValue> dict)
        {
            foreach (var item in dict)
            {
                item.Value.Dispose();
            }
        }

        public override bool Equals(object a)
        {
            bLuaValue other = a as bLuaValue;
            if (other == null)
            {
                return false;
            }

#if UNITY_EDITOR
            int nstack = LuaLibAPI.lua_gettop(bLuaNative._state);
#endif

            Lua.PushStack(this);
            Lua.PushStack(other);

            int res = LuaLibAPI.lua_rawequal(bLuaNative._state, -1, -2);
            Lua.LuaPop(bLuaNative._state, 2);

#if UNITY_EDITOR
            Assert.AreEqual(nstack, LuaLibAPI.lua_gettop(bLuaNative._state));
#endif


            return res != 0;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
} // bLua namespace
