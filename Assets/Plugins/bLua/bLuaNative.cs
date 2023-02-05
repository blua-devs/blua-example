using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Threading;
using System.Threading.Tasks;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace bLua
{
    public enum DataType
    {
        Nil = 0,
        Boolean = 1,
        LightUserdata = 2,
        Number = 3,
        String = 4,
        Table = 5,
        Function = 6,
        UserData = 7,
        Thread = 8,
        Unknown = 9,
    }

    public enum GC_FLAG
    {
        STOP = 0,
        RESTART = 1,
        COLLECT = 2,
        COUNT = 3,
        COUNTB = 4,
        STEP = 5,
        SETPAUSE = 6,
        SETSTEPMUL = 7,
        ISRUNNING = 9,
        GEN = 10,
        INC = 11,
    }

    public static class bLuaNative
    {
        public static void Error(string message, string engineTrace = null)
        {
            string msg = TraceMessage(message);
            if (engineTrace != null)
            {
                msg += "\n\n---\nEngine error details:\n" + engineTrace;
            }

            Debug.LogError(msg);
        }

        public static int LUA_TNONE = -1;
        public static int LUAI_MAXSTACK = 1000000;
        public static int LUA_REGISTRYINDEX = (-LUAI_MAXSTACK - 1000);

        public static int lua_upvalueindex(int i)
        {
            return LUA_REGISTRYINDEX - i;
        }

        /// <summary>
        /// Ends the Lua script
        /// </summary>
        public static void Close()
        {
            if (_state != System.IntPtr.Zero)
            {
                lua_close(_state);
                _state = System.IntPtr.Zero;
            }
        }

        /// <summary>
        /// Loads a string of Lua code and runs it.
        /// </summary>
        public static bLuaValue DoString(string code)
        {
            return DoBuffer("code", code);
        }

        /// <summary>
        /// Loads a buffer as a Lua chunk and runs it.
        /// </summary>
        /// <param name="name">The chunk name, used for debug information and error messages.</param>
        /// <param name="text">The Lua code to load.</param>
        public static void ExecBuffer(string name, string text, int nresults = 0)
        {
            using (s_profileLuaCall.Auto())
            {
                int result = luaL_loadbufferx(_state, text, (ulong)text.Length, name, null);
                if (result != 0)
                {
                    string msg = lua_getstring(_state, -1);
                    lua_pop(_state, 1);
                    throw new LuaException(msg);
                }

                using (s_profileLuaCallInner.Auto())
                {
                    result = lua_pcallk(_state, 0, nresults, 0, 0, System.IntPtr.Zero);
                }

                if (result != 0)
                {
                    string msg = lua_getstring(_state, -1);
                    lua_pop(_state, 1);
                    throw new LuaException(msg);
                }
            }
        }

        /// <summary>
        /// Loads a buffer as a Lua chunk and runs it.
        /// </summary>
        /// <param name="name">The chunk name, used for debug information and error messages.</param>
        /// <param name="text">The Lua code to load.</param>
        public static bLuaValue DoBuffer(string name, string text)
        {
            ExecBuffer(name, text, 1);
            return PopStackIntoValue();
        }

        /// <summary>
        /// Calls a passed Lua function.
        /// </summary>
        /// <param name="fn">The Lua function being called.</param>
        /// <param name="args">Arguments that will be passed into the called Lua function.</param>
        /// <returns>The output from the called Lua function.</returns>
        public static bLuaValue Call(bLua.bLuaValue fn, params object[] args)
        {
            using (s_profileLuaCall.Auto())
            {
                PushStack(fn);

                foreach (var arg in args)
                {
                    PushObjectOntoStack(arg);
                }

                int result;
                //TODO set the error handler to get the stack trace.
                using (s_profileLuaCallInner.Auto())
                {
                    result = lua_pcallk(_state, args.Length, 1, 0, 0L, System.IntPtr.Zero);
                }
                if (result != 0)
                {
                    string error = lua_getstring(_state, -1);
                    lua_pop(_state, 1);
                    bLuaNative.Error($"Error in function call: {error}");
                    throw new LuaException(error);
                }

                return PopStackIntoValue();
            }
        }

        public static System.IntPtr _state;

        public class LuaException : System.Exception
        {
            public LuaException(string message) : base(message)
            {

            }
        }

        static Dictionary<string, bLua.bLuaValue> _lookups = new Dictionary<string, bLua.bLuaValue>();
        static public bLua.bLuaValue FullLookup(bLua.bLuaValue obj, string key)
        {
            bLua.bLuaValue fn;
            if (_lookups.TryGetValue(key, out fn) == false)
            {
                fn = DoBuffer("lookup", $"return function(obj) return obj.{key} end");
                _lookups.Add(key, fn);
            }

            return Call(fn, obj);
        }

        static public void DestroyDynValue(int refid)
        {
            if (_state != System.IntPtr.Zero)
            {
                //remove the value from the registry.
                luaL_unref(_state, LUA_REGISTRYINDEX, refid);
            }
        }

        static public bLua.DataType InspectTypeOnTopOfStack()
        {
            return (bLua.DataType)lua_type(_state, -1);
        }

        static public void PushObjectOntoStack(bool b)
        {
            lua_checkstack(_state, 1);

            lua_pushboolean(_state, b ? 1 : 0);
        }

        static public void PushObjectOntoStack(double d)
        {
            lua_checkstack(_state, 1);

            lua_pushnumber(_state, d);
        }

        static public void PushObjectOntoStack(float d)
        {
            lua_checkstack(_state, 1);

            lua_pushnumber(_state, (double)d);
        }

        static public void PushObjectOntoStack(int d)
        {
            lua_checkstack(_state, 1);

            lua_pushinteger(_state, d);
        }

        static public void PushObjectOntoStack(string d)
        {
            lua_checkstack(_state, 1);

            if (d == null)
            {
                PushNil();
                return;
            }
            lua_pushstring(_state, d);
        }

        static public void PushNil()
        {
            lua_checkstack(_state, 1);

            lua_pushnil(_state);
        }

        static public void PushNewTable(int reserveArray = 0, int reserveTable = 0)
        {
            lua_checkstack(_state, 1);

            lua_createtable(_state, reserveArray, reserveTable);
        }

        static public void PushObjectOntoStack(object obj)
        {
            bLua.bLuaValue dynValue = obj as bLua.bLuaValue;
            if (dynValue != null)
            {
                PushStack(dynValue);
                return;
            }

            lua_checkstack(_state, 1);

            if (obj == null)
            {
                lua_pushnil(_state);
            }
            else if (obj is int)
            {
                lua_pushinteger(_state, (int)obj);
            }
            else if (obj is double)
            {
                lua_pushnumber(_state, (double)obj);
            }
            else if (obj is float)
            {
                lua_pushnumber(_state, (double)(float)obj);
            }
            else if (obj is bool)
            {
                lua_pushboolean(_state, ((bool)obj) ? 1 : 0);
            }
            else if (obj is string)
            {
                lua_pushstring(_state, (string)obj);
            }
            else if (obj is LuaCFunction)
            {
                lua_pushcfunction(obj as LuaCFunction);
            }
            else
            {
                lua_pushnil(_state);
                bLuaNative.Error($"Unrecognized object pushing onto stack: {obj.GetType().ToString()}");
            }
        }

        static public int PushStack(bLua.bLuaValue val)
        {
            lua_checkstack(_state, 1);

            if (val == null)
            {
                PushNil();
                return (int)DataType.Nil;
            }

            return lua_rawgeti(_state, LUA_REGISTRYINDEX, val.refid);
        }

        static public object PopStackIntoObject()
        {
            DataType t = (DataType)lua_type(_state, -1);
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

        static public double PopNumber()
        {
            double result = lua_tonumberx(_state, -1, System.IntPtr.Zero);
            lua_pop(_state, 1);
            return result;
        }

        static public int PopInteger()
        {
            int result = lua_tointegerx(_state, -1, System.IntPtr.Zero);
            lua_pop(_state, 1);
            return result;
        }

        static public bool PopBool()
        {
            int result = lua_toboolean(_state, -1);
            lua_pop(_state, 1);
            return result != 0;
        }


        static public string PopString()
        {
            string result = lua_getstring(_state, -1);
            lua_pop(_state, 1);
            return result;
        }

        static public List<bLua.bLuaValue> PopList()
        {
            int len = (int)lua_rawlen(_state, -1);
            List<bLuaValue> result = new List<bLuaValue>(len);

            lua_checkstack(_state, 2);

            for (int i = 1; i <= len; ++i)
            {
                lua_geti(_state, -1, i);
                result.Add(PopStackIntoValue());
            }

            //we're actually popping the list off.
            lua_pop(_state, 1);

            return result;
        }

        static public List<string> PopListOfStrings()
        {
            lua_checkstack(_state, 2);

            int len = (int)lua_rawlen(_state, -1);
            List<string> result = new List<string>(len);

            for (int i = 1; i <= len; ++i)
            {
                int t = lua_geti(_state, -1, i);
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
            lua_pop(_state, 1);

            return result;
        }


        static public Dictionary<string, bLuaValue> PopDict()
        {
            Dictionary<string, bLuaValue> result = new Dictionary<string, bLuaValue>();
            lua_pushnil(_state);
            while (lua_next(_state, -2) != 0)
            {
                if (lua_type(_state, -2) != (int)DataType.String)
                {
                    lua_pop(_state, 1);
                    continue;
                }

                string key = lua_getstring(_state, -2);
                result.Add(key, PopStackIntoValue());
            }

            //pop the table off the stack.
            lua_pop(_state, 1);

            return result;
        }

        static public List<bLuaValue.Pair> PopFullDict()
        {
            List<bLuaValue.Pair> result = new List<bLuaValue.Pair>();
            lua_pushnil(_state);
            while (lua_next(_state, -2) != 0)
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
            lua_pop(_state, 1);

            return result;
        }

        static public bool PopTableHasNonInts()
        {
            lua_pushnil(_state);
            while (lua_next(_state, -2) != 0)
            {
                var val = PopStackIntoValue();

                if (lua_type(_state, -1) != (int)DataType.String)
                {
                    //pop key, value, and table.
                    lua_pop(_state, 3);
                    return true;
                }

                //just pop value, key goes with next.
                lua_pop(_state, 1);
            }

            //pop the table off the stack.
            lua_pop(_state, 1);

            return false;
        }

        static public bool PopTableEmpty()
        {
            lua_pushnil(_state);

            bool result = (lua_next(_state, -2) == 0);
            lua_pop(_state, result ? 1 : 3); //if empty pop just the table, otherwise the table and the key/value pair.

            return result;
        }

        static public bLua.bLuaValue GetGlobal(string key)
        {
            int resType = lua_getglobal(_state, key);
            var result = PopStackIntoValue();
            result.dataType = (bLua.DataType)resType;
            return result;
        }

        static public void SetGlobal(string key, bLuaValue val)
        {
            PushStack(val);
            lua_setglobal(_state, key);
        }

        static public void PopStack()
        {
            lua_pop(_state, 1);
        }

        static public bLua.bLuaValue NewBoolean(bool val)
        {
            lua_checkstack(_state, 1);
            lua_pushboolean(_state, val ? 1 : 0);
            return PopStackIntoValue();
        }

        static public bLua.bLuaValue NewNumber(double val)
        {
            lua_checkstack(_state, 1);
            lua_pushnumber(_state, val);
            return PopStackIntoValue();
        }

        static public bLua.bLuaValue NewString(string val)
        {
            lua_checkstack(_state, 1);
            lua_pushstring(_state, val);
            return PopStackIntoValue();
        }

        static public bLua.bLuaValue PopStackIntoValue()
        {
            int t = lua_type(_state, -1);
            switch (t)
            {
                case (int)DataType.Nil:
                    lua_pop(_state, 1);
                    return bLuaValue.Nil;
                case (int)DataType.Boolean:
                    {
                        int val = lua_toboolean(_state, -1);
                        lua_pop(_state, 1);
                        return val != 0 ? bLuaValue.True : bLuaValue.False;
                    }

                default:
                    {
                        //pops the value on top of the stack and makes a reference to it.
                        int refid = luaL_ref(_state, LUA_REGISTRYINDEX);

                        return new bLua.bLuaValue(refid);
                    }
            }

        }

        static public bLuaValue GetTable(bLuaValue tbl, string key)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            DataType t = (DataType)lua_gettable(_state, -2);
            var result = PopStackIntoValue(); //pop the result value out.
            result.dataType = t;
            PopStack(); //pop the table out and discard it.
            return result;
        }

        static public bLuaValue GetTable(bLuaValue tbl, object key)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            DataType t = (DataType)lua_gettable(_state, -2);
            var result = PopStackIntoValue(); //pop the result value out.
            result.dataType = t;
            PopStack(); //pop the table out and discard it.
            return result;
        }

        static public bLuaValue RawGetTable(bLuaValue tbl, string key)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            DataType t = (DataType)lua_rawget(_state, -2);
            var result = PopStackIntoValue(); //pop the result value out.
            result.dataType = t;
            PopStack(); //pop the table out and discard it.
            return result;
        }


        static public bLuaValue RawGetTable(bLuaValue tbl, object key)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            DataType t = (DataType)lua_rawget(_state, -2);
            var result = PopStackIntoValue(); //pop the result value out.
            result.dataType = t;
            PopStack(); //pop the table out and discard it.
            return result;
        }



        static public void SetTable(bLuaValue tbl, bLuaValue key, bLuaValue val)
        {
            PushStack(tbl);
            PushStack(key);
            PushStack(val);
            lua_settable(_state, -3);
            PopStack();
        }

        static public void SetTable(bLuaValue tbl, string key, bLuaValue val)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            PushStack(val);
            lua_settable(_state, -3);
            PopStack();
        }

        static public void SetTable(bLuaValue tbl, string key, object val)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            PushObjectOntoStack(val);
            lua_settable(_state, -3);
            PopStack();
        }

        static public void SetTable(bLuaValue tbl, object key, object val)
        {
            PushStack(tbl);
            PushObjectOntoStack(key);
            PushObjectOntoStack(val);
            lua_settable(_state, -3);
            PopStack();
        }


        static public int Length(bLua.bLuaValue val)
        {
            PushStack(val);
            uint result = lua_rawlen(_state, -1);
            PopStack();

            return (int)result;
        }

        //index -- remember, 1-based!
        static public bLuaValue Index(bLua.bLuaValue val, int index)
        {
            lua_checkstack(_state, 3);
            PushStack(val);
            lua_geti(_state, -1, index);
            var result = PopStackIntoValue();
            PopStack();
            return result;
        }

        static public void SetIndex(bLuaValue array, int index, bLuaValue newVal)
        {
            PushStack(array);
            PushStack(newVal);
            lua_seti(_state, -2, index);
            PopStack();
        }

        static public void AppendArray(bLuaValue array, bLuaValue newVal)
        {
            PushStack(array);
            int len = (int)lua_rawlen(_state, -1);
            PushStack(newVal);
            lua_seti(_state, -2, len + 1);
            PopStack();
        }

        static public void AppendArray(bLuaValue array, object newVal)
        {
            PushStack(array);
            int len = (int)lua_rawlen(_state, -1);
            PushObjectOntoStack(newVal);
            lua_seti(_state, -2, len + 1);
            PopStack();
        }

        public static string TraceMessage(string message = null, int level = 1)
        {
            if (message == null)
            {
                message = "stack";
            }
            lua_checkstack(_state, 1);

            luaL_traceback(_state, _state, message, level);
            return PopString();
        }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        const string _dllName = "lua54.dll";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
    const string _dllName = "Lua";
#endif

        [DllImport(_dllName)]
        public static extern void luaL_traceback(System.IntPtr state, System.IntPtr state2, string msg, int level);

        [DllImport(_dllName)]
        public static extern System.IntPtr luaL_newstate();


        [DllImport(_dllName)]
        public static extern System.IntPtr lua_close(System.IntPtr state);

        [DllImport(_dllName)]
        static extern void luaL_openlibs(System.IntPtr state);

        //push values onto stack.
        [DllImport(_dllName)]
        static extern void lua_pushnil(System.IntPtr state);
        [DllImport(_dllName)]
        static extern void lua_pushnumber(System.IntPtr state, double n);
        [DllImport(_dllName)]
        static extern void lua_pushinteger(System.IntPtr state, int n);

        static void lua_pushstring(System.IntPtr state, string s)
        {
            byte[] b = StrToUTF8(s);

            unsafe
            {
                fixed (byte* p = b)
                {
                    lua_pushlstring(state, new System.IntPtr((void*)p), (ulong)b.Length);
                }
            }
        }

        [DllImport(_dllName, CharSet = CharSet.Ansi)]
        static extern void lua_pushlstring(System.IntPtr state, System.IntPtr s, ulong len);
        [DllImport(_dllName)]
        static extern void lua_pushboolean(System.IntPtr state, int b);

        [DllImport(_dllName)]
        public static extern void lua_xmove(System.IntPtr state, System.IntPtr to, int n);

        //find type of value on stack.
        [DllImport(_dllName)]
        public static extern int lua_type(System.IntPtr state, int idx);


        //inspect values on stack.
        [DllImport(_dllName)]
        static extern int lua_toboolean(System.IntPtr state, int idx);

        [DllImport(_dllName)]
        public static extern double lua_tonumberx(System.IntPtr state, int n, System.IntPtr /*|int*|*/ isnum);

        [DllImport(_dllName)]
        public static extern int lua_tointegerx(System.IntPtr state, int n, System.IntPtr /*|int*|*/ isnum);

        [DllImport(_dllName)]
        static extern System.IntPtr lua_tolstring(System.IntPtr state, int n, StrLen /*|size_t*|*/ len);

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]

        class StrLen
        {
            public ulong len;
        }

        static StrLen s_strlen = new StrLen();

        public static string lua_getstring(System.IntPtr state, int n)
        {
            var ptr = lua_tolstring(state, n, s_strlen);
            byte[] bytes = new byte[s_strlen.len];
            Marshal.Copy(ptr, bytes, 0, (int)s_strlen.len);
            return UTF8ToStr(bytes);
        }

        [DllImport(_dllName)]
        static extern void lua_createtable(System.IntPtr state, int narray, int ntable);

        [DllImport(_dllName)]
        static extern int lua_geti(System.IntPtr state, int stack_index, int table_index);

        [DllImport(_dllName)]
        static extern void lua_seti(System.IntPtr state, int stack_index, int table_index);

        //returns the length of the string or table.
        [DllImport(_dllName)]
        static extern uint lua_rawlen(System.IntPtr state, int stack_index);

        [DllImport(_dllName)]
        static extern int lua_next(System.IntPtr state, int idx);

        //Does the equivalent to t[k] = v, where t is the value at the given index, v is the value on the top of the stack, and k is the value just below the top.
        [DllImport(_dllName)]
        static extern void lua_settable(System.IntPtr state, int idx);

        //Pushes onto the stack the value t[k], where t is the value at the given index and k is the value on the top of the stack.
        [DllImport(_dllName)]
        public static extern int lua_gettable(System.IntPtr state, int idx);

        [DllImport(_dllName)]
        public static extern int lua_rawget(System.IntPtr state, int idx);


        [DllImport(_dllName)]
        static extern int lua_getglobal(System.IntPtr state, string key);

        [DllImport(_dllName)]
        static extern void lua_setglobal(System.IntPtr state, string key);


        //returns a char*
        [DllImport(_dllName)]
        static extern System.IntPtr lua_typename(System.IntPtr state, int idx);


        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int LuaCFunction(System.IntPtr state);


        //void lua_pushcclosure (lua_State* L, lua_CFunction fn, int n);
        [DllImport(_dllName)]
        static extern void lua_pushcclosure(System.IntPtr state, System.IntPtr fn, int n);

        public static void lua_pushcfunction(LuaCFunction fn)
        {
            lua_pushcclosure(_state, Marshal.GetFunctionPointerForDelegate(fn), 0);
        }

        public static void PushClosure(LuaCFunction fn, bLuaValue[] upvalues)
        {
            for (int i = 0; i != upvalues.Length; ++i)
            {
                PushStack(upvalues[i]);
            }

            lua_pushcclosure(_state, Marshal.GetFunctionPointerForDelegate(fn), upvalues.Length);
        }

        [DllImport(_dllName)]
        public static extern System.IntPtr lua_newuserdatauv(System.IntPtr state, System.IntPtr sz, int nuvalue);

        [DllImport(_dllName)]
        public static extern int lua_getiuservalue(System.IntPtr state, int idx, int n);

        [DllImport(_dllName)]
        public static extern int lua_setiuservalue(System.IntPtr state, int idx, int n);

        [DllImport(_dllName)]
        public static extern int lua_setmetatable(System.IntPtr state, int objindex);

        [DllImport(_dllName)]
        public static extern int lua_getmetatable(System.IntPtr state, int objindex);

        [DllImport(_dllName)]
        static extern int luaL_newmetatable(System.IntPtr state, string tname);

        public static bLuaValue NewMetaTable(string tname)
        {
            luaL_newmetatable(_state, tname);
            return PopStackIntoValue();
        }

        [DllImport(_dllName)]
        static extern void luaL_setmetatable(System.IntPtr state, string tname);


        //int lua_pushthread (lua_State* L);

        [DllImport(_dllName)]
        static extern void lua_pushlightuserdata(System.IntPtr state, System.IntPtr addr);

        [DllImport(_dllName)]
        static extern void luaopen_base(System.IntPtr state);

        [DllImport(_dllName)]
        static extern void luaopen_coroutine(System.IntPtr state);

        [DllImport(_dllName)]
        static extern void luaopen_table(System.IntPtr state);

        [DllImport(_dllName)]
        static extern void luaopen_io(System.IntPtr state);

        [DllImport(_dllName)]
        static extern void luaopen_os(System.IntPtr state);

        [DllImport(_dllName)]
        static extern void luaopen_string(System.IntPtr state);

        [DllImport(_dllName)]
        static extern void luaopen_utf8(System.IntPtr state);

        [DllImport(_dllName)]
        static extern void luaopen_math(System.IntPtr state);

        [DllImport(_dllName)]
        static extern void luaopen_debug(System.IntPtr state);

        [DllImport(_dllName)]
        static extern void luaopen_package(System.IntPtr state);

        [DllImport(_dllName)]
        static extern int luaL_loadbufferx(System.IntPtr state, string buff, ulong sz, string name, string mode);

        [DllImport(_dllName)]
        static extern int lua_pcallk(System.IntPtr state, int nargs, int nresults, int msgh, long ctx, System.IntPtr k);

        [DllImport(_dllName)]
        public static extern int lua_resume(System.IntPtr state, System.IntPtr from, int nargs, bLua.CoroutineResult result);

        [DllImport(_dllName)]
        public static extern void lua_settop(System.IntPtr state, int n);

        public static void lua_pop(System.IntPtr state, int n)
        {
            lua_settop(state, -(n) - 1);
        }

        [DllImport(_dllName)]
        static extern void lua_rotate(System.IntPtr state, int idx, int n);

        [DllImport(_dllName)]
        public static extern int lua_gettop(System.IntPtr state);

        [DllImport(_dllName)]
        public static extern int lua_checkstack(System.IntPtr state, int n);


        //references
        [DllImport(_dllName)]
        public static extern int luaL_ref(System.IntPtr state, int t);

        [DllImport(_dllName)]
        public static extern void luaL_unref(System.IntPtr state, int t, int refIndex);

        [DllImport(_dllName)]
        public static extern int lua_rawgeti(System.IntPtr state, int idx, int n);

        [DllImport(_dllName)]
        public static extern int lua_rawequal(System.IntPtr state, int idx1, int idx2);

        [DllImport(_dllName)]
        public static extern int lua_compare(System.IntPtr state, int idx1, int idx2, int op);

        /// <summary> Interim control over the C#-managed Garbage Collection system </summary>
        static public bool manualGCEnabled = false;
        static bLuaValue _forcegc = null;
        static float _lastgc = 0.0f;

        static public int tickDelay = 10; // 10 = 100 ticks per second
        static bool _ticking = false;
        static bLuaValue _callco = null;
        static bLuaValue _updateco = null;

        static public Unity.Profiling.ProfilerMarker s_profileLuaGC = new Unity.Profiling.ProfilerMarker("Lua.GC");
        static public Unity.Profiling.ProfilerMarker s_profileLuaCo = new Unity.Profiling.ProfilerMarker("Lua.Coroutine");
        static public Unity.Profiling.ProfilerMarker s_profileLuaCall = new Unity.Profiling.ProfilerMarker("Lua.Call");
        static public Unity.Profiling.ProfilerMarker s_profileLuaCallInner = new Unity.Profiling.ProfilerMarker("Lua.CallInner");


        static bool initialized = false;
        public static void Init()
        {
#if UNITY_EDITOR
            if (!EditorApplication.isPlaying
                && initialized)
            {
                DeInit();
            }
#endif
            if (initialized)
            {
                return;
            }
            initialized = true;

            _state = luaL_newstate();

            luaL_openlibs(_state);

            //hide these libraries.
            lua_pushnil(_state);
            lua_setglobal(_state, "io");

            bLua.bLuaUserData.Init();

            _forcegc = DoString(@"return function() collectgarbage() end");

            DoString(@"builtin_coroutines = {}");

            SetGlobal("blua", bLuaValue.CreateUserData(new bLuaGlobalLibrary()));

            _callco = DoBuffer("callco", @"return function(fn, a, b, c, d, e, f, g, h)
    local co = coroutine.create(fn)
    local res, error = coroutine.resume(co, a, b, c, d, e, f, g, h)
    blua.print('COROUTINE:: call co: %s -> %s -> %s', type(co), type(fn), coroutine.status(co))
    if not res then
        blua.print(string.format('error in co-routine: %s', error))
    end
    if coroutine.status(co) ~= 'dead' then
        builtin_coroutines[#builtin_coroutines+1] = co
    end
end");

            _updateco = DoBuffer("updateco", @"return function()
    local allRunning = true
    for _,co in ipairs(builtin_coroutines) do
        local res, error = coroutine.resume(co)
        if not res then
            blua.print(string.format('error in co-routine: %s', error))
        end
        if coroutine.status(co) == 'dead' then
            allRunning = false
        end
    end

    if not allRunning then
        local new_coroutines = {}
        for _,co in ipairs(builtin_coroutines) do
            if coroutine.status(co) ~= 'dead' then
                new_coroutines[#new_coroutines+1] = co
            end
        end

        builtin_coroutines = new_coroutines
    end
end");

            //initialize true and false.
            lua_pushboolean(_state, 1);

            //pops the value on top of the stack and makes a reference to it.
            int refid = luaL_ref(_state, LUA_REGISTRYINDEX);
            bLuaValue.True = new bLuaValue(refid);

            lua_pushboolean(_state, 0);

            //pops the value on top of the stack and makes a reference to it.
            refid = luaL_ref(_state, LUA_REGISTRYINDEX);
            bLuaValue.False = new bLuaValue(refid);

            Tick();
        }

        public static void DeInit()
        {
            _ticking = false;

            Close();

            _lookups.Clear();
            _forcegc = null;
            _callco = null;
            _updateco = null;

            bLua.bLuaValue.DeInit();
            bLua.bLuaUserData.DeInit();

            initialized = false;
        }

        async static void Tick()
        {
            if (_ticking)
            {
                return;
            }

            _ticking = true;
            while (_ticking)
            {
                // Update Lua Coroutines
                using (s_profileLuaCo.Auto())
                {
                    Call(_updateco);

                    while (_scheduledCoroutines.Count > 0)
                    {
                        var co = _scheduledCoroutines[0];
                        _scheduledCoroutines.RemoveAt(0);

                        CallCoroutine(co.fn, co.args);
                    }
                }
                // End Update Lua Coroutines

                // Garbage Collection
                if (manualGCEnabled)
                {
                    if (bLuaGlobalLibrary.time > _lastgc + 10.0f)
                    {
                        using (s_profileLuaGC.Auto())
                        {
                            bLuaNative.Call(_forcegc);
                        }
                        _lastgc = bLuaGlobalLibrary.time;
                    }

                    int refid;
                    while (bLua.bLuaValue.deleteQueue.TryDequeue(out refid))
                    {
                        DestroyDynValue(refid);
                    }
                }
                // End Garbage Collection

                await Task.Delay(tickDelay);
            }
        }

        struct ScheduledCoroutine
        {
            public bLuaValue fn;
            public object[] args;
            public int debugTag;
        }

        static List<ScheduledCoroutine> _scheduledCoroutines = new List<ScheduledCoroutine>();

        static int _ncoroutine = 0;
        public static void ScheduleCoroutine(bLuaValue fn, params object[] args)
        {
            ++_ncoroutine;
            _scheduledCoroutines.Add(new ScheduledCoroutine()
            {
                fn = fn,
                args = args,
                debugTag = _ncoroutine,
            });
        }

        public static int numRunningCoroutines
        {
            get
            {
                lua_getglobal(bLuaNative._state, "builtin_coroutines");
                int len = (int)lua_rawlen(bLuaNative._state, -1);
                PopStack();
                return len;
            }
        }

        //some string read/write utils.
        static byte[] StrToUTF8(string s)
        {
            return System.Text.UTF8Encoding.UTF8.GetBytes(s);
        }

        static string UTF8ToStr(byte[] b)
        {
            return System.Text.UTF8Encoding.UTF8.GetString(b);
        }

        /*
        public static int LuaPrint(System.IntPtr state)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int top = lua_gettop(state);
            for (int i = 1; i <= top; ++i)
            {
                DataType t = (DataType)lua_type(state, i);
                switch (t)
                {
                    case DataType.Nil:
                        sb.Append("nil");
                        break;

                    case DataType.Boolean:
                        sb.Append((lua_toboolean(state, i) != 0) ? "true" : "false");
                        break;
                    case DataType.LightUserdata:
                        sb.Append("light user data");
                        break;
                    case DataType.Number:
                        sb.Append(lua_tonumberx(state, i, System.IntPtr.Zero));
                        break;
                    case DataType.String:
                        sb.Append(lua_getstring(state, i));
                        break;
                    case DataType.Table:
                        sb.Append("(table)");
                        break;
                    case DataType.Function:
                        sb.Append("(function)");
                        break;
                    case DataType.UserData:
                        sb.Append("(userdata)");
                        break;
                    case DataType.Thread:
                        sb.Append("(thread)");
                        break;
                }
            }

            Debug.Log($"LUA: {sb.ToString()}");
            return 0;
        }
        */

        public static void CallCoroutine(bLuaValue fn, params object[] args)
        {
            int nargs = args != null ? args.Length : 0;

            object[] a = new object[nargs + 1];
            a[0] = fn;
            if (nargs > 0)
            {
                for (int i = 0; i != args.Length; ++i)
                {
                    a[i + 1] = args[i];
                }
            }

            Call(_callco, a);
        }
    }
} // bLua namespace
