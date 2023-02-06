using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Profiling;

namespace bLua.NativeLua
{
    [StructLayout(LayoutKind.Sequential)]
    public class StrLen
    {
        public ulong len;
    }

    /// <summary> Contains helper functions as well as functions that interface with the LuaLibAPI and LuaXLibAPI. </summary>
    public static class Lua
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        public const string _dllName = "lua54.dll";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        public const string _dllName = "Lua";
#endif

        public static int LUA_TNONE = -1;
        public static int LUAI_MAXSTACK = 1000000;
        public static int LUA_REGISTRYINDEX = (-LUAI_MAXSTACK - 1000);

        public static ProfilerMarker s_profileLuaGC = new ProfilerMarker("Lua.GC");
        public static ProfilerMarker s_profileLuaCo = new ProfilerMarker("Lua.Coroutine");
        public static ProfilerMarker s_profileLuaCall = new ProfilerMarker("Lua.Call");
        public static ProfilerMarker s_profileLuaCallInner = new ProfilerMarker("Lua.CallInner");

        static StrLen s_strlen = new StrLen();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int LuaCFunction(System.IntPtr state);


        #region Miscellaneous
        public static byte[] StrToUTF8(string s)
        {
            return System.Text.UTF8Encoding.UTF8.GetBytes(s);
        }

        public static string UTF8ToStr(byte[] b)
        {
            return System.Text.UTF8Encoding.UTF8.GetString(b);
        }

        public static int UpValueIndex(int i)
        {
            return LUA_REGISTRYINDEX - i;
        }

        public static string GetString(System.IntPtr state, int n)
        {
            var ptr = LuaLibAPI.lua_tolstring(state, n, s_strlen);
            byte[] bytes = new byte[s_strlen.len];
            Marshal.Copy(ptr, bytes, 0, (int)s_strlen.len);
            return UTF8ToStr(bytes);
        }

        public static void DestroyDynValue(int refid)
        {
            if (bLuaNative._state != System.IntPtr.Zero)
            {
                //remove the value from the registry.
                LuaXLibAPI.luaL_unref(bLuaNative._state, LUA_REGISTRYINDEX, refid);
            }
        }

        public static DataType InspectTypeOnTopOfStack()
        {
            return (DataType)LuaLibAPI.lua_type(bLuaNative._state, -1);
        }

        public static string TraceMessage(string message = null, int level = 1)
        {
            if (message == null)
            {
                message = "stack";
            }
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);

            LuaXLibAPI.luaL_traceback(bLuaNative._state, bLuaNative._state, message, level);
            return PopString();
        }
        #endregion // Miscellaneous

        #region Push (Stack)
        public static void PushObjectOntoStack(bool b)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);

            LuaLibAPI.lua_pushboolean(bLuaNative._state, b ? 1 : 0);
        }

        public static void PushObjectOntoStack(double d)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);

            LuaLibAPI.lua_pushnumber(bLuaNative._state, d);
        }

        public static void PushObjectOntoStack(float d)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);

            LuaLibAPI.lua_pushnumber(bLuaNative._state, (double)d);
        }

        public static void PushObjectOntoStack(int d)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);

            LuaLibAPI.lua_pushinteger(bLuaNative._state, d);
        }

        public static void PushString(System.IntPtr state, string s)
        {
            byte[] b = StrToUTF8(s);

            unsafe
            {
                fixed (byte* p = b)
                {
                    LuaLibAPI.lua_pushlstring(state, new System.IntPtr((void*)p), (ulong)b.Length);
                }
            }
        }

        public static void PushObjectOntoStack(string d)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);

            if (d == null)
            {
                PushNil();
                return;
            }
            PushString(bLuaNative._state, d);
        }

        public static void PushNil()
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);

            LuaLibAPI.lua_pushnil(bLuaNative._state);
        }

        public static void LuaPushCFunction(LuaCFunction fn)
        {
            LuaLibAPI.lua_pushcclosure(bLuaNative._state, Marshal.GetFunctionPointerForDelegate(fn), 0);
        }

        public static void PushClosure(LuaCFunction fn, bLuaValue[] upvalues)
        {
            for (int i = 0; i != upvalues.Length; ++i)
            {
                PushStack(upvalues[i]);
            }

            LuaLibAPI.lua_pushcclosure(bLuaNative._state, Marshal.GetFunctionPointerForDelegate(fn), upvalues.Length);
        }

        public static void PushNewTable(int reserveArray = 0, int reserveTable = 0)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);

            LuaLibAPI.lua_createtable(bLuaNative._state, reserveArray, reserveTable);
        }

        public static void PushObjectOntoStack(object obj)
        {
            bLuaValue dynValue = obj as bLuaValue;
            if (dynValue != null)
            {
                PushStack(dynValue);
                return;
            }

            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);

            if (obj == null)
            {
                LuaLibAPI.lua_pushnil(bLuaNative._state);
            }
            else if (obj is int)
            {
                LuaLibAPI.lua_pushinteger(bLuaNative._state, (int)obj);
            }
            else if (obj is double)
            {
                LuaLibAPI.lua_pushnumber(bLuaNative._state, (double)obj);
            }
            else if (obj is float)
            {
                LuaLibAPI.lua_pushnumber(bLuaNative._state, (double)(float)obj);
            }
            else if (obj is bool)
            {
                LuaLibAPI.lua_pushboolean(bLuaNative._state, ((bool)obj) ? 1 : 0);
            }
            else if (obj is string)
            {
                PushString(bLuaNative._state, (string)obj);
            }
            else if (obj is LuaCFunction)
            {
                LuaPushCFunction(obj as LuaCFunction);
            }
            else
            {
                LuaLibAPI.lua_pushnil(bLuaNative._state);
                bLuaNative.Error($"Unrecognized object pushing onto stack: {obj.GetType().ToString()}");
            }
        }

        public static int PushStack(bLuaValue val)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);

            if (val == null)
            {
                PushNil();
                return (int)DataType.Nil;
            }

            return LuaLibAPI.lua_rawgeti(bLuaNative._state, LUA_REGISTRYINDEX, val.refid);
        }
        #endregion // Push (Stack)

        #region Pop (Stack)
        public static void LuaPop(System.IntPtr state, int n)
        {
            LuaLibAPI.lua_settop(state, -(n) - 1);
        }

        public static bLuaValue PopStackIntoValue()
        {
            int t = LuaLibAPI.lua_type(bLuaNative._state, -1);
            switch (t)
            {
                case (int)DataType.Nil:
                    LuaPop(bLuaNative._state, 1);
                    return bLuaValue.Nil;

                case (int)DataType.Boolean:
                    int val = LuaLibAPI.lua_toboolean(bLuaNative._state, -1);
                    LuaPop(bLuaNative._state, 1);
                    return val != 0 ? bLuaValue.True : bLuaValue.False;

                default:
                    //pops the value on top of the stack and makes a reference to it.
                    int refid = LuaXLibAPI.luaL_ref(bLuaNative._state, LUA_REGISTRYINDEX);
                    return new bLuaValue(refid);
            }
        }

        public static object PopStackIntoObject()
        {
            DataType t = (DataType)LuaLibAPI.lua_type(bLuaNative._state, -1);
            switch (t)
            {
                case DataType.Nil:
                    PopStack();
                    return bLuaValue.Nil;
                case DataType.Boolean:
                    return PopBool();
                case DataType.Number:
                    return PopNumber();
                case DataType.String:
                    return PopString();
                default:
                    return PopStackIntoValue();
            }
        }

        public static double PopNumber()
        {
            double result = LuaLibAPI.lua_tonumberx(bLuaNative._state, -1, System.IntPtr.Zero);
            LuaPop(bLuaNative._state, 1);
            return result;
        }

        public static int PopInteger()
        {
            int result = LuaLibAPI.lua_tointegerx(bLuaNative._state, -1, System.IntPtr.Zero);
            LuaPop(bLuaNative._state, 1);
            return result;
        }

        public static bool PopBool()
        {
            int result = LuaLibAPI.lua_toboolean(bLuaNative._state, -1);
            LuaPop(bLuaNative._state, 1);
            return result != 0;
        }

        public static string PopString()
        {
            string result = GetString(bLuaNative._state, -1);
            LuaPop(bLuaNative._state, 1);
            return result;
        }

        public static List<bLuaValue> PopList()
        {
            int len = (int)LuaLibAPI.lua_rawlen(bLuaNative._state, -1);
            List<bLuaValue> result = new List<bLuaValue>(len);

            LuaLibAPI.lua_checkstack(bLuaNative._state, 2);

            for (int i = 1; i <= len; ++i)
            {
                LuaLibAPI.lua_geti(bLuaNative._state, -1, i);
                result.Add(PopStackIntoValue());
            }

            //we're actually popping the list off.
            LuaPop(bLuaNative._state, 1);

            return result;
        }

        public static List<string> PopListOfStrings()
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 2);

            int len = (int)LuaLibAPI.lua_rawlen(bLuaNative._state, -1);
            List<string> result = new List<string>(len);

            for (int i = 1; i <= len; ++i)
            {
                int t = LuaLibAPI.lua_geti(bLuaNative._state, -1, i);
                if (t == (int)DataType.String)
                {
                    result.Add(PopString());
                }
                else
                {
                    PopStack();
                }
            }

            //we're actually popping the list off.
            LuaPop(bLuaNative._state, 1);

            return result;
        }

        public static Dictionary<string, bLuaValue> PopDict()
        {
            Dictionary<string, bLuaValue> result = new Dictionary<string, bLuaValue>();
            LuaLibAPI.lua_pushnil(bLuaNative._state);
            while (LuaLibAPI.lua_next(bLuaNative._state, -2) != 0)
            {
                if (LuaLibAPI.lua_type(bLuaNative._state, -2) != (int)DataType.String)
                {
                    LuaPop(bLuaNative._state, 1);
                    continue;
                }

                string key = GetString(bLuaNative._state, -2);
                result.Add(key, PopStackIntoValue());
            }

            //pop the table off the stack.
            LuaPop(bLuaNative._state, 1);

            return result;
        }

        public static List<bLuaValue.Pair> PopFullDict()
        {
            List<bLuaValue.Pair> result = new List<bLuaValue.Pair>();
            LuaLibAPI.lua_pushnil(bLuaNative._state);
            while (LuaLibAPI.lua_next(bLuaNative._state, -2) != 0)
            {
                var val = PopStackIntoValue();
                var key = PopStackIntoValue();
                PushStack(key);

                result.Add(new bLuaValue.Pair()
                {
                    Key = key,
                    Value = val,
                });
            }

            //pop the table off the stack.
            LuaPop(bLuaNative._state, 1);

            return result;
        }

        public static bool PopTableHasNonInts()
        {
            LuaLibAPI.lua_pushnil(bLuaNative._state);
            while (LuaLibAPI.lua_next(bLuaNative._state, -2) != 0)
            {
                var val = PopStackIntoValue();

                if (LuaLibAPI.lua_type(bLuaNative._state, -1) != (int)DataType.String)
                {
                    //pop key, value, and table.
                    LuaPop(bLuaNative._state, 3);
                    return true;
                }

                //just pop value, key goes with next.
                LuaPop(bLuaNative._state, 1);
            }

            //pop the table off the stack.
            LuaPop(bLuaNative._state, 1);

            return false;
        }

        public static bool PopTableEmpty()
        {
            LuaLibAPI.lua_pushnil(bLuaNative._state);

            bool result = (LuaLibAPI.lua_next(bLuaNative._state, -2) == 0);
            LuaPop(bLuaNative._state, result ? 1 : 3); //if empty pop just the table, otherwise the table and the key/value pair.

            return result;
        }

        public static void PopStack()
        {
            LuaPop(bLuaNative._state, 1);
        }
        #endregion // Pop (Stack)

        #region New Values
        public static bLuaValue NewMetaTable(string tname)
        {
            LuaXLibAPI.luaL_newmetatable(bLuaNative._state, tname);
            return PopStackIntoValue();
        }

        public static bLuaValue NewBoolean(bool val)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);
            LuaLibAPI.lua_pushboolean(bLuaNative._state, val ? 1 : 0);
            return PopStackIntoValue();
        }

        public static bLuaValue NewNumber(double val)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);
            LuaLibAPI.lua_pushnumber(bLuaNative._state, val);
            return PopStackIntoValue();
        }

        public static bLuaValue NewString(string val)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);
            PushString(bLuaNative._state, val);
            return PopStackIntoValue();
        }
        #endregion // New Values

        #region Tables
        public static bLuaValue GetTable(bLuaValue tbl, string key)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            DataType t = (DataType)LuaLibAPI.lua_gettable(bLuaNative._state, -2);
            var result = PopStackIntoValue(); //pop the result value out.
            result.dataType = t;
            PopStack(); //pop the table out and discard it.
            return result;
        }

        public static bLuaValue GetTable(bLuaValue tbl, object key)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            DataType t = (DataType)LuaLibAPI.lua_gettable(bLuaNative._state, -2);
            var result = PopStackIntoValue(); //pop the result value out.
            result.dataType = t;
            PopStack(); //pop the table out and discard it.
            return result;
        }

        public static bLuaValue RawGetTable(bLuaValue tbl, string key)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            DataType t = (DataType)LuaLibAPI.lua_rawget(bLuaNative._state, -2);
            var result = PopStackIntoValue(); //pop the result value out.
            result.dataType = t;
            PopStack(); //pop the table out and discard it.
            return result;
        }

        public static bLuaValue RawGetTable(bLuaValue tbl, object key)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            DataType t = (DataType)LuaLibAPI.lua_rawget(bLuaNative._state, -2);
            var result = PopStackIntoValue(); //pop the result value out.
            result.dataType = t;
            PopStack(); //pop the table out and discard it.
            return result;
        }

        public static void SetTable(bLuaValue tbl, bLuaValue key, bLuaValue val)
        {
            PushStack(tbl);
            PushStack(key);
            PushStack(val);
            LuaLibAPI.lua_settable(bLuaNative._state, -3);
            PopStack();
        }

        public static void SetTable(bLuaValue tbl, string key, bLuaValue val)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            PushStack(val);
            LuaLibAPI.lua_settable(bLuaNative._state, -3);
            PopStack();
        }

        public static void SetTable(bLuaValue tbl, string key, object val)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            PushObjectOntoStack(val);
            LuaLibAPI.lua_settable(bLuaNative._state, -3);
            PopStack();
        }

        public static void SetTable(bLuaValue tbl, object key, object val)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            PushObjectOntoStack(val);
            LuaLibAPI.lua_settable(bLuaNative._state, -3);
            PopStack();
        }
        #endregion // Tables

        #region Arrays
        public static int Length(bLuaValue val)
        {
            PushStack(val);
            uint result = LuaLibAPI.lua_rawlen(bLuaNative._state, -1);
            PopStack();

            return (int)result;
        }

        //index -- remember, 1-based!
        public static bLuaValue Index(bLuaValue val, int index)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 3);
            PushStack(val);
            LuaLibAPI.lua_geti(bLuaNative._state, -1, index);
            var result = PopStackIntoValue();
            PopStack();
            return result;
        }

        public static void SetIndex(bLuaValue array, int index, bLuaValue newVal)
        {
            PushStack(array);
            PushStack(newVal);
            LuaLibAPI.lua_seti(bLuaNative._state, -2, index);
            PopStack();
        }

        public static void AppendArray(bLuaValue array, bLuaValue newVal)
        {
            PushStack(array);
            int len = (int)LuaLibAPI.lua_rawlen(bLuaNative._state, -1);
            PushStack(newVal);
            LuaLibAPI.lua_seti(bLuaNative._state, -2, len + 1);
            PopStack();
        }

        public static void AppendArray(bLuaValue array, object newVal)
        {
            PushStack(array);
            int len = (int)LuaLibAPI.lua_rawlen(bLuaNative._state, -1);
            PushObjectOntoStack(newVal);
            LuaLibAPI.lua_seti(bLuaNative._state, -2, len + 1);
            PopStack();
        }
        #endregion // Arrays
    }
} // bLua.NativeLua namespace
