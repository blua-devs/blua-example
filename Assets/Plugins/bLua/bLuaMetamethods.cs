using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using bLua;
using bLua.NativeLua;

namespace bLua
{
    public static class bLuaMetamethods
    {
        static bool GetUserDataEntry(IntPtr _originalState, bLuaInstance _instance, out UserDataRegistryEntry _userDataEntry)
        {
            _userDataEntry = new UserDataRegistryEntry();

            int n = LuaLibAPI.lua_tointegerx(_originalState, Lua.UpValueIndex(1), IntPtr.Zero);
            if (n < 0 || n >= _instance.s_entries.Count)
            {
                _instance.Error($"{bLuaError.error_invalidTypeIndex}{n}");
                return false;
            }

            _userDataEntry = _instance.s_entries[n];
            return true;
        }

        static bool GetUserDataPropertyEntry(IntPtr _originalState, bLuaInstance _instance, UserDataRegistryEntry _userDataEntry, out UserDataRegistryEntry.PropertyEntry _userDataPropertyEntry, out string _propertyName)
        {
            _propertyName = Lua.GetString(_originalState, 2);
            if (!_userDataEntry.properties.TryGetValue(_propertyName, out _userDataPropertyEntry))
            {
                _instance.Error($"{bLuaError.error_invalidProperty}{_propertyName}");
                return false;
            }

            return true;
        }

        static bool GetMethodCallInfo(bLuaInstance _instance, UserDataRegistryEntry _userDataEntry, string _methodName,  out MethodCallInfo _methodCallInfo)
        {
            _methodCallInfo = new MethodCallInfo();

            UserDataRegistryEntry.PropertyEntry propertyEntry;
            if (!_userDataEntry.properties.TryGetValue(_methodName, out propertyEntry))
            {
                _instance.Error($"{bLuaError.error_invalidMethod}{_methodName}");
                return false;
            }

            if (propertyEntry.index < 0 || propertyEntry.index >= _instance.s_methods.Count)
            {
                _instance.Error($"{bLuaError.error_invalidMethod}{_methodName}");
                return false;
            }

            _methodCallInfo = _instance.s_methods[propertyEntry.index];
            return true;
        }

        static bool GetLiveObjectInstance(IntPtr _originalState, bLuaInstance _instance, out object _object)
        {
            _object = null;

            int t = LuaLibAPI.lua_type(_originalState, 1);
            if (t != (int)DataType.UserData)
            {
                _instance.Error($"{bLuaError.error_objectIsNotUserdata}{(DataType)t}");
                return false;
            }

            LuaLibAPI.lua_checkstack(_originalState, 1);
            int res = LuaLibAPI.lua_getiuservalue(_originalState, 1, 1);
            if (res != (int)DataType.Number)
            {
                _instance.Error($"{bLuaError.error_objectNotProvided}");
                return false;
            }

            int instanceIndex = Lua.PopInteger(_instance);
            _object = _instance.s_liveObjects[instanceIndex];
            return true;
        }

        public static int Metamethod_Addition(IntPtr _state)
        {
            return 0;
        }

        public static int Metamethod_Subtraction(IntPtr _state)
        {
            return 0;
        }

        public static int Metamethod_Multiplication(IntPtr _state)
        {
            return 0;
        }

        public static int Metamethod_Division(IntPtr _state)
        {
            return 0;
        }

        public static int Metamethod_NegationUnary(IntPtr _state)
        {
            return 0;
        }

        public static int Metamethod_Concatenation(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                bLuaValue operandR = Lua.PopStackIntoValue(mainThreadInstance);
                bLuaValue operandL = Lua.PopStackIntoValue(mainThreadInstance);
                string lhs = operandL.CastToString();
                string rhs = operandR.CastToString();
                string result = lhs + rhs;

                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, MethodCallInfo.ParamType.Str, result);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        public static int Metamethod_Length(IntPtr _state)
        {
            return 0;
        }

        public static int Metamethod_Equal(IntPtr _state)
        {
            return 0;
        }

        public static int Metamethod_LessThan(IntPtr _state)
        {
            return 0;
        }

        public static int Metamethod_LessEqual(IntPtr _state)
        {
            return 0;
        }

        public static int Metamethod_Index(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                UserDataRegistryEntry userDataInfo;
                if (!GetUserDataEntry(_state, mainThreadInstance, out userDataInfo))
                {
                    return 0;
                }

                string propertyName;
                UserDataRegistryEntry.PropertyEntry propertyEntry;
                if (!GetUserDataPropertyEntry(_state, mainThreadInstance, userDataInfo, out propertyEntry, out propertyName))
                {
                    return 0;
                }

                switch (propertyEntry.propertyType)
                {
                    case UserDataRegistryEntry.PropertyEntry.Type.Method:
                        {
                            int val = Lua.PushStack(mainThreadInstance, mainThreadInstance.s_methods[propertyEntry.index].closure);
                            return 1;
                        }
                    case UserDataRegistryEntry.PropertyEntry.Type.Property:
                        {
                            object obj;
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out obj))
                            {
                                return 0;
                            }

                            PropertyCallInfo propertyInfo = mainThreadInstance.s_properties[propertyEntry.index];
                            object result = propertyInfo.propertyInfo.GetMethod.Invoke(obj, null);
                            bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, propertyInfo.propertyType, result);
                            return 1;
                        }
                    case UserDataRegistryEntry.PropertyEntry.Type.Field:
                        {
                            object obj;
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out obj))
                            {
                                return 0;
                            }

                            FieldCallInfo fieldInfo = mainThreadInstance.s_fields[propertyEntry.index];
                            object result = fieldInfo.fieldInfo.GetValue(obj);
                            bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, fieldInfo.fieldType, result);
                            return 1;
                        }
                }

                Lua.PushNil(mainThreadInstance);
                return 1;
            }
            catch (Exception e)
            {
                mainThreadInstance.ExceptionError(e, bLuaError.error_objectNotProvided);
                Lua.PushNil(mainThreadInstance);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        public static int Metamethod_NewIndex(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                UserDataRegistryEntry userDataInfo;
                if (!GetUserDataEntry(_state, mainThreadInstance, out userDataInfo))
                {
                    return 0;
                }

                string propertyName;
                UserDataRegistryEntry.PropertyEntry propertyEntry;
                if (!GetUserDataPropertyEntry(_state, mainThreadInstance, userDataInfo, out propertyEntry, out propertyName))
                {
                    return 0;
                }

                switch (propertyEntry.propertyType)
                {
                    case UserDataRegistryEntry.PropertyEntry.Type.Property:
                        {
                            object obj;
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out obj))
                            {
                                return 0;
                            }

                            PropertyCallInfo propertyInfo = mainThreadInstance.s_properties[propertyEntry.index];
                            object[] args = new object[1] { bLuaUserData.PopStackIntoParamType(mainThreadInstance, propertyInfo.propertyType) };
                            propertyInfo.propertyInfo.SetMethod.Invoke(obj, args);
                            return 0;
                        }
                    case UserDataRegistryEntry.PropertyEntry.Type.Field:
                        {
                            object obj;
                            if (!GetLiveObjectInstance(_state, mainThreadInstance, out obj))
                            {
                                return 0;
                            }

                            FieldCallInfo fieldInfo = mainThreadInstance.s_fields[propertyEntry.index];
                            object arg = bLuaUserData.PopStackIntoParamType(mainThreadInstance, fieldInfo.fieldType);
                            fieldInfo.fieldInfo.SetValue(obj, arg);
                            return 0;
                        }
                }

                mainThreadInstance.Error($"{bLuaError.error_setProperty}{propertyName}");
                return 0;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }

        public static int MetaMethod_GC(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));

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

        public static int Metamethod_ToString(IntPtr _state)
        {
            bLuaInstance mainThreadInstance = bLuaInstance.GetInstanceByState(Lua.GetMainThread(_state));
            IntPtr revertState = mainThreadInstance.state;

            try
            {
                mainThreadInstance.state = _state;

                UserDataRegistryEntry userDataInfo;
                if (!GetUserDataEntry(_state, mainThreadInstance, out userDataInfo))
                {
                    Lua.PushNil(mainThreadInstance);
                    return 0;
                }

                MethodCallInfo methodCallInfo;
                if (!GetMethodCallInfo(mainThreadInstance, userDataInfo, "ToString", out methodCallInfo))
                {
                    Lua.PushNil(mainThreadInstance);
                    return 0;
                }

                object obj;
                if (!GetLiveObjectInstance(_state, mainThreadInstance, out obj))
                {
                    Lua.PushNil(mainThreadInstance);
                    return 0;
                }

                object result = methodCallInfo.methodInfo.Invoke(obj, new object[0]);
                bLuaUserData.PushReturnTypeOntoStack(mainThreadInstance, MethodCallInfo.ParamType.Str, result);
                return 1;
            }
            finally
            {
                mainThreadInstance.state = revertState;
            }
        }
    }
} // bLua namespace
