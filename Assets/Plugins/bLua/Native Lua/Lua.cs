using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Profiling;
using bLua.Internal;
using UnityEditorInternal;
using UnityEngine;

namespace bLua.NativeLua
{
    public class MonoPInvokeCallbackAttribute : Attribute
    {

    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int LuaCFunction(IntPtr state);

    [StructLayout(LayoutKind.Sequential)]
    public class StrLen
    {
        public ulong len;
    }

    [StructLayout(LayoutKind.Sequential)]
    public class CoroutineResult
    {
        public int result;
    }
    
    class LuaCoroutine
    {
        public IntPtr state;
        public int refId;
    }
    
    public struct StringCacheEntry
    {
        public string key;
        public bLuaValue value;
    }

    public enum LuaThreadStatus
    {
        LUA_OK = 0,
        LUA_YIELD = 1,
        LUA_ERRRUN = 2,
        LUA_ERRSYNTAX = 3,
        LUA_ERRMEM = 4,
        LUA_ERRERR = 5
    }
    
    /// <summary> Contains helper functions as well as functions that interface with the LuaLibAPI and LuaXLibAPI. </summary>
    public static class Lua
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        public const string LUA_DLL = "lua54.dll";
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        public const string LUA_DLL = "Lua";
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        public const string LUA_DLL = "liblua54.so";
#endif

        public static int LUA_TNONE = -1;
        public static int LUA_MAXSTACK = 1000000;
        public static int LUA_REGISTRYINDEX = (-LUA_MAXSTACK - 1000);

        public static int LUA_RIDX_MAINTHREAD = 1;
        public static int LUA_RIDX_GLOBALS = 2;
        public static int LUA_RIDX_LAST = LUA_RIDX_GLOBALS;

        public static ProfilerMarker profile_luaGC = new("Lua.GC");
        public static ProfilerMarker profile_luaCo = new("Lua.Coroutine");
        public static ProfilerMarker profile_luaCall = new("Lua.Call");
        public static ProfilerMarker profile_luaCallInner = new("Lua.CallInner");

        public const string LUA_COROUTINE = "coroutine";
        public const string LUA_COROUTINE_WRAP = "wrap";
        
        private static StrLen strlen = new();


#region Miscellaneous
        public static IntPtr GetMainThread(IntPtr _state)
        {
            LuaLibAPI.lua_rawgeti(_state, LUA_REGISTRYINDEX, LUA_RIDX_MAINTHREAD);
            IntPtr thread = LuaLibAPI.lua_tothread(_state, -1);
            LuaPop(_state, 1);
            return thread;
        }

        public static IntPtr StringToIntPtr(string _string)
        {
            byte[] b = StrToUTF8(_string);

            unsafe
            {
                fixed (byte* p = b)
                {
                    return new IntPtr(p);
                }
            }
        }

        public static byte[] StrToUTF8(string _string)
        {
            return System.Text.UTF8Encoding.UTF8.GetBytes(_string);
        }

        public static string UTF8ToStr(byte[] _bytes)
        {
            return System.Text.UTF8Encoding.UTF8.GetString(_bytes);
        }

        public static int UpValueIndex(int i)
        {
            return LUA_REGISTRYINDEX - i;
        }

        public static string GetString(IntPtr _state, int n)
        {
            var ptr = LuaLibAPI.lua_tolstring(_state, n, strlen);
            byte[] bytes = new byte[strlen.len];
            Marshal.Copy(ptr, bytes, 0, (int)strlen.len);
            return UTF8ToStr(bytes);
        }

        public static void Unreference(bLuaInstance _instance, int _referenceID)
        {
            if (_instance.state != IntPtr.Zero)
            {
                LuaXLibAPI.luaL_unref(_instance.state, LUA_REGISTRYINDEX, _referenceID);
            }
        }

        public static DataType InspectTypeOnTopOfStack(bLuaInstance _instance)
        {
            return (DataType)LuaLibAPI.lua_type(_instance.state, -1);
        }

        /// <summary> Returns a stack trace from the top of the stack. </summary>
        /// <param name="_message">An optional message to be prepended to the returned trace</param>
        /// <param name="_level">The level in the stack to start tracing from</param>
        /// <returns></returns>
        public static string TraceMessage(bLuaInstance _instance, string _message = null, int _level = 1)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            LuaXLibAPI.luaL_traceback(_instance.state, _instance.state, _message != null ? _message : "", _level);
            return PopString(_instance);
        }
#endregion // Miscellaneous

#region Push (Stack)
        public static void PushNil(bLuaInstance _instance)
        {
            PushNil(_instance.state);
        }
        public static void PushNil(IntPtr _state)
        {
            LuaLibAPI.lua_checkstack(_state, 1);
            LuaLibAPI.lua_pushnil(_state);
        }

        public static void LuaPushCFunction(bLuaInstance _instance, IntPtr _state, LuaCFunction _fn)
        {
            LuaLibAPI.lua_pushcclosure(_state, Marshal.GetFunctionPointerForDelegate(_fn), 0);
        }

        public static void PushClosure(bLuaInstance _instance, IntPtr _state, LuaCFunction _fn, bLuaValue[] _upvalues)
        {
            for (int i = 0; i != _upvalues.Length; ++i)
            {
                PushStack(_state, _upvalues[i]);
            }

            LuaLibAPI.lua_pushcclosure(_state, Marshal.GetFunctionPointerForDelegate(_fn), _upvalues.Length);
        }

        public static void PushClosure(bLuaInstance _instance, IntPtr _state, GlobalMethodCallInfo _globalMethodCallInfo)
        {
            LuaCFunction fn = CallGlobalMethod;
            bLuaValue[] upvalues = new bLuaValue[] { bLuaValue.CreateNumber(_instance, _instance.registeredMethods.Count) };
            _instance.registeredMethods.Add(_globalMethodCallInfo);

            PushClosure(_instance, _state, fn, upvalues);
        }

        public static void PushClosure<T>(bLuaInstance _instance, IntPtr _state, T _func) where T : MulticastDelegate
        {
            MethodInfo methodInfo = _func.Method;

            ParameterInfo[] methodParams = methodInfo.GetParameters();
            MethodCallInfo.ParamType[] argTypes = new MethodCallInfo.ParamType[methodParams.Length];
            object[] defaultArgs = new object[methodParams.Length];
            for (int i = 0; i != methodParams.Length; ++i)
            {
                argTypes[i] = bLuaUserData.SystemTypeToParamType(_instance, methodParams[i].ParameterType);
                if (i == methodParams.Length - 1 && methodParams[i].GetCustomAttribute(typeof(ParamArrayAttribute)) != null)
                {
                    argTypes[i] = MethodCallInfo.ParamType.Params;
                }

                if (methodParams[i].HasDefaultValue)
                {
                    defaultArgs[i] = methodParams[i].DefaultValue;
                }
                else if (argTypes[i] == MethodCallInfo.ParamType.LuaValue)
                {
                    defaultArgs[i] = bLuaValue.Nil;
                }
                else
                {
                    defaultArgs[i] = null;
                }
            }

            LuaCFunction fn = CallDelegate;
            bLuaValue[] upvalues = new bLuaValue[] { bLuaValue.CreateNumber(_instance, _instance.registeredMethods.Count) };

            DelegateCallInfo methodCallInfo = new DelegateCallInfo()
            {
                methodInfo = methodInfo,
                returnType = bLuaUserData.SystemTypeToParamType(_instance, methodInfo.ReturnType),
                argTypes = argTypes,
                defaultArgs = defaultArgs,
                closure = bLuaValue.CreateClosure(_instance, fn, upvalues),
                multicastDelegate = _func
            };
            _instance.registeredMethods.Add(methodCallInfo);

            PushClosure(_instance, _state, fn, upvalues);
        }

        public static bLuaValue GetUpvalue(bLuaInstance _instance, int _index, int _n)
        {
            LuaLibAPI.lua_getupvalue(_instance.state, _index, _n);
            return PopStackIntoValue(_instance);
        }
        
        public static void PushNewTable(bLuaInstance _instance, int _reserveArray = 0, int _reserveTable = 0)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            LuaLibAPI.lua_createtable(_instance.state, _reserveArray, _reserveTable);
        }

        public static void PushString(bLuaInstance _instance, string _string)
        {
            byte[] b = StrToUTF8(_string);
            LuaLibAPI.lua_pushlstring(_instance.state, StringToIntPtr(_string), (ulong)b.Length);
        }

        public static void PushOntoStack(bLuaInstance _instance, object _object)
        {
            PushOntoStack(_instance, _instance.state, _object);
        }
        
        public static void PushOntoStack(bLuaInstance _instance, IntPtr _state, object _object)
        {
            if (_object is bLuaValue dynValue)
            {
                PushStack(_state, dynValue);
                return;
            }
            
            if (bLuaUserData.IsRegistered(_instance, _object.GetType()))
            {
                bLuaValue ud = bLuaValue.CreateUserData(_instance, _object);
                PushStack(_state, ud);
                return;
            }

            LuaLibAPI.lua_checkstack(_state, 1);

            switch (_object)
            {
                case int objectInt:
                    LuaLibAPI.lua_pushinteger(_state, objectInt);
                    break;
                case double objectDouble:
                    LuaLibAPI.lua_pushnumber(_state, objectDouble);
                    break;
                case float objectFloat:
                    LuaLibAPI.lua_pushnumber(_state, objectFloat);
                    break;
                case bool objectBool:
                    LuaLibAPI.lua_pushboolean(_state, objectBool ? 1 : 0);
                    break;
                case string objectString:
                    LuaLibAPI.lua_pushstring(_state, objectString);
                    break;
                case LuaCFunction objectFunction:
                    LuaPushCFunction(_instance, _state, objectFunction);
                    break;
                case GlobalMethodCallInfo objectInfo:
                    PushClosure(_instance, _state, objectInfo);
                    break;
                case MulticastDelegate objectDelegate: // Func<> and Action<>
                    PushClosure(_instance, _state, objectDelegate);
                    break;
                default:
                    LuaLibAPI.lua_pushnil(_state);
                    _instance.ErrorFromCSharp($"{bLuaError.error_unrecognizedStackPush}{_object.GetType()}");
                    break;
            }
        }

        public static int PushStack(bLuaInstance _instance, bLuaValue _value)
        {
            return PushStack(_instance.state, _value);
        }
        public static int PushStack(IntPtr _state, bLuaValue _value)
        {
            LuaLibAPI.lua_checkstack(_state, 1);

            if (_value == null)
            {
                PushNil(_state);
                return (int)DataType.Nil;
            }

            return LuaLibAPI.lua_rawgeti(_state, LUA_REGISTRYINDEX, _value.referenceID);
        }
#endregion // Push (Stack)

#region Pop (Stack)
        public static void LuaPop(IntPtr _state, int n)
        {
            LuaLibAPI.lua_settop(_state, -(n) - 1);
        }

        public static IntPtr PopStackIntoPointer(bLuaInstance _instance)
        {
            IntPtr pointer = LuaLibAPI.lua_topointer(_instance.state, 1);
            LuaPop(_instance.state, 1);
            return pointer;
        }
        
        public static bLuaValue PopStackIntoValue(bLuaInstance _instance)
        {
            int t = LuaLibAPI.lua_type(_instance.state, -1);
            switch (t)
            {
                case (int)DataType.Nil:
                    LuaPop(_instance.state, 1);
                    return bLuaValue.Nil;

                default:
                    int refid = LuaXLibAPI.luaL_ref(_instance.state, LUA_REGISTRYINDEX);
                    return new bLuaValue(_instance, refid);
            }
        }

        public static object PopStackIntoObject(bLuaInstance _instance)
        {
            DataType t = (DataType)LuaLibAPI.lua_type(_instance.state, -1);
            switch (t)
            {
                case DataType.Nil:
                    PopStack(_instance);
                    return bLuaValue.Nil;
                case DataType.Boolean:
                    return PopBool(_instance);
                case DataType.Number:
                    return PopNumber(_instance);
                case DataType.String:
                    return PopString(_instance);
                default:
                    return PopStackIntoValue(_instance);
            }
        }

        public static double PopNumber(bLuaInstance _instance)
        {
            double result = LuaLibAPI.lua_tonumberx(_instance.state, -1, IntPtr.Zero);
            LuaPop(_instance.state, 1);
            return result;
        }

        public static int PopInteger(bLuaInstance _instance)
        {
            int result = LuaLibAPI.lua_tointegerx(_instance.state, -1, IntPtr.Zero);
            LuaPop(_instance.state, 1);
            return result;
        }

        public static bool PopBool(bLuaInstance _instance)
        {
            int result = LuaLibAPI.lua_toboolean(_instance.state, -1);
            LuaPop(_instance.state, 1);
            return result != 0;
        }

        public static string PopString(bLuaInstance _instance)
        {
            string result = GetString(_instance.state, -1);
            LuaPop(_instance.state, 1);
            return result;
        }

        public static List<bLuaValue> PopList(bLuaInstance _instance)
        {
            int len = (int)LuaLibAPI.lua_rawlen(_instance.state, -1);
            List<bLuaValue> result = new List<bLuaValue>(len);

            LuaLibAPI.lua_checkstack(_instance.state, 2);

            for (int i = 1; i <= len; ++i)
            {
                LuaLibAPI.lua_geti(_instance.state, -1, i);
                result.Add(PopStackIntoValue(_instance));
            }

            //we're actually popping the list off.
            LuaPop(_instance.state, 1);

            return result;
        }

        public static List<string> PopListOfStrings(bLuaInstance _instance)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 2);

            int len = (int)LuaLibAPI.lua_rawlen(_instance.state, -1);
            List<string> result = new List<string>(len);

            for (int i = 1; i <= len; ++i)
            {
                int t = LuaLibAPI.lua_geti(_instance.state, -1, i);
                if (t == (int)DataType.String)
                {
                    result.Add(PopString(_instance));
                }
                else
                {
                    PopStack(_instance);
                }
            }

            //we're actually popping the list off.
            LuaPop(_instance.state, 1);

            return result;
        }

        public static Dictionary<string, bLuaValue> PopDict(bLuaInstance _instance)
        {
            Dictionary<string, bLuaValue> result = new Dictionary<string, bLuaValue>();
            LuaLibAPI.lua_pushnil(_instance.state);
            while (LuaLibAPI.lua_next(_instance.state, -2) != 0)
            {
                if (LuaLibAPI.lua_type(_instance.state, -2) != (int)DataType.String)
                {
                    LuaPop(_instance.state, 1);
                    continue;
                }

                string key = GetString(_instance.state, -2);
                result.Add(key, PopStackIntoValue(_instance));
            }

            //pop the table off the stack.
            LuaPop(_instance.state, 1);

            return result;
        }

        public static List<bLuaValue.Pair> PopFullDict(bLuaInstance _instance)
        {
            List<bLuaValue.Pair> result = new List<bLuaValue.Pair>();
            LuaLibAPI.lua_pushnil(_instance.state);
            while (LuaLibAPI.lua_next(_instance.state, -2) != 0)
            {
                var val = PopStackIntoValue(_instance);
                var key = PopStackIntoValue(_instance);
                PushStack(_instance, key);

                result.Add(new bLuaValue.Pair()
                {
                    Key = key,
                    Value = val,
                });
            }

            //pop the table off the stack.
            LuaPop(_instance.state, 1);

            return result;
        }

        public static bool PopTableHasNonInts(bLuaInstance _instance)
        {
            LuaLibAPI.lua_pushnil(_instance.state);
            while (LuaLibAPI.lua_next(_instance.state, -2) != 0)
            {
                bLuaValue val = PopStackIntoValue(_instance);

                if (LuaLibAPI.lua_type(_instance.state, -1) != (int)DataType.String)
                {
                    //pop key, value, and table.
                    LuaPop(_instance.state, 3);
                    return true;
                }

                //just pop value, key goes with next.
                LuaPop(_instance.state, 1);
            }

            //pop the table off the stack.
            LuaPop(_instance.state, 1);

            return false;
        }

        public static bool PopTableEmpty(bLuaInstance _instance)
        {
            LuaLibAPI.lua_pushnil(_instance.state);

            bool result = (LuaLibAPI.lua_next(_instance.state, -2) == 0);
            LuaPop(_instance.state, result ? 1 : 3); //if empty pop just the table, otherwise the table and the key/value pair.

            return result;
        }

        public static void PopStack(bLuaInstance _instance)
        {
            LuaPop(_instance.state, 1);
        }
#endregion // Pop (Stack)

#region New Values
        public static bLuaValue NewMetaTable(bLuaInstance _instance, string _name)
        {
            LuaXLibAPI.luaL_newmetatable(_instance.state, _name);
            return PopStackIntoValue(_instance);
        }

        public static bLuaValue NewBoolean(bLuaInstance _instance, bool _value)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            LuaLibAPI.lua_pushboolean(_instance.state, _value ? 1 : 0);
            return PopStackIntoValue(_instance);
        }

        public static bLuaValue NewNumber(bLuaInstance _instance, double _value)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            LuaLibAPI.lua_pushnumber(_instance.state, _value);
            return PopStackIntoValue(_instance);
        }

        public static bLuaValue NewString(bLuaInstance _instance, string _value)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 1);
            PushOntoStack(_instance, _value);
            return PopStackIntoValue(_instance);
        }
#endregion // New Values

#region Tables
        public static bLuaValue GetTable<T>(bLuaInstance _instance, bLuaValue _table, T _key)
        {
            PushStack(_instance, _table);
            PushOntoStack(_instance, _key);
            DataType t = (DataType)LuaLibAPI.lua_gettable(_instance.state, -2);
            var result = PopStackIntoValue(_instance);
            result.dataType = t;
            PopStack(_instance);
            return result;
        }

        public static void SetTable<TKey, TValue>(bLuaInstance _instance, bLuaValue _table, TKey _key, TValue _value)
        {
            PushOntoStack(_instance, _table);
            PushOntoStack(_instance, _key);
            PushOntoStack(_instance, _value);
            LuaLibAPI.lua_settable(_instance.state, -3);
            PopStack(_instance);
        }
#endregion // Tables

#region Arrays
        public static int Length(bLuaInstance _instance, bLuaValue _value)
        {
            PushStack(_instance, _value);
            uint result = LuaLibAPI.lua_rawlen(_instance.state, -1);
            PopStack(_instance);

            return (int)result;
        }

        //index -- remember, 1-based!
        public static bLuaValue Index(bLuaInstance _instance, bLuaValue _value, int i)
        {
            LuaLibAPI.lua_checkstack(_instance.state, 3);
            PushStack(_instance, _value);
            LuaLibAPI.lua_geti(_instance.state, -1, i);
            var result = PopStackIntoValue(_instance);
            PopStack(_instance);
            return result;
        }

        public static void SetIndex(bLuaInstance _instance, bLuaValue _array, int i, bLuaValue _newValue)
        {
            PushStack(_instance, _array);
            PushStack(_instance, _newValue);
            LuaLibAPI.lua_seti(_instance.state, -2, i);
            PopStack(_instance);
        }

        public static void AppendArray(bLuaInstance _instance, bLuaValue _array, bLuaValue _newValue)
        {
            PushStack(_instance, _array);
            int len = (int)LuaLibAPI.lua_rawlen(_instance.state, -1);
            PushStack(_instance, _newValue);
            LuaLibAPI.lua_seti(_instance.state, -2, len + 1);
            PopStack(_instance);
        }

        public static void AppendArray(bLuaInstance _instance, bLuaValue _array, object _newValue)
        {
            PushStack(_instance, _array);
            int len = (int)LuaLibAPI.lua_rawlen(_instance.state, -1);
            PushOntoStack(_instance, _newValue);
            LuaLibAPI.lua_seti(_instance.state, -2, len + 1);
            PopStack(_instance);
        }
#endregion // Arrays

#region Coroutines
        public static bool IsYieldable(bLuaInstance _instance)
        {
            return LuaLibAPI.lua_isyieldable(_instance.state) == 1;
        }

        public static int Yield(bLuaInstance _instance, int _results)
        {
            return LuaLibAPI.lua_yieldk(_instance.state, _results, IntPtr.Zero, IntPtr.Zero);
        }

        public static LuaThreadStatus Resume(IntPtr _state, IntPtr _instigator, int _nargs)
        {
            return (LuaThreadStatus)LuaLibAPI.lua_resume(_state, _instigator, _nargs, out int nResults);
        }

        public static void PushThread(bLuaInstance _instance)
        {
            LuaLibAPI.lua_pushthread(_instance.state);
        }

        public static IntPtr NewThread(bLuaInstance _instance)
        {
            return LuaLibAPI.lua_newthread(_instance.state);
        }
#endregion // Coroutines

#region MonoPInvokeCallback
        [MonoPInvokeCallback]
        public static int CallGlobalMethod(IntPtr _state)
        {
            IntPtr mainThreadState = GetMainThread(_state);
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int stackSize = LuaLibAPI.lua_gettop(_state);

                int n = LuaLibAPI.lua_tointegerx(_state, UpValueIndex(1), IntPtr.Zero);

                if (n < 0 || n >= mainThreadInstance.registeredMethods.Count)
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_invalidMethodIndex}{n}");
                    return 0;
                }

                MethodCallInfo methodCallInfo = mainThreadInstance.registeredMethods[n];
                GlobalMethodCallInfo info = methodCallInfo as GlobalMethodCallInfo;

                if (info == null)
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_invalidMethodIndex}{n}");
                    return 0;
                }
                
                object[] parms = null;
                int parmsIndex = 0;

                int parametersLength = info.argTypes.Length;
                if (parametersLength > 0 && info.argTypes[parametersLength - 1] == MethodCallInfo.ParamType.Params)
                {
                    parametersLength--;
                    if (stackSize > parametersLength)
                    {
                        parms = new object[(stackSize) - parametersLength];
                        parmsIndex = parms.Length - 1;
                    }
                }

                object[] args = new object[info.argTypes.Length];
                int argIndex = args.Length - 1;

                // Set the last args index to be the parameters array
                if (parms != null)
                {
                    args[argIndex--] = parms;
                }

                while (argIndex > stackSize - 1)
                {
                    // Backfill any arguments with defaults.
                    args[argIndex] = info.defaultArgs[argIndex];
                    --argIndex;
                }
                while (stackSize - 1 > argIndex)
                {
                    // Backfill the parameters with values from the Lua stack
                    if (parms != null)
                    {
                        parms[parmsIndex--] = PopStackIntoObject(mainThreadInstance);
                    }
                    else
                    {
                        PopStack(mainThreadInstance);
                    }
                    --stackSize;
                }

                while (stackSize > 0)
                {
                    args[argIndex] = bLuaUserData.PopStackIntoParamType(mainThreadInstance, info.argTypes[argIndex]);

                    --stackSize;
                    --argIndex;
                }

                object result = info.methodInfo.Invoke(info.objectInstance, args);

                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, info.returnType, result);
                return 1;

            }
            catch (Exception e)
            {
                mainThreadInstance.ErrorFromCSharp(e, bLuaError.error_callingDelegate);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        [MonoPInvokeCallback]
        public static int CallDelegate(IntPtr _state)
        {
            IntPtr mainThreadState = GetMainThread(_state);
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int stackSize = LuaLibAPI.lua_gettop(_state);

                int n = LuaLibAPI.lua_tointegerx(_state, UpValueIndex(1), IntPtr.Zero);

                if (n < 0 || n >= mainThreadInstance.registeredMethods.Count)
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_invalidMethodIndex}{n}");
                    return 0;
                }

                MethodCallInfo methodCallInfo = mainThreadInstance.registeredMethods[n];
                DelegateCallInfo info = methodCallInfo as DelegateCallInfo;

                if (info == null)
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_invalidMethodIndex}{n}");
                    return 0;
                }
                
                object[] parms = null;
                int parmsIndex = 0;

                int parametersLength = info.argTypes.Length;
                if (parametersLength > 0 && info.argTypes[parametersLength - 1] == MethodCallInfo.ParamType.Params)
                {
                    parametersLength--;
                    if (stackSize > parametersLength)
                    {
                        parms = new object[(stackSize) - parametersLength];
                        parmsIndex = parms.Length - 1;
                    }
                }

                object[] args = new object[info.argTypes.Length];
                int argIndex = args.Length - 1;

                // Set the last args index to be the parameters array
                if (parms != null)
                {
                    args[argIndex--] = parms;
                }

                while (argIndex > stackSize - 1)
                {
                    // Backfill any arguments with defaults.
                    args[argIndex] = info.defaultArgs[argIndex];
                    --argIndex;
                }
                while (stackSize - 1 > argIndex)
                {
                    // Backfill the parameters with values from the Lua stack
                    if (parms != null)
                    {
                        parms[parmsIndex--] = PopStackIntoObject(mainThreadInstance);
                    }
                    else
                    {
                        PopStack(mainThreadInstance);
                    }
                    --stackSize;
                }

                while (stackSize > 0)
                {
                    args[argIndex] = bLuaUserData.PopStackIntoParamType(mainThreadInstance, info.argTypes[argIndex]);

                    --stackSize;
                    --argIndex;
                }

                object result = info.multicastDelegate.DynamicInvoke(args);

                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, info.returnType, result);
                return 1;

            }
            catch (Exception e)
            {
                mainThreadInstance.ErrorFromCSharp(e, bLuaError.error_callingDelegate);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        [MonoPInvokeCallback]
        public static int CallUserDataFunction(IntPtr _state)
        {
            IntPtr mainThreadState = GetMainThread(_state);
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int stackSize = LuaLibAPI.lua_gettop(_state);
                if (stackSize == 0 || LuaLibAPI.lua_type(_state, 1) != (int)DataType.UserData)
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_objectNotProvided}");
                    return 0;
                }

                int n = LuaLibAPI.lua_tointegerx(_state, UpValueIndex(1), IntPtr.Zero);

                if (n < 0 || n >= mainThreadInstance.registeredMethods.Count)
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_invalidMethodIndex}{n}");
                    return 0;
                }

                MethodCallInfo info = mainThreadInstance.registeredMethods[n];

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
                    // Backfill any arguments with defaults.
                    args[argIndex] = info.defaultArgs[argIndex];
                    --argIndex;
                }
                while (stackSize - 2 > argIndex)
                {
                    if (parms != null)
                    {
                        parms[parmsIndex--] = PopStackIntoObject(mainThreadInstance);
                    }
                    else
                    {
                        PopStack(mainThreadInstance);
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
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_stackIsEmpty}");
                    return 0;
                }

                int t = LuaLibAPI.lua_type(_state, 1);
                if (t != (int)DataType.UserData)
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_objectIsNotUserdata}{(DataType)t}");
                    return 0;
                }

                LuaLibAPI.lua_checkstack(_state, 1);
                int res = LuaLibAPI.lua_getiuservalue(_state, 1, 1);
                if (res != (int)DataType.Number)
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_objectNotProvided}");
                    return 0;
                }
                int liveObjectIndex = LuaLibAPI.lua_tointegerx(_state, -1, IntPtr.Zero);
                object obj = mainThreadInstance.liveObjects[liveObjectIndex];

                object result = info.methodInfo.Invoke(obj, args);

                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, info.returnType, result);
                return 1;
            }
            catch (Exception e)
            {
                mainThreadInstance.ErrorFromCSharp(e, bLuaError.error_callingFunction);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }

        [MonoPInvokeCallback]
        public static int CallStaticUserDataFunction(IntPtr _state)
        {
            IntPtr mainThreadState = GetMainThread(_state);
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(mainThreadState);

            var stateBack = mainThreadInstance.state;
            try
            {
                mainThreadInstance.state = _state;

                int n = LuaLibAPI.lua_tointegerx(_state, UpValueIndex(1), IntPtr.Zero);
                MethodCallInfo info = mainThreadInstance.registeredMethods[n];

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
                    // Backfill any arguments with nulls.
                    args[argIndex] = info.defaultArgs[argIndex];
                    --argIndex;
                }
                while (stackSize - 1 > argIndex)
                {
                    if (parms != null)
                    {
                        parms[parmsIndex--] = PopStackIntoObject(mainThreadInstance);
                    }
                    else
                    {
                        PopStack(mainThreadInstance);
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
                mainThreadInstance.ErrorFromCSharp(e, bLuaError.error_callingFunction);
                return 0;
            }
            finally
            {
                mainThreadInstance.state = stateBack;
            }
        }
#endregion // MonoPInvokeCallback
    }
} // bLua.NativeLua namespace
