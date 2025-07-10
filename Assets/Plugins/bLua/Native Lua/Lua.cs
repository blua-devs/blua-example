using System;
using System.Reflection;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Unity.Profiling;
using bLua.Internal;
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

    public struct StateData
    {
        public IntPtr state;
    }

    [Flags]
    public enum CoroutinePauseFlag
    {
        NONE = 0,
        BLUA_CSHARPASYNCAWAIT = 1,
    }
    
    public class LuaCoroutine
    {
        public IntPtr state;
        public int refId;
        public CoroutinePauseFlag pauseFlags;
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
            List<MethodCallInfo.ParamType> argTypes = new();
            List<object> defaultArgs = new();
            for (int i = 0; i != methodParams.Length; ++i)
            {
                if (methodParams[i].GetCustomAttribute(typeof(bLuaStateParam)) != null
                    && methodParams[i].ParameterType == typeof(StateData))
                {
                    continue;
                }

                MethodCallInfo.ParamType paramType = bLuaUserData.SystemTypeToParamType(_instance, methodParams[i].ParameterType);
                if (methodParams[i].GetCustomAttribute(typeof(ParamArrayAttribute)) != null)
                {
                    argTypes.Add(MethodCallInfo.ParamType.Params);
                }
                else
                {
                    argTypes.Add(paramType);
                }

                if (methodParams[i].HasDefaultValue)
                {
                    defaultArgs.Add(methodParams[i].DefaultValue);
                }
                else if (paramType == MethodCallInfo.ParamType.LuaValue)
                {
                    defaultArgs.Add(bLuaValue.Nil);
                }
                else
                {
                    defaultArgs.Add(null);
                }
            }

            LuaCFunction fn = CallDelegate;
            bLuaValue[] upvalues = new bLuaValue[] { bLuaValue.CreateNumber(_instance, _instance.registeredMethods.Count) };

            DelegateCallInfo methodCallInfo = new DelegateCallInfo()
            {
                methodInfo = methodInfo,
                returnType = bLuaUserData.SystemTypeToParamType(_instance, methodInfo.ReturnType),
                argTypes = argTypes.ToArray(),
                defaultArgs = defaultArgs.ToArray(),
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
        public static bool IsYieldable(IntPtr _state)
        {
            return LuaLibAPI.lua_isyieldable(_state) == 1;
        }

        public static LuaThreadStatus YieldThread(bLuaInstance _instance, IntPtr _state, int _results)
        {
            return (LuaThreadStatus)LuaLibAPI.lua_yieldk(_state, _results, IntPtr.Zero, IntPtr.Zero);
        }

        public static bool IsDead(bLuaInstance _instance, IntPtr _state)
        {
            LuaThreadStatus preResumeStatus = (LuaThreadStatus)LuaLibAPI.lua_status(_state);
            if (preResumeStatus == LuaThreadStatus.LUA_OK) // Coroutine is either dead or hasn't started yet
            {
                if (LuaLibAPI.lua_gettop(_state) == 0) // Coroutine stack is empty (coroutine is dead)
                {
                    return true;
                }
            }

            return false;
        }
        
        public static bool ResumeThread(bLuaInstance _instance, IntPtr _state, IntPtr _instigator, params object[] _args)
        {
            if (IsDead(_instance, _state))
            {
                return false;
            }
            
            // Push arguments to the coroutine state
            if (_args != null)
            {
                foreach (object arg in _args)
                {
                    PushOntoStack(_instance, _state, arg);
                }
            }
            
            LuaThreadStatus postResumeStatus = (LuaThreadStatus)LuaLibAPI.lua_resume(_state, _instigator, _args != null ? _args.Length : 0, out int nResults);
            
            if (postResumeStatus != LuaThreadStatus.LUA_OK
                && postResumeStatus != LuaThreadStatus.LUA_YIELD)
            {
                string error = GetString(_state, -1);
                LuaPop(_state, 1);
                _instance.ErrorFromLua($"{bLuaError.error_inCoroutineResume}", $"{error}");

                return false;
            }

            return true;
        }

        public static IntPtr NewThread(IntPtr _state)
        {
            return LuaLibAPI.lua_newthread(_state);
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

                if (!GetMethodInfoUpvalue(_state, mainThreadInstance, out MethodCallInfo methodCallInfo))
                {
                    return 0;
                }
                
                if (!PopStackIntoArgs(mainThreadInstance, methodCallInfo, out object[] args, 1))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_inFunctionCall}nil");
                    return 0;
                }
                
                return (int)InvokeCSharpMethod(mainThreadInstance, _state, methodCallInfo, null, args);
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
                
                if (!GetMethodInfoUpvalue(_state, mainThreadInstance, out MethodCallInfo methodCallInfo))
                {
                    return 0;
                }
                
                if (!PopStackIntoArgs(mainThreadInstance, methodCallInfo, out object[] args))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_inFunctionCall}nil");
                    return 0;
                }
                
                return (int)InvokeCSharpMethod(mainThreadInstance, _state, methodCallInfo, null, args);
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

                if (LuaLibAPI.lua_gettop(_state) == 0 // Stack size equals 0
                    || LuaLibAPI.lua_type(_state, 1) != (int)DataType.UserData) // First arg passed isn't userdata
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_objectNotProvided}");
                    return 0;
                }

                if (!GetMethodInfoUpvalue(_state, mainThreadInstance, out MethodCallInfo info))
                {
                    return 0;
                }

                if (!GetLiveObjectUpvalue(_state, mainThreadInstance, out object liveObject))
                {
                    return 0;
                }

                if (!PopStackIntoArgs(mainThreadInstance, info, out object[] args, 1))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_inFunctionCall}nil");
                    return 0;
                }
                
                return (int)InvokeCSharpMethod(mainThreadInstance, _state, info, liveObject, args);
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

                if (!GetMethodInfoUpvalue(_state, mainThreadInstance, out MethodCallInfo info))
                {
                    return 0;
                }
                
                if (!PopStackIntoArgs(mainThreadInstance, info, out object[] args))
                {
                    mainThreadInstance.ErrorFromCSharp($"{bLuaError.error_inFunctionCall}nil");
                    return 0;
                }

                return (int)InvokeCSharpMethod(mainThreadInstance, _state, info, null, args);
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
        
#region Invocation
        public static LuaThreadStatus InvokeCSharpMethod(bLuaInstance _instance, IntPtr _state, MethodCallInfo _methodCallInfo, object _liveObject, params object[] _args)
        {
            // If the method is async, yield the coroutine, pause the coroutine, and only resume + unpause when the async method has completed
            if ((typeof(Task).IsAssignableFrom(_methodCallInfo.methodInfo.ReturnType) || _methodCallInfo.methodInfo.GetCustomAttribute<AsyncStateMachineAttribute>() != null)
                && _instance.FeatureEnabled(Features.Coroutines)
                && IsYieldable(_state))
            {
                Func<Task> asyncTask = async () =>
                {
                    object returnValue = InvokeMethodCallInfo(_methodCallInfo, _liveObject, _state, _args);
                    
                    // Await the async Task's completion before continuing
                    await (Task)returnValue;

                    _instance.SetCoroutinePauseFlag(_state, CoroutinePauseFlag.BLUA_CSHARPASYNCAWAIT, false);

                    // If our Task has a return type, we need to push those to the Lua stack as the return value(/s)
                    Type returnValueType = returnValue.GetType();
                    if (typeof(Task).IsAssignableFrom(returnValueType))
                    {
                        PropertyInfo taskTypeResultPropertyInfo = returnValueType.GetProperty("Result");
                        if (taskTypeResultPropertyInfo != null)
                        {
                            object taskReturnValue = taskTypeResultPropertyInfo.GetValue(returnValue);
                            object[] taskReturnValues;

                            Type taskReturnType = taskReturnValue.GetType();
                            if (taskReturnType.IsTupleType()) // If type is Task<T1, T2, ...> (aka Task<Tuple<T1, T2, ...>>)
                            {
                                FieldInfo[] fieldInfos = taskReturnType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                                taskReturnValues = new object[fieldInfos.Length];
                                for (int i = 0; i < fieldInfos.Length; i++)
                                {
                                    taskReturnValues[i] = fieldInfos[i].GetValue(taskReturnValue);
                                }
                            }
                            else
                            {
                                taskReturnValues = new object[] { taskReturnValue };
                            }
                            
                            // Resume the thread and pass in any return values from the C# function
                            ResumeThread(_instance, _state, _state, taskReturnValues);
                            return;
                        }
                    }
                    
                    // Resume without passing any return values
                    ResumeThread(_instance, _state, _state);
                };

                _instance.SetCoroutinePauseFlag(_state, CoroutinePauseFlag.BLUA_CSHARPASYNCAWAIT, true);
                
                Task.Run(asyncTask);
                
                return YieldThread(_instance, _state, 0);
            }
            
            // Otherwise call the method normally
            object returnValue = InvokeMethodCallInfo(_methodCallInfo, _liveObject, _state, _args);
            bLuaUserData.PushReturnTypeOntoStack(_instance, _state, _methodCallInfo.returnType, returnValue);
            
            return LuaThreadStatus.LUA_YIELD;
        }

        private static object InvokeMethodCallInfo(MethodCallInfo _methodCallInfo, object _liveObject, IntPtr _state, params object[] _args)
        {
            ParameterInfo[] parameterInfos = _methodCallInfo.methodInfo.GetParameters();
            object[] newArgs = new object[parameterInfos.Length];
            int consumedOldArgIndex = 0;
            for (int i = 0; i < parameterInfos.Length; i++)
            {
                if (parameterInfos[i].GetCustomAttribute(typeof(bLuaStateParam)) != null
                    && parameterInfos[i].ParameterType == typeof(StateData))
                {
                    newArgs[i] = new StateData()
                    {
                        state = _state
                    };
                }
                else
                {
                    newArgs[i] = _args[consumedOldArgIndex++];
                }
            }
            _args = newArgs;
            
            if (_methodCallInfo is DelegateCallInfo delegateCallInfo)
            {
                return delegateCallInfo.multicastDelegate.DynamicInvoke(_args);
            }
            
            if (_methodCallInfo is GlobalMethodCallInfo globalMethodCallInfo)
            {
                return globalMethodCallInfo.methodInfo.Invoke(globalMethodCallInfo.objectInstance, _args);
            }
            
            return _methodCallInfo.methodInfo.Invoke(_liveObject, _args);
        }
        
        public static bool GetMethodInfoUpvalue(IntPtr _state, bLuaInstance _instance, out MethodCallInfo _methodInfo)
        {
            _methodInfo = null;

            int m = LuaLibAPI.lua_tointegerx(_state, UpValueIndex(1), IntPtr.Zero);
            if (m < 0 || m >= _instance.registeredMethods.Count)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_invalidMethodIndex}{m}");
                return false;
            }
            
            _methodInfo = _instance.registeredMethods[m];

            return true;
        }

        public static bool GetLiveObjectUpvalue(IntPtr _state, bLuaInstance _instance, out object _liveObject)
        {
            _liveObject = null;

            if (LuaLibAPI.lua_gettop(_state) < 1)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_stackIsEmpty}");
                return false;
            }

            DataType dataType = (DataType)LuaLibAPI.lua_type(_state, 1);
            if (dataType != DataType.UserData)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_objectIsNotUserdata}{dataType}");
                return false;
            }

            if (LuaLibAPI.lua_checkstack(_state, 1) == 0)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_stackHasNoRoom}");
                return false;
            }
            
            int liveObjectRefId = LuaLibAPI.lua_tointegerx(_state, UpValueIndex(2), IntPtr.Zero);
            if (liveObjectRefId < 0 || liveObjectRefId >= _instance.liveObjects.Length)
            {
                _instance.ErrorFromCSharp($"{bLuaError.error_invalidLiveObjectIndex}{liveObjectRefId}");
                return false;
            }

            _liveObject = _instance.liveObjects[liveObjectRefId];

            return true;
        }
        
        public static bool PopStackIntoArgs(bLuaInstance _instance, MethodCallInfo _methodInfo, out object[] _args, int _skipNum = 0)
        {
            int stackSize = LuaLibAPI.lua_gettop(_instance.state);
            
            _args = new object[_methodInfo.argTypes.Length];
            int argIndex = _args.Length - 1;
            
            // If we have a params argument, prepare our iterators for that data
            object[] parms = null;
            int parmsIndex = 0;
            int len = _methodInfo.argTypes.Length;
            if (len > 0 && _methodInfo.argTypes[len - 1] == MethodCallInfo.ParamType.Params)
            {
                len--;
                if (stackSize - _skipNum > len)
                {
                    parms = new object[stackSize - _skipNum - len];
                    parmsIndex = parms.Length - 1;
                }
            }

            // If we had a params argument, populate that argument with the params array
            if (parms != null)
            {
                _args[argIndex--] = parms;
            }
            
            // Populate arguments with defaults
            while (argIndex > stackSize - (1 + _skipNum))
            {
                _args[argIndex] = _methodInfo.defaultArgs[argIndex];
                --argIndex;
            }
            
            // Populate params from stack
            while (stackSize - (1 + _skipNum) > argIndex)
            {
                if (parms != null)
                {
                    parms[parmsIndex--] = PopStackIntoValue(_instance);
                }
                else
                {
                    PopStack(_instance);
                }
                --stackSize;
            }

            // Populate arguments from stack
            while (stackSize > _skipNum)
            {
                _args[argIndex] = bLuaUserData.PopStackIntoParamType(_instance, _methodInfo.argTypes[argIndex]);

                --stackSize;
                --argIndex;
            }
            
            return true;
        }
#endregion // Invocation
    }
} // bLua.NativeLua namespace
