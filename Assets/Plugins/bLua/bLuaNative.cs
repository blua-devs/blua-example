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
    /// <summary> Lua data types. </summary>
    public enum DataType
    {
        Nil = 0,
        Boolean = 1,
        LightUserData = 2,
        Number = 3,
        String = 4,
        Table = 5,
        Function = 6,
        UserData = 7,
        Thread = 8,
        Unknown = 9,
    }

    /// <summary> Features are specific libraries, Lua boilerplate, or C# systems that improve upon vanilla Lua usage or functionality. </summary>
    [Flags]
    public enum Feature
    {
        /// <summary> No given features. </summary>
        None = 0,
        /// <summary> Basic Function (https://www.lua.org/manual/5.4/manual.html#6.1). </summary>
        BasicLibrary = 1,
        /// <summary> Lua Coroutine library and boilerplate Lua code for calling coroutines from C# (https://www.lua.org/manual/5.4/manual.html#6.2). </summary>
        CoroutineManipulation = 2,
        /// <summary> Contains the `package` library for loading modules via paths (https://www.lua.org/manual/5.4/manual.html#6.3). </summary>
        Packages = 4,
        /// <summary> String Manipulation (https://www.lua.org/manual/5.4/manual.html#6.4) </summary>
        StringManipulation = 8,
        /// <summary> UTF-8 Support (https://www.lua.org/manual/5.4/manual.html#6.5). </summary>
        UTF8Support = 16,
        /// <summary> Table Manipulation (https://www.lua.org/manual/5.4/manual.html#6.6). </summary>
        Tables = 32,
        /// <summary> Mathematical Functions (https://www.lua.org/manual/5.4/manual.html#6.7). </summary>
        MathLibrary = 64,
        /// <remarks> WARNING! IO access includes the ability to read and write files on the user's machine. </remarks>
        /// <summary> Input and Output Facilities (https://www.lua.org/manual/5.4/manual.html#6.8). </summary>
        IO = 128,
        /// <remarks> WARNING! OS access includes the ability to delete files on the user's machine as well as execute C functions on the operating system shell. </remarks>
        /// <summary> Operating System Facilities (https://www.lua.org/manual/5.4/manual.html#6.9). </summary>
        OS = 256,
        /// <remarks> WARNING! (From the Lua 5.4 manual:) "You should exert care when using this library. Several of its functions violate basic assumptions about 
        /// Lua code (e.g., that variables local to a function cannot be accessed from outside; that userdata metatables cannot be changed by Lua code; that Lua 
        /// programs do not crash) and therefore can compromise otherwise secure code." </remarks>
        /// <summary> The Debug Library (https://www.lua.org/manual/5.4/manual.html#6.10). </summary>
        Debug = 512,
        /// <summary> By default includes a library of helpful functions that can be accessed via `blua.` in Lua code. </summary>
        bLuaGlobalLibrary = 1024,
        /// <remarks> WARNING! Known play mode issues with bLua (C#-managed) garbage collection! Lua has built in GC handling for a vast majority of uses. </remarks>
        /// <summary> bLua (C#-managed) garbage collection. NOTE: Feature.BasicLibrary also needs to be enabled for this feature to work. </summary>
        CSharpGarbageCollection = 2048,
    }

    /// <summary> Sandboxes are groupings of features that let you select premade feature lists for your bLua environment. </summary>
    public enum Sandbox
    {
        /// <summary> No additional Lua or bLua features. </summary>
        None = Feature.None,
        /// <remarks> WARNING! Some of these features include developer warnings, please review the remarks on individual features. </remarks>
        /// <summary> Includes all of the features Lua and bLua have to offer. </summary>
        AllFeatures = Feature.BasicLibrary
            | Feature.CoroutineManipulation
            | Feature.Packages
            | Feature.StringManipulation
            | Feature.UTF8Support
            | Feature.Tables
            | Feature.MathLibrary
            | Feature.IO
            | Feature.OS
            | Feature.Debug
            | Feature.bLuaGlobalLibrary
            | Feature.CSharpGarbageCollection,
        /// <remarks> WARNING! Some of these features include developer warnings, please review the remarks on individual features. </remarks>
        /// <summary> Includes most Lua and bLua features, specifically ones that might be used commonly in modding. </summary>
        BasicModding = Feature.BasicLibrary
            | Feature.CoroutineManipulation
            | Feature.StringManipulation
            | Feature.UTF8Support
            | Feature.Tables
            | Feature.MathLibrary
            | Feature.IO
            | Feature.bLuaGlobalLibrary,
        /// <summary> Includes basic Lua and bLua features, avoiding ones that could be potentially used maliciously. </summary>
        Safe = Feature.BasicLibrary
            | Feature.CoroutineManipulation
            | Feature.StringManipulation
            | Feature.UTF8Support
            | Feature.Tables
            | Feature.MathLibrary
            | Feature.bLuaGlobalLibrary
    }

    public static class bLuaNative
    {
        /// <summary> The current Lua state (https://www.lua.org/manual/5.4/manual.html#lua_newstate). </summary>
        public static IntPtr _state;


        #region Initialization
        /// <summary> Whether or not bLua has been initialized. </summary>
        static bool initialized = false;


        /// <summary> Initialize Lua and handle enabling/disabled features based on the current sandbox. </summary>
        public static void Init()
        {
#if UNITY_EDITOR
            // If we're in edit mode and bLua has already been initialized, reinitialize it in case source code (include User Data) has changed
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

            // Create a new state for Lua
            _state = LuaXLibAPI.luaL_newstate();
            if (_state == null)
            {
                Debug.LogError("Created Lua State was null! Failed to initialize bLua.");
                return;
            }

            // Initialize true and false
            LuaLibAPI.lua_pushboolean(_state, 1);
            int refid = LuaXLibAPI.luaL_ref(_state, Lua.LUA_REGISTRYINDEX); // Pops the value on top of the stack and makes a reference to it.
            bLuaValue.True = new bLuaValue(refid);
            LuaLibAPI.lua_pushboolean(_state, 0);
            refid = LuaXLibAPI.luaL_ref(_state, Lua.LUA_REGISTRYINDEX); // Pops the value on top of the stack and makes a reference to it.
            bLuaValue.False = new bLuaValue(refid);

            // Initialize all bLua User Data
            bLuaUserData.Init();

            #region Feature Handling
            if (Feature.CSharpGarbageCollection.Enabled())
            {
                TickHandler += TickGarbageCollection;

                _forcegc = DoString(@"return function() collectgarbage() end");
            }

            if (Feature.BasicLibrary.Enabled())
            {
                LuaLibAPI.luaopen_base(_state);
            }

            if (Feature.CoroutineManipulation.Enabled())
            {
                LuaLibAPI.luaopen_coroutine(_state);

                TickHandler += TickCoroutines;

                DoBuffer("builtin_coroutines", @"builtin_coroutines = {}");
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

            if (Feature.Packages.Enabled())
            {
                LuaLibAPI.luaopen_package(_state);
            }

            if (Feature.StringManipulation.Enabled())
            {
                LuaLibAPI.luaopen_string(_state);
            }

            if (Feature.UTF8Support.Enabled())
            {
                LuaLibAPI.luaopen_utf8(_state);
            }

            if (Feature.Tables.Enabled())
            {
                LuaLibAPI.luaopen_table(_state);
            }

            if (Feature.MathLibrary.Enabled())
            {
                LuaLibAPI.luaopen_math(_state);
            }

            if (Feature.IO.Enabled())
            {
                LuaLibAPI.luaopen_io(_state);
            }

            if (Feature.OS.Enabled())
            {
                LuaLibAPI.luaopen_os(_state);
            }

            if (Feature.Debug.Enabled())
            {
                LuaLibAPI.luaopen_debug(_state);
            }

            if (Feature.bLuaGlobalLibrary.Enabled())
            {
                SetGlobal("blua", bLuaValue.CreateUserData(new bLuaGlobalLibrary()));
            }
            #endregion // Feature Handling

            // Start the threading needed for running bLua without a MonoBehaviour
            Tick();
        }

        public static void DeInit()
        {
            _ticking = false;

            if (_state != IntPtr.Zero)
            {
                LuaLibAPI.lua_close(_state); // Closes the current Lua environment and releases all objects, threads, and dynamic memory
                _state = IntPtr.Zero;
            }

            _lookups.Clear();
            _forcegc = null;
            _callco = null;
            _updateco = null;

            bLuaUserData.DeInit();
            bLuaValue.DeInit();

            initialized = false;
        }
        #endregion // Initialization

        #region Feature Handling
        /// <summary> The selected sandbox (set of features) for bLua. </summary>
        static Sandbox sandbox = Sandbox.Safe;


        /// <summary> Returns true if the current sandbox has the passed feature enabled. </summary>
        public static bool FeatureEnabled(Feature _feature)
        {
            return _feature.Enabled();
        }

        /// <summary> Returns true if the current sandbox has the passed feature enabled. </summary>
        public static bool Enabled(this Feature _feature)
        {
            return ((Feature)(int)sandbox).HasFlag(_feature);
        }
        #endregion // Feature Handling

        #region Tick
        public delegate void TickDelegate();
        /// <summary> This delegate is called whenever bLua ticks. Allows for bLua features (or developers) to listen for when ticking takes place. </summary>
        public static TickDelegate TickHandler;

        /// <summary> The millisecond delay between bLua ticks. </summary>
        static public int tickDelay = 10; // 10 = 100 ticks per second

        /// <summary> Whether or not bLua is ticking. Set to false to close the ticking thread if it exists. </summary>
        static bool _ticking = false;


        async static void Tick()
        {
            // Don't have two instances of the Tick thread; if this value is already set, don't continue
            if (_ticking)
            {
                return;
            }
            _ticking = true;

            // Only continue ticking while this value is set. This allows us to close the tick thread from outside of it when we need to
            while (_ticking)
            {
                TickHandler();

                await Task.Delay(tickDelay);
            }
        }
        #endregion // Tick

        #region Garbage Collection
        /// <summary> Holds a Lua function that calls `collectgarbage`. </summary>
        static bLuaValue _forcegc = null;

        /// <summary> The timestamp of the last garbage collection call. </summary>
        static float _lastgc = 0.0f;


        static void TickGarbageCollection()
        {
            if (bLuaGlobalLibrary.time > _lastgc + 10.0f)
            {
                using (Lua.s_profileLuaGC.Auto())
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
            using (Lua.s_profileLuaCo.Auto())
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
        public delegate void LuaErrorDelegate(string message, string engineTrace = null);
        /// <summary> This delegate is called whenever a Lua Error happens. Allows for bLua features (or developers) to listen. </summary>
        public static LuaErrorDelegate LuaErrorHandler;


        public class LuaException : System.Exception
        {
            public LuaException(string message) : base(message)
            {

            }
        }


        public static void Error(string message, string engineTrace = null)
        {
            LuaErrorHandler(message, engineTrace);

            string msg = Lua.TraceMessage(message);
            if (engineTrace != null)
            {
                msg += "\n\n---\nEngine error details:\n" + engineTrace;
            }

            Debug.LogError(msg);
        }
        #endregion // Errors

        /// <summary> Loads a string of Lua code and runs it. </summary>
        /// <param name="code">The string of code to run.</param>
        public static bLuaValue DoString(string code)
        {
            return DoBuffer("code", code);
        }

        /// <summary> Loads a buffer as a Lua chunk and runs it. </summary>
        /// <param name="name">The chunk name, used for debug information and error messages.</param>
        /// <param name="text">The Lua code to load.</param>
        public static bLuaValue DoBuffer(string name, string text)
        {
            ExecBuffer(name, text, 1);
            return Lua.PopStackIntoValue();
        }

        /// <summary> Loads a buffer as a Lua chunk and runs it. </summary>
        /// <param name="name">The chunk name, used for debug information and error messages.</param>
        /// <param name="text">The Lua code to load.</param>
        public static void ExecBuffer(string name, string text, int nresults = 0)
        {
            using (Lua.s_profileLuaCall.Auto())
            {
                int result = LuaXLibAPI.luaL_loadbufferx(_state, text, (ulong)text.Length, name, null);
                if (result != 0)
                {
                    string msg = Lua.GetString(_state, -1);
                    Lua.LuaPop(_state, 1);
                    throw new LuaException(msg);
                }

                using (Lua.s_profileLuaCallInner.Auto())
                {
                    result = LuaLibAPI.lua_pcallk(_state, 0, nresults, 0, 0, IntPtr.Zero);
                }

                if (result != 0)
                {
                    string msg = Lua.GetString(_state, -1);
                    Lua.LuaPop(_state, 1);
                    throw new LuaException(msg);
                }
            }
        }

        /// <summary> Calls a passed Lua function. </summary>
        /// <param name="fn">The Lua function being called.</param>
        /// <param name="args">Arguments that will be passed into the called Lua function.</param>
        /// <returns>The output from the called Lua function.</returns>
        public static bLuaValue Call(bLuaValue fn, params object[] args)
        {
            using (Lua.s_profileLuaCall.Auto())
            {
                Lua.PushStack(fn);

                foreach (var arg in args)
                {
                    Lua.PushObjectOntoStack(arg);
                }

                int result;
                //TODO set the error handler to get the stack trace.
                using (Lua.s_profileLuaCallInner.Auto())
                {
                    result = LuaLibAPI.lua_pcallk(_state, args.Length, 1, 0, 0L, IntPtr.Zero);
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
    }
} // bLua namespace
