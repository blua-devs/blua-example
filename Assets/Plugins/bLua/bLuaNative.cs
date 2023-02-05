using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Threading;
using System.Threading.Tasks;
using bLua.NativeLua;
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
    public enum FeatureFlags
    {
        None = 0,
        Coroutines = 1,
        CSharpGC = 2, // C#-managed garbage collection
    }

    public static class bLuaNative
    {
        delegate void TickDelegate();
        static TickDelegate TickHandler;

        public static FeatureFlags features = FeatureFlags.Coroutines;


        /// <summary>
        /// Ends the Lua script
        /// </summary>
        public static void Close()
        {
            if (_state != System.IntPtr.Zero)
            {
                LuaLibAPI.lua_close(_state);
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
                int result = LuaXLibAPI.luaL_loadbufferx(_state, text, (ulong)text.Length, name, null);
                if (result != 0)
                {
                    string msg = Lua.GetString(_state, -1);
                    Lua.LuaPop(_state, 1);
                    throw new LuaException(msg);
                }

                using (s_profileLuaCallInner.Auto())
                {
                    result = LuaLibAPI.lua_pcallk(_state, 0, nresults, 0, 0, System.IntPtr.Zero);
                }

                if (result != 0)
                {
                    string msg = Lua.GetString(_state, -1);
                    Lua.LuaPop(_state, 1);
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
            return Lua.PopStackIntoValue();
        }

        /// <summary>
        /// Calls a passed Lua function.
        /// </summary>
        /// <param name="fn">The Lua function being called.</param>
        /// <param name="args">Arguments that will be passed into the called Lua function.</param>
        /// <returns>The output from the called Lua function.</returns>
        public static bLuaValue Call(bLuaValue fn, params object[] args)
        {
            using (s_profileLuaCall.Auto())
            {
                Lua.PushStack(fn);

                foreach (var arg in args)
                {
                    Lua.PushObjectOntoStack(arg);
                }

                int result;
                //TODO set the error handler to get the stack trace.
                using (s_profileLuaCallInner.Auto())
                {
                    result = LuaLibAPI.lua_pcallk(_state, args.Length, 1, 0, 0L, System.IntPtr.Zero);
                }
                if (result != 0)
                {
                    string error = Lua.GetString(_state, -1);
                    Lua.LuaPop(_state, 1);
                    bLuaNative.Error($"Error in function call: {error}");
                    throw new LuaException(error);
                }

                return Lua.PopStackIntoValue();
            }
        }

        public static System.IntPtr _state;

        static Dictionary<string, bLuaValue> _lookups = new Dictionary<string, bLuaValue>();
        static public bLuaValue FullLookup(bLuaValue obj, string key)
        {
            bLuaValue fn;
            if (_lookups.TryGetValue(key, out fn) == false)
            {
                fn = DoBuffer("lookup", $"return function(obj) return obj.{key} end");
                _lookups.Add(key, fn);
            }

            return Call(fn, obj);
        }

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

            _state = LuaXLibAPI.luaL_newstate();

            LuaXLibAPI.luaL_openlibs(_state);

            //hide these libraries.
            LuaLibAPI.lua_pushnil(_state);
            LuaLibAPI.lua_setglobal(_state, "io");

            bLuaUserData.Init();

            SetGlobal("blua", bLuaValue.CreateUserData(new bLuaGlobalLibrary()));

            if (features.HasFlag(FeatureFlags.Coroutines))
            {
                TickHandler += TickCoroutines;

                DoString(@"builtin_coroutines = {}");

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
            }
            if (features.HasFlag(FeatureFlags.CSharpGC))
            {
                TickHandler += TickGarbageCollection;

                _forcegc = DoString(@"return function() collectgarbage() end");
            }

            //initialize true and false.
            LuaLibAPI.lua_pushboolean(_state, 1);

            //pops the value on top of the stack and makes a reference to it.
            int refid = LuaXLibAPI.luaL_ref(_state, Lua.LUA_REGISTRYINDEX);
            bLuaValue.True = new bLuaValue(refid);

            LuaLibAPI.lua_pushboolean(_state, 0);

            //pops the value on top of the stack and makes a reference to it.
            refid = LuaXLibAPI.luaL_ref(_state, Lua.LUA_REGISTRYINDEX);
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

            bLuaValue.DeInit();
            bLuaUserData.DeInit();

            initialized = false;
        }

        #region Tick
        static public int tickDelay = 10; // 10 = 100 ticks per second
        static bool _ticking = false;


        async static void Tick()
        {
            if (_ticking)
            {
                return;
            }

            _ticking = true;
            while (_ticking)
            {
                TickHandler.Invoke();

                await Task.Delay(tickDelay);
            }
        }
        #endregion // Tick

        #region Garbage Collection
        static bLuaValue _forcegc = null;
        static float _lastgc = 0.0f;


        static void TickGarbageCollection()
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
            while (bLuaValue.deleteQueue.TryDequeue(out refid))
            {
                Lua.DestroyDynValue(refid);
            }
        }
        #endregion // Garbage Collection

        #region Coroutines
        struct ScheduledCoroutine
        {
            public bLuaValue fn;
            public object[] args;
            public int debugTag;
        }

        static bLuaValue _callco = null;
        static bLuaValue _updateco = null;

        static List<ScheduledCoroutine> _scheduledCoroutines = new List<ScheduledCoroutine>();

        static int _ncoroutine = 0;


        static void TickCoroutines()
        {
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
        }

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
                LuaLibAPI.lua_getglobal(bLuaNative._state, "builtin_coroutines");
                int len = (int)LuaLibAPI.lua_rawlen(bLuaNative._state, -1);
                Lua.PopStack();
                return len;
            }
        }

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
        #endregion // Coroutines

        #region Globals
        public static bLuaValue GetGlobal(string key)
        {
            int resType = LuaLibAPI.lua_getglobal(bLuaNative._state, key);
            var result = Lua.PopStackIntoValue();
            result.dataType = (bLua.DataType)resType;
            return result;
        }

        public static void SetGlobal(string key, bLuaValue val)
        {
            Lua.PushStack(val);
            LuaLibAPI.lua_setglobal(bLuaNative._state, key);
        }
        #endregion // Globals

        #region Errors
        public class LuaException : System.Exception
        {
            public LuaException(string message) : base(message)
            {

            }
        }

        public static void Error(string message, string engineTrace = null)
        {
            string msg = Lua.TraceMessage(message);
            if (engineTrace != null)
            {
                msg += "\n\n---\nEngine error details:\n" + engineTrace;
            }

            Debug.LogError(msg);
        }
        #endregion // Errors
    }
} // bLua namespace
