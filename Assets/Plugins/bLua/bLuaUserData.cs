using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using bLua.NativeLua;

namespace bLua
{
    [System.AttributeUsage(System.AttributeTargets.Method | System.AttributeTargets.Property | System.AttributeTargets.Field)]
    public sealed class bLuaHiddenAttribute : System.Attribute
    {
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public sealed class bLuaUserDataAttribute : System.Attribute
    {
    }

    [bLuaUserData]
    public class TestUserDataClass
    {
        public int n = 4;
        public bLuaValue MyFunction(int x=5)
        {
            return bLuaValue.CreateNumber(n+x);
        }

        //see if creating one and returning it by class works.
        public static TestUserDataClass Create(int x)
        {
            return new TestUserDataClass()
            {
                n = x,
            };
        }

        public string AddStrings(bLuaValue a, string b)
        {
            return a.String + b;
        }

        public int VarArgsFunction(int a, params object[] b)
        {
            int result = a;
            foreach (var v in b)
            {
                result += (int)(double)v;
            }
            return result;
        }

        public int VarArgsParamsFunction(int a, params object[] b)
        {
            Debug.Log($"VarArgs: {b.Length}");
            foreach (var v in b)
            {
                Debug.Log($"VarArgs: Type {v.GetType().Name}");
            }

            return 0;
        }

        public static int StaticFunction(bLuaValue a, int b=2, int c=2)
        {
            return a.Integer + b + c;
        }

        public int propertyTest
        {
            get
            {
                return n + 8;
            }
            set
            {
                n = value - 8;
            }
        }
    }

    [bLuaUserData]
    public class TestUserDataClassDerived : TestUserDataClass
    {
        public int x
        {
            get
            {
                return 9;
            }
        }
    }


    public class UserDataRegistryEntry
    {
        public bLuaValue metatable;

        public class PropertyEntry
        {
            public enum Type
            {
                Method,
                Property,
                Field,
            };
            public Type propertyType;
            public int index;
        }

        public Dictionary<string, PropertyEntry> properties = new Dictionary<string, PropertyEntry>();
    }

    class MethodCallInfo
    {
        public MethodInfo methodInfo;

        public enum ParamType
        {
            Void, UserDataClass, LuaValue, Int, Str, Double, Bool, Float, Params,
        }

        public ParamType returnType;

        public ParamType[] argTypes;
        public object[] defaultArgs;

        public bLuaValue closure;
    }

    class PropertyCallInfo
    {
        public PropertyInfo propertyInfo;

        public MethodCallInfo.ParamType propertyType;
    }

    class FieldCallInfo
    {
        public FieldInfo fieldInfo;

        public MethodCallInfo.ParamType fieldType;
    }



    public class bLuaUserData
    {
        //whenever we add a method we just add it to this list to be indexed.
        static List<MethodCallInfo> s_methods = new List<MethodCallInfo>();
        static List<PropertyCallInfo> s_properties = new List<PropertyCallInfo>();
        static List<FieldCallInfo> s_fields = new List<FieldCallInfo>();


        static List<UserDataRegistryEntry> s_entries = new List<UserDataRegistryEntry>();
        static Dictionary<string, int> s_typenameToEntryIndex = new Dictionary<string, int>();
        static bLuaValue _gc;

        static object[] s_liveObjects = new object[65536];

        static List<int> s_liveObjectsFreeList = new List<int>();

        static int s_nNextLiveObject = 1;

        public static int numLiveObjects
        {
            get
            {
                return (s_nNextLiveObject - 1) - s_liveObjectsFreeList.Count;
            }
        }

        static int CallFunction(System.IntPtr state)
        {
            var stateBack = bLuaNative._state;
            bLuaNative._state = state;

            try
            {
                int stackSize = LuaLibAPI.lua_gettop(state);
                if (stackSize == 0 || LuaLibAPI.lua_type(state, 1) != (int)DataType.UserData)
                {
                    bLuaNative.Error($"Object not provided when calling function.");
                    return 0;
                }

                int n = LuaLibAPI.lua_tointegerx(state, Lua.UpValueIndex(1), System.IntPtr.Zero);

                if (n < 0 || n >= s_methods.Count)
                {
                    bLuaNative.Error($"Illegal method index: {n}");
                    return 0;
                }

                MethodCallInfo info = s_methods[n];

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

                while(argIndex > stackSize - 2)
                {
                    //backfill any arguments with defaults.
                    args[argIndex] = info.defaultArgs[argIndex];
                    --argIndex;
                }
                while(stackSize-2 > argIndex)
                {
                    if (parms != null)
                    {
                        parms[parmsIndex--] = Lua.PopStackIntoObject();
                    }
                    else
                    {
                        Lua.PopStack();
                    }
                    --stackSize;
                }

                while (stackSize > 1)
                {
                    args[argIndex] = PopStackIntoParamType(info.argTypes[argIndex]);

                    --stackSize;
                    --argIndex;
                }

                if (LuaLibAPI.lua_gettop(state) < 1)
                {
                    bLuaNative.Error($"Stack is empty");
                    return 0;
                }

                int t = LuaLibAPI.lua_type(state, 1);
                if (t != (int)DataType.UserData)
                {
                    bLuaNative.Error($"Object is not a user data: {((DataType)t).ToString()}");
                    return 0;
                }

                LuaLibAPI.lua_checkstack(state, 1);
                int res = LuaLibAPI.lua_getiuservalue(state, 1, 1);
                if (res != (int)DataType.Number)
                {
                    bLuaNative.Error($"Object not provided when calling function.");
                    return 0;
                }
                int liveObjectIndex = LuaLibAPI.lua_tointegerx(state, -1, System.IntPtr.Zero);

                object obj = s_liveObjects[liveObjectIndex];

                object result = info.methodInfo.Invoke(obj, args);

                PushReturnTypeOntoStack(info.returnType, result);
                return 1;

            } catch(System.Exception e)
            {
                var ex = e.InnerException;
                if (ex == null)
                {
                    ex = e;
                }
                bLuaNative.Error($"Error calling function: {ex.Message}", $"{ex.StackTrace}");
                return 0;
            } finally
            {
                bLuaNative._state = stateBack;
            }
        }

        static int CallStaticFunction(System.IntPtr state)
        {
            var stateBack = bLuaNative._state;
            bLuaNative._state = state;

            try
            {
                int n = LuaLibAPI.lua_tointegerx(state, Lua.UpValueIndex(1), System.IntPtr.Zero);
                MethodCallInfo info = s_methods[n];

                int stackSize = LuaLibAPI.lua_gettop(state);

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
                while(stackSize-1 > argIndex)
                {
                    if (parms != null)
                    {
                        parms[parmsIndex--] = Lua.PopStackIntoObject();
                    }
                    else
                    {
                        Lua.PopStack();
                    }
                    --stackSize;
                }

                while (stackSize > 0)
                {
                    args[argIndex] = PopStackIntoParamType(info.argTypes[argIndex]);
                    --stackSize;
                    --argIndex;
                }

                object result = info.methodInfo.Invoke(null, args);

                PushReturnTypeOntoStack(info.returnType, result);
                return 1;

            } catch(System.Exception e)
            {
                var ex = e;
                while(ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }
                bLuaNative.Error($"Error calling function: {ex.Message}", $"{ex.StackTrace}");
                return 0;
            } finally
            {
                bLuaNative._state = stateBack;
            }
        }

        static void PushReturnTypeOntoStack(MethodCallInfo.ParamType returnType, object result)
        {
            switch (returnType)
            {
                case MethodCallInfo.ParamType.Void:
                    Lua.PushNil();
                    return;
                case MethodCallInfo.ParamType.LuaValue:
                    {
                        Lua.PushStack(result as bLuaValue);
                        return;
                    }
                case MethodCallInfo.ParamType.Bool:
                    {
                        Lua.PushObjectOntoStack((bool)result);
                        return;
                    }
                case MethodCallInfo.ParamType.Int:
                    {
                        Lua.PushObjectOntoStack((int)result);
                        return;
                    }
                case MethodCallInfo.ParamType.Float:
                    {
                        Lua.PushObjectOntoStack((float)result);
                        return;
                    }
                case MethodCallInfo.ParamType.Double:
                    {
                        Lua.PushObjectOntoStack((double)result);
                        return;
                    }
                case MethodCallInfo.ParamType.Str:
                    {
                        Lua.PushObjectOntoStack((string)result);
                        return;
                    }
                case MethodCallInfo.ParamType.UserDataClass:
                    {
                        PushNewUserData(result);
                        return;
                    }
                default:
                    Lua.PushNil();
                    return;
            }
        }

        static int IndexFunction(System.IntPtr state)
        {
            var stateBack = bLuaNative._state;
            try
            {
                bLuaNative._state = state;

                int n = LuaLibAPI.lua_tointegerx(state, Lua.UpValueIndex(1), System.IntPtr.Zero);

                if (n < 0 || n >= s_entries.Count)
                {
                    bLuaNative.Error($"Invalid type index in lua: {n}");
                    return 0;
                }

                UserDataRegistryEntry userDataInfo = s_entries[n];

                string str = Lua.GetString(state, 2);

                UserDataRegistryEntry.PropertyEntry propertyEntry;
                if (userDataInfo.properties.TryGetValue(str, out propertyEntry))
                {
                    switch (propertyEntry.propertyType)
                    {
                        case UserDataRegistryEntry.PropertyEntry.Type.Method:
                            int val = Lua.PushStack(s_methods[propertyEntry.index].closure);
                            return 1;
                        case UserDataRegistryEntry.PropertyEntry.Type.Property:
                            {
                                //get the iuservalue for the userdata onto the stack.
                                LuaLibAPI.lua_checkstack(state, 1);
                                LuaLibAPI.lua_getiuservalue(state, 1, 1);

                                int instanceIndex = Lua.PopInteger();
                                object obj = s_liveObjects[instanceIndex];

                                var propertyInfo = s_properties[propertyEntry.index];
                                var result = propertyInfo.propertyInfo.GetMethod.Invoke(obj, null);
                                PushReturnTypeOntoStack(propertyInfo.propertyType, result);
                                return 1;
                            }
                        case UserDataRegistryEntry.PropertyEntry.Type.Field:
                            {
                                //get the iuservalue for the userdata onto the stack.
                                LuaLibAPI.lua_checkstack(state, 1);
                                LuaLibAPI.lua_getiuservalue(state, 1, 1);
                                int instanceIndex = Lua.PopInteger();
                                object obj = s_liveObjects[instanceIndex];

                                var fieldInfo = s_fields[propertyEntry.index];
                                var result = fieldInfo.fieldInfo.GetValue(obj);
                                PushReturnTypeOntoStack(fieldInfo.fieldType, result);
                                return 1;
                            }
                    }
                }

                Lua.PushNil();

                return 1;
            } catch(System.Exception e)
            {
                var ex = e.InnerException;
                if (ex == null)
                {
                    ex = e;
                }
                bLuaNative.Error("Error indexing userdata", $"Error in index: {ex.Message} {ex.StackTrace}");
                Lua.PushNil();
                return 1;
            } finally
            {
                bLuaNative._state = stateBack;
            }
        }

        static public object GetUserDataObject(int nstack)
        {
            LuaLibAPI.lua_checkstack(bLuaNative._state, 1);
            int ntype = LuaLibAPI.lua_getiuservalue(bLuaNative._state, nstack, 1);
            if (ntype != (int)DataType.Number)
            {
                bLuaNative.Error($"Could not find valid user data object");
                Lua.PopStack();
                return null;
            }

            int liveObjectIndex = LuaLibAPI.lua_tointegerx(bLuaNative._state, -1, System.IntPtr.Zero);

            object obj = s_liveObjects[liveObjectIndex];

            Lua.PopStack();

            return obj;
        }

        static int SetIndexFunction(System.IntPtr state)
        {
            var stateBack = bLuaNative._state;
            bLuaNative._state = state;

            try
            {
                int n = LuaLibAPI.lua_tointegerx(state, Lua.UpValueIndex(1), System.IntPtr.Zero);

                if (n < 0 || n >= s_entries.Count)
                {
                    bLuaNative.Error($"Invalid type index in lua: {n}");
                    return 0;
                }

                UserDataRegistryEntry userDataInfo = s_entries[n];

                string str = Lua.GetString(state, 2);

                UserDataRegistryEntry.PropertyEntry propertyEntry;
                if (userDataInfo.properties.TryGetValue(str, out propertyEntry))
                {
                    if (propertyEntry.propertyType == UserDataRegistryEntry.PropertyEntry.Type.Property)
                    {
                        LuaLibAPI.lua_checkstack(state, 1);
                        //get the iuservalue for the userdata onto the stack.
                        LuaLibAPI.lua_getiuservalue(state, 1, 1);
                        int instanceIndex = Lua.PopInteger();
                        object obj = s_liveObjects[instanceIndex];

                        object[] args = new object[1];

                        var propertyInfo = s_properties[propertyEntry.index];
                        args[0] = PopStackIntoParamType(propertyInfo.propertyType);


                        propertyInfo.propertyInfo.SetMethod.Invoke(obj, args);
                        return 0;
                    }
                    else if (propertyEntry.propertyType == UserDataRegistryEntry.PropertyEntry.Type.Field)
                    {
                        LuaLibAPI.lua_checkstack(state, 1);
                        //get the iuservalue for the userdata onto the stack.
                        LuaLibAPI.lua_getiuservalue(state, 1, 1);
                        int instanceIndex = Lua.PopInteger();
                        object obj = s_liveObjects[instanceIndex];

                        var fieldInfo = s_fields[propertyEntry.index];
                        var arg = PopStackIntoParamType(fieldInfo.fieldType);

                        fieldInfo.fieldInfo.SetValue(obj, arg);
                        return 0;
                    }

                }

                bLuaNative.Error($"Could not set property {str}");
                return 0;
            } finally
            {
                bLuaNative._state = stateBack;
            }
        }

        static int GCFunction(System.IntPtr state)
        {
            LuaLibAPI.lua_checkstack(state, 1);
            LuaLibAPI.lua_getiuservalue(state, 1, 1);
            int n = LuaLibAPI.lua_tointegerx(bLuaNative._state, -1, System.IntPtr.Zero);
            s_liveObjects[n] = null;
            s_liveObjectsFreeList.Add(n);
            return 0;
        }

#if UNITY_EDITOR
        struct DebugEntry
        {
            public int count;
            public string typeName;
        }
        public static void DebugInfoReport()
        {
            List<DebugEntry> entries = new List<DebugEntry>();
            Dictionary<string, int> types = new Dictionary<string, int>();
            foreach (var p in s_liveObjects)
            {
                if (p == null)
                {
                    continue;
                }
                string typeName = p.GetType().Name;
                int count = 0;
                if (types.ContainsKey(typeName))
                {
                    count = types[typeName];
                }

                ++count;
                types[typeName] = count;
            }

            foreach (var p in types)
            {
                entries.Add(new DebugEntry()
                {
                    typeName = p.Key,
                    count = p.Value,
                });
            }

            entries.Sort((a,b) =>
            {
                return a.count.CompareTo(b.count);
            });

            foreach (var entry in entries)
            {
                Debug.Log($"LiveObject: {entry.typeName} -> {entry.count}");
            }
        }
#endif

        public static void Init()
        {
            DeInit();

            _gc = bLuaValue.CreateFunction(GCFunction);
            RegisterAllAssemblies();
        }

        public static void DeInit()
        {
            _gc = null;
            s_methods.Clear();
            s_properties.Clear();
            s_fields.Clear();
            s_entries.Clear();
            s_typenameToEntryIndex.Clear();
        }


        public static void PushNewUserData(object obj)
        {
            if (obj == null)
            {
                Lua.PushNil();
                return;
            }
            int typeIndex;
            if (s_typenameToEntryIndex.TryGetValue(obj.GetType().Name, out typeIndex) == false)
            {
                bLuaNative.Error($"Type {obj.GetType().Name} is not marked as a user data. Add [bLuaUserData] to its definition.");
                Lua.PushNil();
                return;
            }

            UserDataRegistryEntry entry = s_entries[typeIndex];

            //work out the index of the new object.
            int objIndex;
            if (s_liveObjectsFreeList.Count > 0)
            {
                objIndex = s_liveObjectsFreeList[s_liveObjectsFreeList.Count - 1];
                s_liveObjectsFreeList.RemoveAt(s_liveObjectsFreeList.Count - 1);
            }
            else
            {
                if (s_nNextLiveObject >= s_liveObjects.Length)
                {
                    object[] liveObjects = new object[s_liveObjects.Length * 2];
                    for (int i = 0; i != s_liveObjects.Length; ++i)
                    {
                        liveObjects[i] = s_liveObjects[i];
                    }

                    s_liveObjects = liveObjects;
                }

                objIndex = s_nNextLiveObject;
                s_nNextLiveObject++;
            }

            LuaLibAPI.lua_newuserdatauv(bLuaNative._state, new System.IntPtr(8), 1);
            Lua.PushObjectOntoStack(objIndex);
            LuaLibAPI.lua_setiuservalue(bLuaNative._state, -2, 1);
            Lua.PushStack(entry.metatable);
            LuaLibAPI.lua_setmetatable(bLuaNative._state, -2);

            string msg = Lua.TraceMessage("live object");

            s_liveObjects[objIndex] = obj;
        }

        public static void RegisterAllAssemblies()
        {
            foreach (Assembly asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                RegisterAssembly(asm);
            }
        }

        public static void RegisterAssembly(Assembly asm)
        {
            foreach (var t in asm.DefinedTypes)
            {
                if (t.IsClass && t.GetCustomAttribute(typeof(bLuaUserDataAttribute)) != null) {
                    Register(t);
                }
            }
        }

        public static void Register(System.Type t)
        {
            if (s_typenameToEntryIndex.ContainsKey(t.Name))
            {
                //can't register the same type multiple times.
                return;
            }

            Dictionary<string, UserDataRegistryEntry.PropertyEntry> baseProperties = new Dictionary<string, UserDataRegistryEntry.PropertyEntry>();

            if (t.BaseType != null && t.BaseType != t)
            {
                if (t.BaseType.IsClass && t.BaseType.GetCustomAttribute(typeof(bLuaUserDataAttribute)) != null)
                {
                    Register(t.BaseType);

                    baseProperties = new Dictionary<string, UserDataRegistryEntry.PropertyEntry>(s_entries[s_typenameToEntryIndex[t.BaseType.Name]].properties);
                }
            }

            int typeIndex = s_entries.Count;

            UserDataRegistryEntry entry = new UserDataRegistryEntry()
            {
                properties = baseProperties,
            };
            entry.metatable = Lua.NewMetaTable(t.Name);
            entry.metatable.Set("__index", bLuaValue.CreateClosure(IndexFunction, bLuaValue.CreateNumber(typeIndex)));
            entry.metatable.Set("__newindex", bLuaValue.CreateClosure(SetIndexFunction, bLuaValue.CreateNumber(typeIndex)));
            entry.metatable.Set("__gc", _gc);

            s_typenameToEntryIndex[t.Name] = typeIndex;

            s_entries.Add(entry);

            MethodInfo[] methods = t.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public);
            foreach (var methodInfo in methods) {
                System.Attribute hiddenAttr = methodInfo.GetCustomAttribute(typeof(bLuaHiddenAttribute));
                if (hiddenAttr != null)
                {
                    continue;
                }

                var methodParams = methodInfo.GetParameters();

                MethodCallInfo.ParamType[] argTypes = new MethodCallInfo.ParamType[methodParams.Length];
                object[] defaultArgs = new object[methodParams.Length];
                for (int i = 0; i != methodParams.Length; ++i)
                {
                    argTypes[i] = SystemTypeToParamType(methodParams[i].ParameterType);
                    if (i == methodParams.Length-1 && methodParams[i].GetCustomAttribute(typeof(System.ParamArrayAttribute)) != null)
                    {
                        argTypes[i] = MethodCallInfo.ParamType.Params;
                    }

                    if (methodParams[i].HasDefaultValue) {
                        defaultArgs[i] = methodParams[i].DefaultValue;
                    } else if (argTypes[i] == MethodCallInfo.ParamType.LuaValue)
                    {
                        defaultArgs[i] = bLuaValue.Nil;
                    } else 
                    {
                        defaultArgs[i] = null;
                    }
                }

                MethodCallInfo.ParamType returnType = SystemTypeToParamType(methodInfo.ReturnType);

                entry.properties[methodInfo.Name] = new UserDataRegistryEntry.PropertyEntry()
                {
                    propertyType = UserDataRegistryEntry.PropertyEntry.Type.Method,
                    index = s_methods.Count,
                };


                Lua.LuaCFunction fn;
                if (methodInfo.IsStatic)
                {
                    fn = CallStaticFunction;
                } else
                {
                    fn = CallFunction;
                }
                s_methods.Add(new MethodCallInfo()
                {
                    methodInfo = methodInfo,
                    returnType = returnType,
                    argTypes = argTypes,
                    defaultArgs = defaultArgs,
                    closure = bLuaValue.CreateClosure(fn, bLuaValue.CreateNumber(s_methods.Count)),
                });
            }

            PropertyInfo[] properties = t.GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            foreach (var propertyInfo in properties)
            {
                System.Attribute hiddenAttr = propertyInfo.GetCustomAttribute(typeof(bLuaHiddenAttribute));
                if (hiddenAttr != null)
                {
                    continue;
                }

                MethodCallInfo.ParamType returnType = SystemTypeToParamType(propertyInfo.PropertyType);
                if (returnType == MethodCallInfo.ParamType.Void)
                {
                    continue;
                }

                entry.properties[propertyInfo.Name] = new UserDataRegistryEntry.PropertyEntry()
                {
                    propertyType = UserDataRegistryEntry.PropertyEntry.Type.Property,
                    index = s_properties.Count,
                };

                s_properties.Add(new PropertyCallInfo()
                {
                    propertyInfo = propertyInfo,
                    propertyType = returnType,
                });
            }

            FieldInfo[] fields = t.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance);
            foreach (var fieldInfo in fields)
            {
                System.Attribute hiddenAttr = fieldInfo.GetCustomAttribute(typeof(bLuaHiddenAttribute));
                if (hiddenAttr != null)
                {
                    continue;
                }

                MethodCallInfo.ParamType returnType = SystemTypeToParamType(fieldInfo.FieldType);
                if (returnType == MethodCallInfo.ParamType.Void)
                {
                    continue;
                }

                entry.properties[fieldInfo.Name] = new UserDataRegistryEntry.PropertyEntry()
                {
                    propertyType = UserDataRegistryEntry.PropertyEntry.Type.Field,
                    index = s_fields.Count,
                };

                s_fields.Add(new FieldCallInfo()
                {
                    fieldInfo = fieldInfo,
                    fieldType = returnType,
                });
            }
        }

        static object PopStackIntoParamType(MethodCallInfo.ParamType t)
        {
            switch (t)
            {
                case MethodCallInfo.ParamType.Bool:
                    return Lua.PopBool();
                case MethodCallInfo.ParamType.Double:
                    return Lua.PopNumber();
                case MethodCallInfo.ParamType.Float:
                    return (float)Lua.PopNumber();
                case MethodCallInfo.ParamType.Int:
                    return Lua.PopInteger();
                case MethodCallInfo.ParamType.Str:
                    return Lua.PopString();
                case MethodCallInfo.ParamType.LuaValue:
                    return Lua.PopStackIntoValue();
                case MethodCallInfo.ParamType.UserDataClass:
                    return Lua.PopStackIntoValue();
                default:
                    Lua.PopStack();
                    return null;
            }
        }

        static MethodCallInfo.ParamType SystemTypeToParamType(System.Type t)
        {
            if (t == typeof(void))
            {
                return MethodCallInfo.ParamType.Void;
            }
            else if (t == typeof(int))
            {
                return MethodCallInfo.ParamType.Int;
            }
            else if (t == typeof(double))
            {
                return MethodCallInfo.ParamType.Double;
            }
            else if (t == typeof(float))
            {
                return MethodCallInfo.ParamType.Float;
            }
            else if (t == typeof(string))
            {
                return MethodCallInfo.ParamType.Str;
            }
            else if (t == typeof(bool))
            {
                return MethodCallInfo.ParamType.Bool;
            }
            else if (t == typeof(bLuaValue))
            {
                return MethodCallInfo.ParamType.LuaValue;
            }
            else if (t.IsClass && t.GetCustomAttribute(typeof(bLuaUserDataAttribute)) != null)
            {
                return MethodCallInfo.ParamType.UserDataClass;
            }
            else
            {
                return MethodCallInfo.ParamType.Void;
            }
        }
    }
} // bLua namespace
