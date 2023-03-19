﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
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
        Coroutines = 2,
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
        /// <summary> Includes `wait(t)`, `spawn(fn)`, and `delay(t, fn)` functions that can be used for better threading and coroutine control in Lua. NOTE: 
        /// Feature.Coroutines needs to be enabled for this feature to work. </summary>
        ThreadMacros = 1024
    }

    /// <summary> Sandboxes are groupings of features that let you select premade feature lists for your bLua environment. </summary>
    public enum Sandbox
    {
        /// <summary> No additional Lua or bLua features. </summary>
        None = Feature.None,
        /// <remarks> WARNING! Some of these features include developer warnings, please review the remarks on individual features. </remarks>
        /// <summary> Includes all of the features Lua and bLua have to offer. </summary>
        AllFeatures = Feature.BasicLibrary
            | Feature.Coroutines
            | Feature.Packages
            | Feature.StringManipulation
            | Feature.UTF8Support
            | Feature.Tables
            | Feature.MathLibrary
            | Feature.IO
            | Feature.OS
            | Feature.Debug
            | Feature.ThreadMacros,
        /// <remarks> WARNING! Some of these features include developer warnings, please review the remarks on individual features. </remarks>
        /// <summary> Includes most Lua and bLua features, specifically ones that might be used commonly in modding. </summary>
        BasicModding = Feature.BasicLibrary
            | Feature.Coroutines
            | Feature.StringManipulation
            | Feature.UTF8Support
            | Feature.Tables
            | Feature.MathLibrary
            | Feature.IO
            | Feature.ThreadMacros,
        /// <summary> Includes basic Lua and bLua features, avoiding ones that could be potentially used maliciously. </summary>
        Safe = Feature.BasicLibrary
            | Feature.Coroutines
            | Feature.StringManipulation
            | Feature.UTF8Support
            | Feature.Tables
            | Feature.MathLibrary
            | Feature.ThreadMacros
    }

    /// <summary> Contains settings for the bLua runtime. </summary>
    public class bLuaSettings
    {
        /// <summary> The selected sandbox (set of features) for bLua. </summary>
        public Sandbox sandbox = Sandbox.Safe;

        public enum TickBehavior
        {
            /// <summary> Always tick at the tickInterval in the settings. </summary>
            AlwaysTick,
            /// <summary> Never tick automatically; use ManualTick() to tick instead. </summary>
            Manual,
            /// <summary> Tick at the tickInterval interval in the settings only when there is at least one active coroutines running in the instance. </summary>
            TickOnlyWhenCoroutinesActive
        }

        /// <summary> When true, bLua scripts will not tick automatically. You should call ManualTick() instead. </summary>
        public TickBehavior tickBehavior = TickBehavior.AlwaysTick;

        /// <summary> The millisecond delay between bLua ticks. </summary>
        public int tickInterval = 10; // 10 = 100 ticks per second

        /// <summary> When true, all user data will be registered with the bLua instance these settings are for. If this is false, you can
        /// still manually register all assemblies or individual user data classes as needed. </summary>
        public bool autoRegisterAllUserData = true;
    }

    public class bLuaInstance : IDisposable
    {
        static Dictionary<IntPtr, bLuaInstance> instanceRegistry = new Dictionary<IntPtr, bLuaInstance>();

        public static bLuaInstance GetInstanceByState(IntPtr _state)
        {
            bLuaInstance instance = null;
            instanceRegistry.TryGetValue(_state, out instance);
            return instance;
        }

        public static int GetInstanceCount()
        {
            return instanceRegistry.Count;
        }

        bLuaSettings settings = new bLuaSettings();

        /// <summary> Contains the current Lua state (https://www.lua.org/manual/5.4/manual.html#lua_newstate). </summary>
        public IntPtr state { get; private set; }

        public int s_stringCacheHit = 0, s_stringCacheMiss = 0;

        public StringCacheEntry[] s_stringCache = new StringCacheEntry[997];

        Dictionary<string, bLuaValue> s_internedStrings = new Dictionary<string, bLuaValue>();

        Dictionary<string, bLuaValue> lookups = new Dictionary<string, bLuaValue>();

        //whenever we add a method we just add it to this list to be indexed.
        public List<MethodCallInfo> s_methods = new List<MethodCallInfo>();
        public List<PropertyCallInfo> s_properties = new List<PropertyCallInfo>();
        public List<FieldCallInfo> s_fields = new List<FieldCallInfo>();


        public List<UserDataRegistryEntry> s_entries = new List<UserDataRegistryEntry>();
        public Dictionary<string, int> s_typenameToEntryIndex = new Dictionary<string, int>();
        public bLuaValue _gc;

        public object[] s_liveObjects = new object[65536];

        public List<int> s_liveObjectsFreeList = new List<int>();

        public int s_nNextLiveObject = 1;

        public int numLiveObjects
        {
            get
            {
                return (s_nNextLiveObject - 1) - s_liveObjectsFreeList.Count;
            }
        }


        public bLuaInstance()
        {
            settings = new bLuaSettings();
            Init();
        }

        public bLuaInstance(bLuaSettings _settings)
        {
            settings = _settings;
            Init();
        }

        ~bLuaInstance()
        {
            Dispose();
        }

        public void Dispose()
        {
            DeInit();
        }

        #region Initialization
        /// <summary> Whether or not bLua has been initialized. </summary>
        bool initialized = false;
        
        bool reinitializing = false;


        /// <summary> Initialize Lua and handle enabling/disabled features based on the current sandbox. </summary>
        void Init()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;

            SceneManager.activeSceneChanged -= OnActiveSceneChanged; // This can be done safely
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            // Create a new state for this instance
            state = LuaXLibAPI.luaL_newstate();
            instanceRegistry.Add(state, this);

            _gc = bLuaValue.CreateFunction(this, GCFunction);
            // Initialize all bLua User Data
            if (settings.autoRegisterAllUserData)
            {
                bLuaUserData.RegisterAllAssemblies(this);
            }

            // Setup the bLua Global Library
            bLuaUserData.Register(this, typeof(bLuaGlobalLibrary));
            SetGlobal("blua", bLuaValue.CreateUserData(this, new bLuaGlobalLibrary()));

            #region Feature Handling
            if (FeatureEnabled(Feature.BasicLibrary))
            {
                LuaLibAPI.luaopen_base(state);
            }

            if (FeatureEnabled(Feature.Coroutines))
            {
                LuaLibAPI.luaopen_coroutine(state);
                LuaLibAPI.lua_setglobal(state, "coroutine");

                if (OnTick != null)
                {
                    OnTick.RemoveListener(TickCoroutines);
                    OnTick.AddListener(TickCoroutines);
                }

                DoBuffer("builtin_coroutines", @"builtin_coroutines = {}");
                callco = DoBuffer("callco",
                    @"return function(fn, a, b, c, d, e, f, g, h)
                        local co = coroutine.create(fn)
                        local res, error = coroutine.resume(co, a, b, c, d, e, f, g, h)
                        --blua.print('COROUTINE:: call co: %s -> %s -> %s', type(co), type(fn), coroutine.status(co))
                        if not res then
                            blua.print(string.format('error in co-routine: %s', error))
                        end
                        if coroutine.status(co) ~= 'dead' then
                            builtin_coroutines[#builtin_coroutines+1] = co
                        end
                    end");
                updateco = DoBuffer("updateco",
                    @"return function()
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
                cancelcos = DoBuffer("cancelcos",
                    @"return function()
                        for _,co in ipairs(builtin_coroutines) do
                            local res, error = coroutine.close(co)
                            if not res then
                                blua.print(string.format('error closing co-routine: %s', error))
                            end
                        end
                        builtin_coroutines = {}
                    end");
            }

            if (FeatureEnabled(Feature.Packages))
            {
                LuaLibAPI.luaopen_package(state);
                LuaLibAPI.lua_setglobal(state, "package");
            }

            if (FeatureEnabled(Feature.StringManipulation))
            {
                LuaLibAPI.luaopen_string(state);
                LuaLibAPI.lua_setglobal(state, "string");
            }

            if (FeatureEnabled(Feature.UTF8Support))
            {
                LuaLibAPI.luaopen_utf8(state);
                LuaLibAPI.lua_setglobal(state, "utf8");
            }

            if (FeatureEnabled(Feature.Tables))
            {
                LuaLibAPI.luaopen_table(state);
                LuaLibAPI.lua_setglobal(state, "table");
            }

            if (FeatureEnabled(Feature.MathLibrary))
            {
                LuaLibAPI.luaopen_math(state);
                LuaLibAPI.lua_setglobal(state, "math");
            }

            if (FeatureEnabled(Feature.IO))
            {
                LuaLibAPI.luaopen_io(state);
                LuaLibAPI.lua_setglobal(state, "io");
            }

            if (FeatureEnabled(Feature.OS))
            {
                LuaLibAPI.luaopen_os(state);
                LuaLibAPI.lua_setglobal(state, "os");
            }

            if (FeatureEnabled(Feature.Debug))
            {
                LuaLibAPI.luaopen_debug(state);
                LuaLibAPI.lua_setglobal(state, "debug");
            }

            if (FeatureEnabled(Feature.ThreadMacros))
            {
                DoBuffer("thread_macros",
                    @"function wait(t)
                        local startTime = blua.time
                        while blua.time < startTime + t do
                            coroutine.yield()
                        end
                    end

                    function spawn(fn, a, b, c, d, e, f, g, h)
                        local co = coroutine.create(fn)
                        local res, error = coroutine.resume(co, a, b, c, d, e, f, g, h)
                        if not res then
                            blua.print(string.format('error in co-routine spawn: %s', error))
                        end
                        if coroutine.status(co) ~= 'dead' then
                            builtin_coroutines[#builtin_coroutines+1] = co
                        end
                    end

                    function delay(t, fn)
                        spawn(function()
                            wait(t)
                            fn()
                        end)
                    end");
            }
            #endregion // Feature Handling

            if (settings.tickBehavior != bLuaSettings.TickBehavior.Manual)
            {
                // Start the threading needed for running bLua without a MonoBehaviour
                StartTicking();
            }
        }

        void DeInit()
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;

            OnTick.RemoveAllListeners();
            StopTicking();

            instanceRegistry.Remove(state);
            if (state != IntPtr.Zero)
            {
                LuaLibAPI.lua_close(state); // Closes the current Lua environment and releases all objects, threads, and dynamic memory
            }
            state = IntPtr.Zero;

            initialized = false;

            if (!reinitializing)
            {
                GC.ReRegisterForFinalize(this);
            }
        }

        void ReInit()
        {
            reinitializing = true;
            DeInit();
            Init();
            reinitializing = false;
        }

        void OnActiveSceneChanged(Scene _previous, Scene _new)
        {
            if (_previous.IsValid())
            {
                Dispose();
            }
        }
        #endregion // Initialization

        #region Tick
        /// <summary> This event is called whenever bLua ticks. Allows for bLua features (or developers) to listen for when ticking takes place. </summary>
        public UnityEvent OnTick = new UnityEvent();

        bool ticking = false;
        bool requestStartTicking = false;
        bool requestStopTicking = false;


        public void ManualTick()
        {
            InternalTick();
        }

        async void Tick()
        {
            // Don't have two instances of the Tick thread; if this value is already set, don't continue
            if (ticking)
            {
                return;
            }

            ticking = true;
            requestStartTicking = false;

            // Only continue ticking while this value is set. This allows us to close the tick thread from outside of it when we need to
            while (!requestStopTicking)
            {
                if (settings.tickBehavior != bLuaSettings.TickBehavior.TickOnlyWhenCoroutinesActive
                    || (settings.tickBehavior == bLuaSettings.TickBehavior.TickOnlyWhenCoroutinesActive && numRunningCoroutines > 0))
                {
                    InternalTick();
                }

                await Task.Delay(settings.tickInterval);
            }

            ticking = false;
            requestStopTicking = false;

            // If we've already re-requested to start ticking again, go ahead and handle that here as the previous Tick() call would have failed
            if (requestStartTicking)
            {
                StartTicking();
            }
        }

        void InternalTick()
        {
            if (OnTick != null)
            {
                OnTick.Invoke();
            }
        }

        public void StartTicking()
        {
            Tick();
            requestStartTicking = true;
        }

        public void StopTicking()
        {
            requestStopTicking = true;
        }
        #endregion // Tick

        #region Coroutines
        struct ScheduledCoroutine
        {
            public bLuaValue fn;
            public object[] args;
            public int debugTag;
        }

        bLuaValue callco = null;
        bLuaValue updateco = null;
        bLuaValue cancelcos = null;

        List<ScheduledCoroutine> scheduledCoroutines = new List<ScheduledCoroutine>();

        int ncoroutine = 0;


        void TickCoroutines()
        {
            using (Lua.s_profileLuaCo.Auto())
            {
                Call(updateco);

                while (scheduledCoroutines.Count > 0)
                {
                    var co = scheduledCoroutines[0];
                    scheduledCoroutines.RemoveAt(0);

                    CallCoroutine(co.fn, co.args);
                }
            }
        }

        void ScheduleCoroutine(bLuaValue _fn, params object[] _args)
        {
            ++ncoroutine;
            scheduledCoroutines.Add(new ScheduledCoroutine()
            {
                fn = _fn,
                args = _args,
                debugTag = ncoroutine,
            });
        }

        int numRunningCoroutines
        {
            get
            {
                LuaLibAPI.lua_getglobal(state, "builtin_coroutines");
                int len = (int)LuaLibAPI.lua_rawlen(state, -1);
                Lua.PopStack(this);
                return len;
            }
        }

        public void CallCoroutine(bLuaValue _fn, params object[] _args)
        {
            int nargs = _args != null ? _args.Length : 0;

            object[] a = new object[nargs + 1];
            a[0] = _fn;
            if (nargs > 0)
            {
                for (int i = 0; i != _args.Length; ++i)
                {
                    a[i + 1] = _args[i];
                }
            }

            Call(callco, a);
        }
        #endregion // Coroutines

        #region Globals
        public bLuaValue GetGlobal(string _key)
        {
            int resType = LuaLibAPI.lua_getglobal(state, _key);
            var result = Lua.PopStackIntoValue(this);
            result.dataType = (DataType)resType;
            return result;
        }

        public void SetGlobal(string _key, bLuaValue _value)
        {
            Lua.PushStack(this, _value);
            LuaLibAPI.lua_setglobal(state, _key);
        }

        public void SetGlobal(string _key, object _userDataObject)
        {
            bLuaValue v = bLuaValue.CreateUserData(this, _userDataObject);
            SetGlobal(_key, v);
        }
        #endregion // Globals

        #region Errors
        public delegate void LuaErrorDelegate(string message, string engineTrace = null);
        /// <summary> This delegate is called whenever a Lua Error happens. Allows for bLua features (or developers) to listen. </summary>
        public LuaErrorDelegate LuaErrorHandler;


        public class LuaException : Exception
        {
            public LuaException(string message) : base(message)
            {

            }
        }


        public void Error(string _message, string _engineTrace = null)
        {
            if (LuaErrorHandler != null)
            {
                LuaErrorHandler(_message, _engineTrace);
            }

            string msg = Lua.TraceMessage(this, _message);
            if (_engineTrace != null)
            {
                msg += "\n\n---\nEngine error details:\n" + _engineTrace;
            }

            Debug.LogError(msg);
        }
        #endregion // Errors

        /// <summary> Loads a string of Lua code and runs it. </summary>
        /// <param name="_code">The string of code to run.</param>
        public bLuaValue DoString(string _code)
        {
            return DoBuffer("code", _code);
        }

        /// <summary> Loads a buffer as a Lua chunk and runs it. </summary>
        /// <param name="_name">The chunk name, used for debug information and error messages.</param>
        /// <param name="_text">The Lua code to load.</param>
        public bLuaValue DoBuffer(string _name, string _text)
        {
            ExecBuffer(_name, _text, 1);
            return Lua.PopStackIntoValue(this);
        }

        /// <summary> Loads a buffer as a Lua chunk and runs it. </summary>
        /// <param name="_name">The chunk name, used for debug information and error messages.</param>
        /// <param name="_text">The Lua code to load.</param>
        public void ExecBuffer(string _name, string _text, int _nresults = 0, string _environment = "")
        {
            if (!initialized)
            {
                Debug.LogError("Attempt to ExecBuffer when instance is not initialized.");
                return;
            }

            using (Lua.s_profileLuaCall.Auto())
            {
                int result = LuaXLibAPI.luaL_loadbufferx(state, _text, (ulong)_text.Length, _name, null); // S: (buffer)
                if (result != 0)
                {
                    string msg = Lua.GetString(state, -1);
                    Lua.LuaPop(state, 1);
                    throw new LuaException(msg);
                }

                if (_environment != "")
                {
                    Lua.SetupEnvironmentForBuffer(state, _environment);
                }

                using (Lua.s_profileLuaCallInner.Auto())
                {
                    result = LuaLibAPI.lua_pcallk(state, 0, _nresults, 0, 0, IntPtr.Zero);
                }

                if (result != 0)
                {
                    string msg = Lua.GetString(state, -1);
                    Lua.LuaPop(state, 1);
                    throw new LuaException(msg);
                }
            }
        }

        /// <summary> Calls a passed Lua function. </summary>
        /// <param name="_fn">The Lua function being called.</param>
        /// <param name="_args">Arguments that will be passed into the called Lua function.</param>
        /// <returns>The output from the called Lua function.</returns>
        public bLuaValue Call(bLuaValue _fn, params object[] _args)
        {
            if (!initialized)
            {
                Debug.LogError("Attempt to Call function when instance is not initialized.");
                return null;
            }

            using (Lua.s_profileLuaCall.Auto())
            {
                Lua.PushStack(this, _fn);

                foreach (var arg in _args)
                {
                    Lua.PushObjectOntoStack(this, arg);
                }

                int result;
                //TODO set the error handler to get the stack trace.
                using (Lua.s_profileLuaCallInner.Auto())
                {
                    result = LuaLibAPI.lua_pcallk(state, _args.Length, 1, 0, 0L, IntPtr.Zero);
                }
                if (result != 0)
                {
                    string error = Lua.GetString(state, -1);
                    Lua.LuaPop(state, 1);
                    Error($"Error in function call: {error}");
                    throw new LuaException(error);
                }

                return Lua.PopStackIntoValue(this);
            }
        }

        public bLuaValue FullLookup(bLuaValue _value, string _key)
        {
            bLuaValue fn;
            if (lookups.TryGetValue(_key, out fn) == false)
            {
                fn = DoBuffer("lookup", $"return function(obj) return obj.{_key} end");
                lookups.Add(_key, fn);
            }

            return Call(fn, _value);
        }

        public bLuaValue InternString(string s)
        {
            bLuaValue result;
            if (s_internedStrings.TryGetValue(s, out result))
            {
                return result;
            }

            result = bLuaValue.CreateString(this, s);
            s_internedStrings.Add(s, result);
            return result;
        }

        /// <summary> Returns true if this instance's sandbox has the passed feature enabled. </summary>
        public bool FeatureEnabled(Feature _feature)
        {
            return ((Feature)(int)settings.sandbox).HasFlag(_feature);
        }

        #region C Functions called from Lua
        public static int CallFunction(IntPtr _state)
        {
            IntPtr mainThreadState = Lua.GetMainThread(_state);
            bLuaInstance mainThreadInstance = GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int stackSize = LuaLibAPI.lua_gettop(_state);
                if (stackSize == 0 || LuaLibAPI.lua_type(_state, 1) != (int)DataType.UserData)
                {
                    mainThreadInstance.Error($"Object not provided when calling function.");
                    return 0;
                }

                int n = LuaLibAPI.lua_tointegerx(_state, Lua.UpValueIndex(1), IntPtr.Zero);

                if (n < 0 || n >= mainThreadInstance.s_methods.Count)
                {
                    mainThreadInstance.Error($"Illegal method index: {n}");
                    return 0;
                }

                MethodCallInfo info = mainThreadInstance.s_methods[n];

                object[] parms = null;
                int parmsIndex = 0;

                int len = info.argTypes.Length;
                if (len > 0 && info.argTypes[len - 1] == MethodCallInfo.ParamType.Params)
                {
                    len--;
                    if (stackSize - 1 > len)
                    {
                        parms = new object[(stackSize - 1) - len];
                        parmsIndex = parms.Length - 1;
                    }
                }


                object[] args = new object[info.argTypes.Length];
                int argIndex = args.Length - 1;

                if (parms != null)
                {
                    args[argIndex--] = parms;
                }

                while (argIndex > stackSize - 2)
                {
                    //backfill any arguments with defaults.
                    args[argIndex] = info.defaultArgs[argIndex];
                    --argIndex;
                }
                while (stackSize - 2 > argIndex)
                {
                    if (parms != null)
                    {
                        parms[parmsIndex--] = Lua.PopStackIntoObject(mainThreadInstance);
                    }
                    else
                    {
                        Lua.PopStack(mainThreadInstance);
                    }
                    --stackSize;
                }

                while (stackSize > 1)
                {
                    args[argIndex] = bLuaUserData.PopStackIntoParamType(mainThreadInstance, info.argTypes[argIndex]);

                    --stackSize;
                    --argIndex;
                }

                if (LuaLibAPI.lua_gettop(_state) < 1)
                {
                    mainThreadInstance.Error($"Stack is empty");
                    return 0;
                }

                int t = LuaLibAPI.lua_type(_state, 1);
                if (t != (int)DataType.UserData)
                {
                    mainThreadInstance.Error($"Object is not a user data: {((DataType)t).ToString()}");
                    return 0;
                }

                LuaLibAPI.lua_checkstack(_state, 1);
                int res = LuaLibAPI.lua_getiuservalue(_state, 1, 1);
                if (res != (int)DataType.Number)
                {
                    mainThreadInstance.Error($"Object not provided when calling function.");
                    return 0;
                }
                int liveObjectIndex = LuaLibAPI.lua_tointegerx(_state, -1, IntPtr.Zero);

                object obj = mainThreadInstance.s_liveObjects[liveObjectIndex];

                object result = info.methodInfo.Invoke(obj, args);

                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, info.returnType, result);
                return 1;

            }
            catch (Exception e)
            {
                var ex = e.InnerException;
                if (ex == null)
                {
                    ex = e;
                }
                mainThreadInstance.Error($"Error calling function: {ex.Message}", $"{ex.StackTrace}");
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        public static int CallStaticFunction(IntPtr _state)
        {
            IntPtr mainThreadState = Lua.GetMainThread(_state);
            bLuaInstance mainThreadInstance = GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int n = LuaLibAPI.lua_tointegerx(_state, Lua.UpValueIndex(1), IntPtr.Zero);
                MethodCallInfo info = mainThreadInstance.s_methods[n];

                int stackSize = LuaLibAPI.lua_gettop(_state);

                object[] parms = null;
                int parmsIndex = 0;

                int len = info.argTypes.Length;
                if (len > 0 && info.argTypes[len - 1] == MethodCallInfo.ParamType.Params)
                {
                    len--;
                    if (stackSize > len)
                    {
                        parms = new object[stackSize - len];
                        parmsIndex = parms.Length - 1;
                    }
                }

                object[] args = new object[info.argTypes.Length];
                int argIndex = args.Length - 1;

                if (parms != null)
                {
                    args[argIndex--] = parms;
                }

                while (argIndex > stackSize - 1)
                {
                    //backfill any arguments with nulls.
                    args[argIndex] = info.defaultArgs[argIndex];
                    --argIndex;
                }
                while (stackSize - 1 > argIndex)
                {
                    if (parms != null)
                    {
                        parms[parmsIndex--] = Lua.PopStackIntoObject(mainThreadInstance);
                    }
                    else
                    {
                        Lua.PopStack(mainThreadInstance);
                    }
                    --stackSize;
                }

                while (stackSize > 0)
                {
                    args[argIndex] = bLuaUserData.PopStackIntoParamType(mainThreadInstance, info.argTypes[argIndex]);
                    --stackSize;
                    --argIndex;
                }

                object result = info.methodInfo.Invoke(null, args);

                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, info.returnType, result);
                return 1;

            }
            catch (Exception e)
            {
                var ex = e;
                while (ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }
                mainThreadInstance.Error($"Error calling function: {ex.Message}", $"{ex.StackTrace}");
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        public static int IndexFunction(IntPtr _state)
        {
            IntPtr mainThreadState = Lua.GetMainThread(_state);
            bLuaInstance mainThreadInstance = GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int n = LuaLibAPI.lua_tointegerx(_state, Lua.UpValueIndex(1), IntPtr.Zero);

                if (n < 0 || n >= mainThreadInstance.s_entries.Count)
                {
                    mainThreadInstance.Error($"Invalid type index in lua: {n}");
                    return 0;
                }

                UserDataRegistryEntry userDataInfo = mainThreadInstance.s_entries[n];

                string str = Lua.GetString(_state, 2);

                UserDataRegistryEntry.PropertyEntry propertyEntry;
                if (userDataInfo.properties.TryGetValue(str, out propertyEntry))
                {
                    switch (propertyEntry.propertyType)
                    {
                        case UserDataRegistryEntry.PropertyEntry.Type.Method:
                            int val = Lua.PushStack(mainThreadInstance, mainThreadInstance.s_methods[propertyEntry.index].closure);
                            return 1;
                        case UserDataRegistryEntry.PropertyEntry.Type.Property:
                            {
                                //get the iuservalue for the userdata onto the stack.
                                LuaLibAPI.lua_checkstack(_state, 1);
                                LuaLibAPI.lua_getiuservalue(_state, 1, 1);

                                int instanceIndex = Lua.PopInteger(mainThreadInstance);
                                object obj = mainThreadInstance.s_liveObjects[instanceIndex];

                                var propertyInfo = mainThreadInstance.s_properties[propertyEntry.index];
                                var result = propertyInfo.propertyInfo.GetMethod.Invoke(obj, null);
                                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, propertyInfo.propertyType, result);
                                return 1;
                            }
                        case UserDataRegistryEntry.PropertyEntry.Type.Field:
                            {
                                //get the iuservalue for the userdata onto the stack.
                                LuaLibAPI.lua_checkstack(_state, 1);
                                LuaLibAPI.lua_getiuservalue(_state, 1, 1);
                                int instanceIndex = Lua.PopInteger(mainThreadInstance);
                                object obj = mainThreadInstance.s_liveObjects[instanceIndex];

                                var fieldInfo = mainThreadInstance.s_fields[propertyEntry.index];
                                var result = fieldInfo.fieldInfo.GetValue(obj);
                                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, fieldInfo.fieldType, result);
                                return 1;
                            }
                    }
                }

                Lua.PushNil(mainThreadInstance);

                return 1;
            }
            catch (Exception e)
            {
                var ex = e.InnerException;
                if (ex == null)
                {
                    ex = e;
                }
                mainThreadInstance.Error("Error indexing userdata", $"Error in index: {ex.Message} {ex.StackTrace}");
                Lua.PushNil(mainThreadInstance);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        public static int SetIndexFunction(IntPtr _state)
        {
            IntPtr mainThreadState = Lua.GetMainThread(_state);
            bLuaInstance mainThreadInstance = GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int n = LuaLibAPI.lua_tointegerx(_state, Lua.UpValueIndex(1), IntPtr.Zero);

                if (n < 0 || n >= mainThreadInstance.s_entries.Count)
                {
                    mainThreadInstance.Error($"Invalid type index in lua: {n}");
                    return 0;
                }

                UserDataRegistryEntry userDataInfo = mainThreadInstance.s_entries[n];

                string str = Lua.GetString(_state, 2);

                UserDataRegistryEntry.PropertyEntry propertyEntry;
                if (userDataInfo.properties.TryGetValue(str, out propertyEntry))
                {
                    if (propertyEntry.propertyType == UserDataRegistryEntry.PropertyEntry.Type.Property)
                    {
                        LuaLibAPI.lua_checkstack(_state, 1);
                        //get the iuservalue for the userdata onto the stack.
                        LuaLibAPI.lua_getiuservalue(_state, 1, 1);
                        int instanceIndex = Lua.PopInteger(mainThreadInstance);
                        object obj = mainThreadInstance.s_liveObjects[instanceIndex];

                        object[] args = new object[1];

                        var propertyInfo = mainThreadInstance.s_properties[propertyEntry.index];
                        args[0] = bLuaUserData.PopStackIntoParamType(mainThreadInstance, propertyInfo.propertyType);


                        propertyInfo.propertyInfo.SetMethod.Invoke(obj, args);
                        return 0;
                    }
                    else if (propertyEntry.propertyType == UserDataRegistryEntry.PropertyEntry.Type.Field)
                    {
                        LuaLibAPI.lua_checkstack(_state, 1);
                        //get the iuservalue for the userdata onto the stack.
                        LuaLibAPI.lua_getiuservalue(_state, 1, 1);
                        int instanceIndex = Lua.PopInteger(mainThreadInstance);
                        object obj = mainThreadInstance.s_liveObjects[instanceIndex];

                        var fieldInfo = mainThreadInstance.s_fields[propertyEntry.index];
                        var arg = bLuaUserData.PopStackIntoParamType(mainThreadInstance, fieldInfo.fieldType);

                        fieldInfo.fieldInfo.SetValue(obj, arg);
                        return 0;
                    }

                }

                mainThreadInstance.Error($"Could not set property {str}");
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        public static int GCFunction(IntPtr _state)
        {
            if (_state == IntPtr.Zero)
            {
                return 0;
            }

            IntPtr mainThreadState = Lua.GetMainThread(_state);
            bLuaInstance mainThreadInstance = GetInstanceByState(mainThreadState);

            if (mainThreadInstance == null)
            {
                return 0;
            }

            LuaLibAPI.lua_checkstack(_state, 1);
            LuaLibAPI.lua_getiuservalue(_state, 1, 1);
            int n = LuaLibAPI.lua_tointegerx(mainThreadInstance.state, -1, IntPtr.Zero);
            mainThreadInstance.s_liveObjects[n] = null;
            mainThreadInstance.s_liveObjectsFreeList.Add(n);

            return 0;
        }
        #endregion // C Functions called from Lua
    }
} // bLua namespace
