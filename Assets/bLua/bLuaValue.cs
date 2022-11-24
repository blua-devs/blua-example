using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

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
                    bLuaNative.PushObjectOntoStack(s);
                    var result = bLuaNative.PopStackIntoValue();

                    entry.key = s;
                    entry.value = result;
                    s_stringCache[n] = entry;
                    ++s_stringCacheMiss;
                    return result;
                }
            }

            bLuaNative.PushObjectOntoStack(s);
            return bLuaNative.PopStackIntoValue();
        }

        public static bLuaValue UniqueString(string s)
        {
            if (s == null)
            {
                return bLuaValue.Nil;
            }

            bLuaNative.PushObjectOntoStack(s);
            return bLuaNative.PopStackIntoValue();
        }

        public static bLuaValue CreateNumber(double d)
        {
            bLuaNative.PushObjectOntoStack(d);
            return bLuaNative.PopStackIntoValue();
        }

        public static bLuaValue CreateBool(bool b)
        {
            return b ? True : False;
        }

        //easy compatibility.
        public static bLuaValue NewTable(bLuaNative.Script script)
        {
            return CreateTable();
        }

        public static bLuaValue CreateTable(int reserveArray=0, int reserveTable=0)
        {

            bLuaNative.PushNewTable(reserveArray, reserveTable);
            return bLuaNative.PopStackIntoValue();
        }

        public static bLuaValue CreateFunction(bLuaNative.LuaCFunction fn)
        {
            bLuaNative.lua_pushcfunction(fn);
            return bLuaNative.PopStackIntoValue();
        }

        public static bLuaValue CreateClosure(bLuaNative.LuaCFunction fn, params bLuaValue[] upvalues)
        {
            bLuaNative.PushClosure(fn, upvalues);
            return bLuaNative.PopStackIntoValue();
        }

        public static bLuaValue CreateUserData(object obj)
        {
            if (obj == null)
            {
                return Nil;
            }
            bLuaUserData.PushNewUserData(obj);
            return bLuaNative.PopStackIntoValue();
        }

        public static bLuaValue FromObject(object obj)
        {
            bLuaNative.PushObjectOntoStack(obj);
            return bLuaNative.PopStackIntoValue();
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
                    bLuaNative.DestroyDynValue(refid);
                } else
                {
                    deleteQueue.Enqueue(refid);
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
                    bLuaNative.PushStack(this);
                    dataType = bLuaNative.InspectTypeOnTopOfStack();
                    bLuaNative.PopStack();
                }

                return dataType;
            }
        }

        public double Number
        {
            get
            {
                bLuaNative.PushStack(this);
                return bLuaNative.PopNumber();
            }
        }

        public int Integer
        {
            get
            {
                bLuaNative.PushStack(this);
                return bLuaNative.PopInteger();
            }
        }

        public bool Boolean
        {
            get
            {
                bLuaNative.PushStack(this);
                return bLuaNative.PopBool();
            }
        }


        public string String
        {
            get
            {
                int t = bLuaNative.PushStack(this);
                if (t == (int)DataType.String)
                {
                    return bLuaNative.PopString();
                }

                bLuaNative.PopStack();
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

                bLuaNative.PushStack(this);
                object result = bLuaUserData.GetUserDataObject(-1);
                bLuaNative.PopStack();
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
                int nstack = bLuaNative.lua_gettop(bLuaNative.script._state);
#endif

                bLuaNative.PushStack(this);
                int res = bLuaNative.lua_getmetatable(bLuaNative.script._state, -1);
                if (res == 0)
                {
                    bLuaNative.PopStack();
                    return Nil;
                }

                var result = bLuaNative.PopStackIntoValue();
                bLuaNative.PopStack();

#if UNITY_EDITOR
                Assert.AreEqual(nstack, bLuaNative.lua_gettop(bLuaNative.script._state));
#endif

                return result;
            }
            set
            {
                bLuaNative.PushStack(this);
                bLuaNative.PushStack(value);
                bLuaNative.lua_setmetatable(bLuaNative.script._state, -2);
                bLuaNative.PopStack();
            }
        }

        public bool? CastToOptionalBool()
        {
            DataType dataType = (DataType)bLuaNative.PushStack(this);
            switch (dataType)
            {
                case DataType.Boolean:
                    return bLuaNative.PopBool();
                case DataType.Number:
                    return bLuaNative.PopNumber() != 0;
                case DataType.Nil:
                    bLuaNative.PopStack();
                    return null;
                default:
                    bLuaNative.PopStack();
                    return null;
            }
        }

        public bool CastToBool(bool defaultValue=false)
        {
            DataType dataType = (DataType)bLuaNative.PushStack(this);
            switch (dataType)
            {
                case DataType.Boolean:
                    return bLuaNative.PopBool();
                case DataType.Number:
                    return bLuaNative.PopNumber() != 0;
                case DataType.Nil:
                    bLuaNative.PopStack();
                    return defaultValue;
                default:
                    bLuaNative.PopStack();
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
            DataType dataType = (DataType)bLuaNative.PushStack(this);

            switch (dataType)
            {
                case DataType.String:
                    return bLuaNative.PopString();
                case DataType.Number:
                    return bLuaNative.PopNumber().ToString();
                case DataType.Boolean:
                    return bLuaNative.PopBool() ? "true" : "false";
                default:
                    bLuaNative.PopStack();
                    return defaultValue;
            }
        }

        public float? CastToOptionalFloat()
        {
            DataType dataType = (DataType)bLuaNative.PushStack(this);
            switch (dataType)
            {
                case DataType.Number:
                    return (float)bLuaNative.PopNumber();
                case DataType.String:
                    {
                        float f;
                        string s = bLuaNative.PopString();
                        if (float.TryParse(s, out f))
                        {
                            return f;
                        }

                        return null;
                    }
                case DataType.Boolean:
                    return bLuaNative.PopBool() ? 1.0f : 0.0f;
                default:
                    bLuaNative.PopStack();
                    return null;
            }

        }

        public float CastToFloat(float defaultValue=0.0f)
        {
            DataType dataType = (DataType)bLuaNative.PushStack(this);

            switch (dataType)
            {
                case DataType.Number:
                    return (float)bLuaNative.PopNumber();
                case DataType.String:
                    {
                        float f;
                        string s = bLuaNative.PopString();
                        if (float.TryParse(s, out f))
                        {
                            return f;
                        }

                        return defaultValue;
                    }
                case DataType.Boolean:
                    return bLuaNative.PopBool() ? 1.0f : 0.0f;
                default:
                    bLuaNative.PopStack();
                    return defaultValue;
            }
        }

        public int CastToInt(int defaultValue = 0)
        {
            DataType dataType = (DataType)bLuaNative.PushStack(this);

            switch (dataType)
            {
                case DataType.Number:
                    return (int)bLuaNative.PopNumber();
                case DataType.String:
                    {
                        int f;
                        string s = bLuaNative.PopString();
                        if (int.TryParse(s, out f))
                        {
                            return f;
                        }

                        return defaultValue;
                    }
                case DataType.Boolean:
                    return bLuaNative.PopBool() ? 1 : 0;
                default:
                    bLuaNative.PopStack();
                    return defaultValue;
            }
        }


        public double? CastToNumber()
        {
            DataType dataType = (DataType)bLuaNative.PushStack(this);

            switch (dataType)
            {
                case DataType.Number:
                    return bLuaNative.PopNumber();
                case DataType.String:
                    {
                        double f;
                        string s = bLuaNative.PopString();
                        if (double.TryParse(s, out f))
                        {
                            return f;
                        }

                        return 0.0;
                    }
                case DataType.Boolean:
                    return bLuaNative.PopBool() ? 1.0 : 0.0;
                case DataType.Nil:
                    bLuaNative.PopStack();
                    return null;
                default:
                    bLuaNative.PopStack();
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
            return bLuaNative.script.Call(this, args);
        }

        //table operations.
        public int Length
        {
            get
            {
                return bLuaNative.Length(this);
            }
        }

        public bLuaValue this[int n] {
            get
            {
                return bLuaNative.Index(this, n+1);
            }
        }

        public bLuaValue GetNonRaw(string key)
        {
            return bLuaNative.GetTable(this, key);
        }

        public bLuaValue GetNonRaw(object key)
        {
            return bLuaNative.GetTable(this, key);
        }

        //synonyms with RawGet
        public bLuaValue Get(string key)
        {
            return bLuaNative.RawGetTable(this, key);
        }

        public bLuaValue Get(object key)
        {
            return bLuaNative.RawGetTable(this, key);
        }

        public bLuaValue RawGet(object key)
        {
            return bLuaNative.RawGetTable(this, key);
        }

        public bLuaValue RawGet(string key)
        {
            return bLuaNative.RawGetTable(this, key);
        }

        public void Set(bLuaValue key, bLuaValue val)
        {
            bLuaNative.SetTable(this, key, val);
        }

        public void Set(string key, bLuaValue val)
        {
            bLuaNative.SetTable(this, key, val);
        }

        public void Set(string key, object val)
        {
            bLuaNative.SetTable(this, key, val);
        }

        public void Set(object key, object val)
        {
            bLuaNative.SetTable(this, key, val);
        }

        public void Remove(object key)
        {
            bLuaNative.SetTable(this, key, Nil);
        }

        public List<bLuaValue> List()
        {
            if (Type != DataType.Table)
            {
                return null;
            }

#if UNITY_EDITOR
            int nstack = bLuaNative.lua_gettop(bLuaNative.script._state);
#endif

            bLuaNative.PushStack(this);
            var result = bLuaNative.PopList();

#if UNITY_EDITOR
            Assert.AreEqual(nstack, bLuaNative.lua_gettop(bLuaNative.script._state));
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
            int nstack = bLuaNative.lua_gettop(bLuaNative.script._state);
#endif

            bLuaNative.PushStack(this);
            var result = bLuaNative.PopListOfStrings();
#if UNITY_EDITOR
            Assert.AreEqual(nstack, bLuaNative.lua_gettop(bLuaNative.script._state));
#endif

            return result;
        }

        public Dictionary<string,bLuaValue> Dict()
        {
#if UNITY_EDITOR
            int nstack = bLuaNative.lua_gettop(bLuaNative.script._state);
#endif

            bLuaNative.PushStack(this);
            var result = bLuaNative.PopDict();

#if UNITY_EDITOR
            Assert.AreEqual(nstack, bLuaNative.lua_gettop(bLuaNative.script._state));
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
            int nstack = bLuaNative.lua_gettop(bLuaNative.script._state);
#endif

            bLuaNative.PushStack(this);
            var result = bLuaNative.PopFullDict();

#if UNITY_EDITOR
            Assert.AreEqual(nstack, bLuaNative.lua_gettop(bLuaNative.script._state));
#endif


            return result;
        }

        public List<bLuaValue> Keys
        {
            get
            {
#if UNITY_EDITOR
                int nstack = bLuaNative.lua_gettop(bLuaNative.script._state);
#endif

                var result = Pairs();
                var values = new List<bLuaValue>();
                foreach (var p in result)
                {
                    values.Add(p.Key);
                }

#if UNITY_EDITOR
                Assert.AreEqual(nstack, bLuaNative.lua_gettop(bLuaNative.script._state));
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
                int nstack = bLuaNative.lua_gettop(bLuaNative.script._state);
#endif

                bLuaNative.PushStack(this);
                var result = bLuaNative.PopTableEmpty();

#if UNITY_EDITOR
                Assert.AreEqual(nstack, bLuaNative.lua_gettop(bLuaNative.script._state));
#endif

                return result;
            }
        }

        //has only non-ints.
        public bool IsPureArray
        {
            get
            {
                bLuaNative.PushStack(this);
                return !bLuaNative.PopTableHasNonInts();
            }
        }

        public void Append(bLuaValue val)
        {
            bLuaNative.AppendArray(this, val);
        }

        public void Append(object val)
        {
            bLuaNative.AppendArray(this, val);
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
            int nstack = bLuaNative.lua_gettop(bLuaNative.script._state);
#endif

            bLuaNative.PushStack(this);
            bLuaNative.PushStack(other);

            int res = bLuaNative.lua_rawequal(bLuaNative.script._state, -1, -2);
            bLuaNative.lua_pop(bLuaNative.script._state, 2);

#if UNITY_EDITOR
            Assert.AreEqual(nstack, bLuaNative.lua_gettop(bLuaNative.script._state));
#endif


            return res != 0;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

}
