using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;
using System.Runtime.InteropServices;
using bLua;

#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR


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
        STOP =              0,
        RESTART =           1,
        COLLECT =           2,
        COUNT =             3,
        COUNTB =            4,
        STEP =              5,
        SETPAUSE =          6,
        SETSTEPMUL =        7,
        ISRUNNING =         9,
        GEN =               10,
        INC =               11,
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(bLuaNative))]
public class bLuaNativeEditor : Editor
{
    enum EditorAction
    {
        RunTest,
        RunBenchmark,
        RunTestCoroutine
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Run Unit Tests"))
        {
            bLuaNative.instance.RunUnitTests();
        }
        if (GUILayout.Button("Run Unit Tests (w/ Benchmark)"))
        {
            bLuaNative.instance.RunBenchmark();
        }
        if (GUILayout.Button("Run Test Coroutine"))
        {
            bLuaNative.instance.RunTestCoroutine();
        }
    }
}
#endif // UNITY_EDITOR

public class bLuaNative : MonoBehaviour
{
    public static void Error(string message, string engineTrace=null)
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

    public static bLuaNative instance;

    public static Script script = null;

    public class Script
    {
        public Script()
        {
            _state = luaL_newstate();

            luaL_openlibs(_state);

            //hide these libraries.
            lua_pushnil(_state);
            lua_setglobal(_state, "io");
        }

        ~Script()
        {
            Close();
        }

        public void Close()
        {
            if (_state != System.IntPtr.Zero)
            {
                lua_close(_state);
                _state = System.IntPtr.Zero;
            }
        }

        public bLuaValue DoString(string code)
        {
            return DoBuffer("code", code);
        }
        public void ExecBuffer(string name, string text, int nresults=0)
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

        public bLua.bLuaValue DoBuffer(string name, string text)
        {
            ExecBuffer(name, text, 1);
            return PopStackIntoValue();
        }

        public bLua.bLuaValue Call(bLua.bLuaValue fn, params object[] args)
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

        public System.IntPtr _state;
    }

    public class LuaException : System.Exception
    {
        public LuaException(string message) : base(message)
        {

        }
    }

    Dictionary<string, bLua.bLuaValue> _lookups = new Dictionary<string, bLua.bLuaValue>(); 
    public bLua.bLuaValue FullLookup(bLua.bLuaValue obj, string key)
    {
        bLua.bLuaValue fn;
        if (_lookups.TryGetValue(key, out fn) == false)
        {
            fn = script.DoBuffer("lookup", $"return function(obj) return obj.{key} end");
            _lookups.Add(key, fn);
        }

        return script.Call(fn, obj);
    }

    static public void DestroyDynValue(int refid)
    {
        if (instance != null)
        {
            //remove the value from the registry.
            luaL_unref(script._state, LUA_REGISTRYINDEX, refid);
        }
    }

    static public bLua.DataType InspectTypeOnTopOfStack()
    {
        return (bLua.DataType)lua_type(script._state, -1);
    }

    static public void PushObjectOntoStack(bool b)
    {
        lua_checkstack(script._state, 1);

        lua_pushboolean(script._state, b ? 1 : 0);
    }

    static public void PushObjectOntoStack(double d)
    {
        lua_checkstack(script._state, 1);

        lua_pushnumber(script._state, d);
    }

    static public void PushObjectOntoStack(float d)
    {
        lua_checkstack(script._state, 1);

        lua_pushnumber(script._state, (double)d);
    }

    static public void PushObjectOntoStack(int d)
    {
        lua_checkstack(script._state, 1);

        lua_pushinteger(script._state, d);
    }

    static public void PushObjectOntoStack(string d)
    {
        lua_checkstack(script._state, 1);

        if (d == null)
        {
            PushNil();
            return;
        }
        lua_pushstring(script._state, d);
    }

    static public void PushNil()
    {
        lua_checkstack(script._state, 1);

        lua_pushnil(script._state);
    }

    static public void PushNewTable(int reserveArray=0, int reserveTable=0)
    {
        lua_checkstack(script._state, 1);

        lua_createtable(script._state, reserveArray, reserveTable);
    }

    static public void PushObjectOntoStack(object obj)
    {
        bLua.bLuaValue dynValue = obj as bLua.bLuaValue;
        if (dynValue != null)
        {
            PushStack(dynValue);
            return;
        }

        lua_checkstack(script._state, 1);

        if (obj == null)
        {
            lua_pushnil(script._state);
        }
        else if (obj is int)
        {
            lua_pushinteger(script._state, (int)obj);
        } else if (obj is double)
        {
            lua_pushnumber(script._state, (double)obj);
        } else if (obj is float)
        {
            lua_pushnumber(script._state, (double)(float)obj);
        } else if (obj is bool)
        {
            lua_pushboolean(script._state, ((bool)obj) ? 1 : 0);
        } else if (obj is string)
        {
            lua_pushstring(script._state, (string)obj);
        } else if (obj is LuaCFunction)
        {
            lua_pushcfunction(obj as LuaCFunction);
        } else
        {
            lua_pushnil(script._state);
            bLuaNative.Error($"Unrecognized object pushing onto stack: {obj.GetType().ToString()}");
        }
    }

    static public int PushStack(bLua.bLuaValue val)
    {
        lua_checkstack(script._state, 1);

        if (val == null)
        {
            PushNil();
            return (int)DataType.Nil;
        }

        return lua_rawgeti(script._state, LUA_REGISTRYINDEX, val.refid);
    }

    static public object PopStackIntoObject()
    {
        DataType t = (DataType)lua_type(script._state, -1);
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
        double result = lua_tonumberx(script._state, -1, System.IntPtr.Zero);
        lua_pop(script._state, 1);
        return result;
    }

    static public int PopInteger()
    {
        int result = lua_tointegerx(script._state, -1, System.IntPtr.Zero);
        lua_pop(script._state, 1);
        return result;
    }

    static public bool PopBool()
    {
        int result = lua_toboolean(script._state, -1);
        lua_pop(script._state, 1);
        return result != 0;
    }


    static public string PopString()
    {
        string result = lua_getstring(script._state, -1);
        lua_pop(script._state, 1);
        return result;
    }

    static public List<bLua.bLuaValue> PopList()
    {
        int len = (int)lua_rawlen(script._state, -1);
        List<bLuaValue> result = new List<bLuaValue>(len);

        lua_checkstack(script._state, 2);

        for (int i = 1; i <= len; ++i)
        {
            lua_geti(script._state, -1, i);
            result.Add(PopStackIntoValue());
        }

        //we're actually popping the list off.
        lua_pop(script._state, 1);

        return result;
    }

    static public List<string> PopListOfStrings()
    {
        lua_checkstack(script._state, 2);

        int len = (int)lua_rawlen(script._state, -1);
        List<string> result = new List<string>(len);

        for (int i = 1; i <= len; ++i)
        {
            int t = lua_geti(script._state, -1, i);
            if (t == (int)DataType.String)
            {
                result.Add(PopString());
            } else
            {
                PopStack();
            }
        }

        //we're actually popping the list off.
        lua_pop(script._state, 1);

        return result;
    }


    static public Dictionary<string, bLuaValue> PopDict()
    {
        Dictionary<string, bLuaValue> result = new Dictionary<string, bLuaValue>();
        lua_pushnil(script._state);
        while(lua_next(script._state, -2) != 0)
        {
            if (lua_type(script._state, -2) != (int)DataType.String)
            {
                lua_pop(script._state, 1);
                continue;
            }

            string key = lua_getstring(script._state, -2);
            result.Add(key, PopStackIntoValue());
        }

        //pop the table off the stack.
        lua_pop(script._state, 1);

        return result;
    }

    static public List<bLuaValue.Pair> PopFullDict()
    {
        List<bLuaValue.Pair> result = new List<bLuaValue.Pair>();
        lua_pushnil(script._state);
        while (lua_next(script._state, -2) != 0)
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
        lua_pop(script._state, 1);

        return result;
    }

    static public bool PopTableHasNonInts()
    {
        lua_pushnil(script._state);
        while (lua_next(script._state, -2) != 0)
        {
            var val = PopStackIntoValue();

            if (lua_type(script._state, -1) != (int)DataType.String)
            {
                //pop key, value, and table.
                lua_pop(script._state, 3);
                return true;
            }

            //just pop value, key goes with next.
            lua_pop(script._state, 1);
        }

        //pop the table off the stack.
        lua_pop(script._state, 1);

        return false;
    }

    static public bool PopTableEmpty()
    {
        lua_pushnil(script._state);

        bool result = (lua_next(script._state, -2) == 0);
        lua_pop(script._state, result ? 1 : 3); //if empty pop just the table, otherwise the table and the key/value pair.

        return result;
    }

    static public bLua.bLuaValue GetGlobal(string key)
    {
        int resType = lua_getglobal(script._state, key);
        var result = PopStackIntoValue();
        result.dataType = (bLua.DataType)resType;
        return result;
    }

    static public void SetGlobal(string key, bLuaValue val)
    {
        PushStack(val);
        lua_setglobal(script._state, key);
    }

    static public void PopStack()
    {
        lua_pop(script._state, 1);
    }

    static public bLua.bLuaValue NewBoolean(bool val)
    {
        lua_checkstack(script._state, 1);
        lua_pushboolean(script._state, val ? 1 : 0);
        return PopStackIntoValue();
    }

    static public bLua.bLuaValue NewNumber(double val)
    {
        lua_checkstack(script._state, 1);
        lua_pushnumber(script._state, val);
        return PopStackIntoValue();
    }

    static public bLua.bLuaValue NewString(string val)
    {
        lua_checkstack(script._state, 1);
        lua_pushstring(script._state, val);
        return PopStackIntoValue();
    }

    static public bLua.bLuaValue PopStackIntoValue()
    {
        int t = lua_type(script._state, -1);
        switch (t)
        {
            case (int)DataType.Nil:
                lua_pop(script._state, 1);
                return bLuaValue.Nil;
            case (int)DataType.Boolean:
                {
                    int val = lua_toboolean(script._state, -1);
                    lua_pop(script._state, 1);
                    return val != 0 ? bLuaValue.True : bLuaValue.False;
                }

            default:
                {
                    //pops the value on top of the stack and makes a reference to it.
                    int refid = luaL_ref(script._state, LUA_REGISTRYINDEX);

                    return new bLua.bLuaValue(refid);
                }
        }

    }

    static public bLuaValue GetTable(bLuaValue tbl, string key)
    {
        PushStack(tbl);
        PushObjectOntoStack(key);
        DataType t = (DataType)lua_gettable(script._state, -2);
        var result = PopStackIntoValue(); //pop the result value out.
        result.dataType = t;
        PopStack(); //pop the table out and discard it.
        return result;
    }

    static public bLuaValue GetTable(bLuaValue tbl, object key)
    {
        PushStack(tbl);
        PushObjectOntoStack(key);
        DataType t = (DataType)lua_gettable(script._state, -2);
        var result = PopStackIntoValue(); //pop the result value out.
        result.dataType = t;
        PopStack(); //pop the table out and discard it.
        return result;
    }

    static public bLuaValue RawGetTable(bLuaValue tbl, string key)
    {
        PushStack(tbl);
        PushObjectOntoStack(key);
        DataType t = (DataType)lua_rawget(script._state, -2);
        var result = PopStackIntoValue(); //pop the result value out.
        result.dataType = t;
        PopStack(); //pop the table out and discard it.
        return result;
    }


    static public bLuaValue RawGetTable(bLuaValue tbl, object key)
    {
        PushStack(tbl);
        PushObjectOntoStack(key);
        DataType t = (DataType)lua_rawget(script._state, -2);
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
        lua_settable(script._state, -3);
        PopStack();
    }

    static public void SetTable(bLuaValue tbl, string key, bLuaValue val)
    {
        PushStack(tbl);
        PushObjectOntoStack(key);
        PushStack(val);
        lua_settable(script._state, -3);
        PopStack();
    }

    static public void SetTable(bLuaValue tbl, string key, object val)
    {
        PushStack(tbl);
        PushObjectOntoStack(key);
        PushObjectOntoStack(val);
        lua_settable(script._state, -3);
        PopStack();
    }

    static public void SetTable(bLuaValue tbl, object key, object val)
    {
        PushStack(tbl);
        PushObjectOntoStack(key);
        PushObjectOntoStack(val);
        lua_settable(script._state, -3);
        PopStack();
    }


    static public int Length(bLua.bLuaValue val)
    {
        PushStack(val);
        uint result = lua_rawlen(script._state, -1);
        PopStack();

        return (int)result;
    }

    //index -- remember, 1-based!
    static public bLuaValue Index(bLua.bLuaValue val, int index)
    {
        lua_checkstack(script._state, 3);
        PushStack(val);
        lua_geti(script._state, -1, index);
        var result = PopStackIntoValue();
        PopStack();
        return result;
    }

    static public void SetIndex(bLuaValue array, int index, bLuaValue newVal)
    {
        PushStack(array);
        PushStack(newVal);
        lua_seti(script._state, -2, index);
        PopStack();
    }

    static public void AppendArray(bLuaValue array, bLuaValue newVal)
    {
        PushStack(array);
        int len = (int)lua_rawlen(script._state, -1);
        PushStack(newVal);
        lua_seti(script._state, -2, len+1);
        PopStack();
    }

    static public void AppendArray(bLuaValue array, object newVal)
    {
        PushStack(array);
        int len = (int)lua_rawlen(script._state, -1);
        PushObjectOntoStack(newVal);
        lua_seti(script._state, -2, len+1);
        PopStack();
    }

    public static string TraceMessage(string message=null, int level=1)
    {
        if (message == null)
        {
            message = "stack";
        }
        lua_checkstack(script._state, 1);

        luaL_traceback(script._state, script._state, message, level);
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
    static extern void lua_pushnil (System.IntPtr state);
    [DllImport(_dllName)]
    static extern void lua_pushnumber (System.IntPtr state, double n);
    [DllImport(_dllName)]
    static extern void lua_pushinteger (System.IntPtr state, int n);

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

    [DllImport(_dllName, CharSet=CharSet.Ansi)]
    static extern void lua_pushlstring (System.IntPtr state, System.IntPtr s, ulong len);
    [DllImport(_dllName)]
    static extern void lua_pushboolean (System.IntPtr state, int b);

    [DllImport(_dllName)]
    public static extern void lua_xmove (System.IntPtr state, System.IntPtr to, int n);

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
        lua_pushcclosure(script._state, Marshal.GetFunctionPointerForDelegate(fn), 0);
    }
    
    public static void PushClosure(LuaCFunction fn, bLuaValue[] upvalues)
    {
        for (int i = 0; i != upvalues.Length; ++i)
        {
            PushStack(upvalues[i]);
        }

        lua_pushcclosure(script._state, Marshal.GetFunctionPointerForDelegate(fn), upvalues.Length);
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
        luaL_newmetatable(script._state, tname);
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

    bLuaValue _forcegc = null;
    float _lastgc = 0.0f;

    bLuaValue _callco = null;
    bLuaValue _updateco = null;

    static public Unity.Profiling.ProfilerMarker s_profileLuaGC = new Unity.Profiling.ProfilerMarker("Lua.GC");
    static public Unity.Profiling.ProfilerMarker s_profileLuaCo = new Unity.Profiling.ProfilerMarker("Lua.Coroutine");
    static public Unity.Profiling.ProfilerMarker s_profileLuaCall = new Unity.Profiling.ProfilerMarker("Lua.Call");
    static public Unity.Profiling.ProfilerMarker s_profileLuaCallInner = new Unity.Profiling.ProfilerMarker("Lua.CallInner");


#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            Init();
        }
    }
#endif // UNITY_EDITOR

    void Start()
    {
        Init();
    }

    void Update()
    {
        if (Time.time > _lastgc + 10.0f)
        {
            using (s_profileLuaGC.Auto())
            {
                Debug.Log("Run garbage collection");
                bLuaNative.script.Call(_forcegc);
            }
            _lastgc = Time.time;
        }

        int refid;
        while (bLua.bLuaValue.deleteQueue.TryDequeue(out refid))
        {
            DestroyDynValue(refid);
        }

        using (s_profileLuaCo.Auto())
        {
            script.Call(_updateco);

            while (_scheduledCoroutines.Count > 0)
            {
                var co = _scheduledCoroutines[0];
                _scheduledCoroutines.RemoveAt(0);

                CallCoroutine(co.fn, co.args);
            }
        }
    }

    private void OnDestroy()
    {
        DeInit();
    }

    public void Init()
    {
        instance = this;

        if (script != null)
        {
            script.Close();
        }
        bLuaNative.script = new Script();

        _forcegc = bLuaNative.script.DoString(@"return function() collectgarbage() end");

        bLuaNative.script.DoString(@"builtin_coroutines = {}");

        lua_pushcfunction(Luaprint);
        lua_setglobal(script._state, "print");

        _callco = bLuaNative.script.DoBuffer("callco", @"return function(fn, a, b, c, d, e, f, g, h)
    local co = coroutine.create(fn)
    local res, error = coroutine.resume(co, a, b, c, d, e, f, g, h)
    print('COROUTINE:: call co: %s -> %s -> %s', type(co), type(fn), coroutine.status(co))
    if not res then
        print(string.format('error in co-routine: %s', error))
    end
    if coroutine.status(co) ~= 'dead' then
        builtin_coroutines[#builtin_coroutines+1] = co
    end
end");

        _updateco = bLuaNative.script.DoBuffer("updateco", @"return function()
    local allRunning = true
    for _,co in ipairs(builtin_coroutines) do
        local res, error = coroutine.resume(co)
        if not res then
            print(string.format('error in co-routine: %s', error))
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
        lua_pushboolean(script._state, 1);

        //pops the value on top of the stack and makes a reference to it.
        int refid = luaL_ref(script._state, LUA_REGISTRYINDEX);
        bLuaValue.True = new bLuaValue(refid);

        lua_pushboolean(script._state, 0);

        //pops the value on top of the stack and makes a reference to it.
        refid = luaL_ref(script._state, LUA_REGISTRYINDEX);
        bLuaValue.False = new bLuaValue(refid);

        bLua.bLuaUserData.Init();
    }

    public void DeInit()
    {
        if (script != null)
        {
            script.Close();
            script = null;
        }

        instance = null;
    }

    struct ScheduledCoroutine
    {
        public bLuaValue fn;
        public object[] args;
        public int debugTag;
    }

    List<ScheduledCoroutine> _scheduledCoroutines = new List<ScheduledCoroutine>();

    static int _ncoroutine = 0;
    public void ScheduleCoroutine(bLuaValue fn, params object[] args)
    {
        ++_ncoroutine;
        _scheduledCoroutines.Add(new ScheduledCoroutine()
        {
            fn = fn,
            args = args,
            debugTag = _ncoroutine,
        });
    }

    public int numRunningCoroutines
    {
        get
        {
            lua_getglobal(bLuaNative.script._state, "builtin_coroutines");
            int len = (int)lua_rawlen(bLuaNative.script._state, -1);
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

    public static int Luaprint(System.IntPtr state)
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

    public void CallCoroutine(bLuaValue fn, params object[] args)
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

        script.Call(_callco, a);
    }

#if UNITY_EDITOR
    public void RunBenchmark()
    {
        RunUnitTests(true);
    }

    public void RunUnitTests(bool benchmarks=false)
    {
        int stackSize = lua_gettop(script._state);

        bLuaNative.script.ExecBuffer("main", @"
print('hello world')
MyFunctions = {
}

function MyFunctions.blah(y)
    return y
end

function myfunction(x)
    return x + 5
end

function lotsofargs(a,b,c,d,e,f,g)
    return a+b+c+d+e+f+g
end

function make_table()
    return {
        xyz = 5,
        abc = 9,
        def = 7,
    }
end

function add_from_table(options)
    return options.a + options.b + options.c
end


function make_big_table()
    local result = {}
    for i=1,1000 do
        result[tostring(i)] = i
    end
    return result
end

function test_userdata(a)
    return a.StaticFunction(0) + a.StaticFunction(2,3,4) + a:MyFunction() + a.propertyTest
end

function incr_userdata(a)
    a.propertyTest = a.propertyTest + 1
end

function test_addstrings(x, a, b)
    return x:AddStrings(a,b)
end

function test_field(x)
    x.n = x.n + 2
    return x.n
end

function test_varargs(x)
    x:VarArgsParamsFunction(3, x, x, 4, x)
    return x:VarArgsFunction(2, 3, 4, 5, 6)
end

function test_classproperty(x, n)
    return x.Create(n).n
end");

        using (bLua.bLuaValue fn = GetGlobal("myfunction"))
        {
            var result = script.Call(fn, 8);
            Assert.AreEqual(result.Number, 13.0);
        }

        using (bLua.bLuaValue fn = FullLookup(GetGlobal("MyFunctions"), "blah"))
        {
            Assert.AreEqual(script.Call(fn, 12).Number, 12.0);

            Assert.AreEqual(lua_gettop(script._state), stackSize);
        }

        using (bLua.bLuaValue fn = GetGlobal("make_table"))
        {
            bLuaValue t = script.Call(fn);
            Dictionary<string, bLuaValue> tab = t.Dict();
            Assert.AreEqual(tab.Count, 3);
            Assert.AreEqual(tab["abc"].Number, 9);
        }

        using (bLua.bLuaValue fn = GetGlobal("add_from_table"))
        {
            bLuaValue v = bLuaValue.CreateTable();
            v.Set("a", bLuaValue.CreateNumber(4));
            v.Set("b", bLuaValue.CreateNumber(5));
            v.Set("c", bLuaValue.CreateNumber(6));
            bLuaValue t = fn.Call(v);
            Assert.AreEqual(t.Number, 15);
        }

        using (bLuaValue fn = bLuaValue.CreateFunction(TestCFunction))
        {
            Assert.AreEqual(fn.Call().Number, 5);
        }

        using (bLuaValue fn = GetGlobal("test_userdata"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClass() { n = 7 });
            Assert.AreEqual(fn.Call(userdata).Number, 40);
            using (bLuaValue fn2 = GetGlobal("incr_userdata"))
            {
                fn2.Call(userdata);
                Assert.AreEqual(fn.Call(userdata).Number, 42);
            }
        }

        using (bLuaValue fn = GetGlobal("test_addstrings"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClass() { n = 7 });
            Assert.AreEqual(fn.Call(userdata, "abc:", bLuaValue.CreateString("def")).String, "abc:def");

        }

        using (bLuaValue fn = GetGlobal("test_varargs"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClass() { n = 7 });
            Assert.AreEqual(fn.Call(userdata).Number, 20);

        }


        using (bLuaValue fn = GetGlobal("test_field"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClass() { n = 7 });
            Assert.AreEqual(fn.Call(userdata).Number, 9.0);
        }

        using (bLuaValue fn = GetGlobal("test_field"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClassDerived() { n = 7 });
            Assert.AreEqual(fn.Call(userdata).Number, 9.0);
        }

        using (bLuaValue fn = GetGlobal("test_classproperty"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClassDerived() { n = 7 });
            Assert.AreEqual(fn.Call(userdata, 7.0).Number, 7.0);
        }

        Debug.Log("Lua: Ran unit tests");

        if (benchmarks)
        {

            Benchmark("Noop", () => { });
            Benchmark("GetTop", () => lua_gettop(script._state));

            using (bLua.bLuaValue fn = GetGlobal("myfunction"))
            {
                Benchmark("Call", () => script.Call(fn, 8));
            }

            using (bLua.bLuaValue fn = GetGlobal("lotsofargs"))
            {
                Benchmark("CallManyArgs", () => script.Call(fn, 8, 2, 3, 4, 5, 6, 2));
            }

            Benchmark("GetGlobal", () => GetGlobal("MyFunctions"));
            Benchmark("FullLookup", () => FullLookup(GetGlobal("MyFunctions"), "blah"));

            using (bLuaValue t = script.Call(GetGlobal("make_table")))
            {
                Benchmark("GetDict", () => bLuaValue.RunDispose(t.Dict()));
            }

            using (bLuaValue t = script.Call(GetGlobal("make_big_table")))
            {
                Benchmark("GetBigDict", () => bLuaValue.RunDispose(t.Dict()));
            }

            using (bLuaValue fn = GetGlobal("make_big_table"))
            {
                Benchmark("MakeBigTable", () => script.Call(fn));
            }
            Debug.Log("Ran benchmarks");
        }

        Assert.AreEqual(lua_gettop(script._state), stackSize);
    }

    void Benchmark(string name, System.Action fn)
    {
        fn();

        long niterations = 1;
        long elapsed = 0;

        while (elapsed < 10)
        {
            niterations *= 10;
            var sw = System.Diagnostics.Stopwatch.StartNew();

            for (long i = 0; i < niterations; ++i)
            {
                fn();
            }

            sw.Stop();
            elapsed = sw.ElapsedMilliseconds;
        }

        if (niterations >= 1000000)
        {
            double ns = elapsed * 1000000.0;
            ns /= niterations;
            Debug.Log($"BENCH: {name} did {niterations} in {elapsed}ms; {(int)ns}ns/iteration");
        } else if (niterations >= 1000)
        {
            double us = elapsed * 1000.0;
            us /= niterations;
            Debug.Log($"BENCH: {name} did {niterations} in {elapsed}ms; {(int)us}us/iteration");
        } else
        {
            double ms = elapsed;
            ms /= niterations;
            Debug.Log($"BENCH: {name} did {niterations} in {elapsed}ms; {(int)ms}ms/iteration");
        }
    }

    public void RunTestCoroutine()
    {
        lua_pushcfunction(Luaprint);
        lua_setglobal(script._state, "TestPrint");

        bLuaNative.script.ExecBuffer("co", @"function myco(a, b, c)
    for i=1,5 do
        print('co: ' .. i)

        coroutine.yield()
    end
end");

        using (bLua.bLuaValue fn = GetGlobal("myco"))
        {
            CallCoroutine(fn, 1);
            CallCoroutine(fn, 2);
        }
    }

    public static int TestCFunction(System.IntPtr state)
    {
        lua_pushinteger(state, 5);
        return 1;
    }
#endif // UNITY_EDITOR
}
