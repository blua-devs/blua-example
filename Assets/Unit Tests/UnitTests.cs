using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using bLua;
using System;
#if UNITY_EDITOR
using UnityEditor;
#endif

[bLuaUserData]
public class TestUserDataClass
{
    public int n = 4;
    public int MyFunction(int x = 5)
    {
        return n + x;
    }

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

    public static int StaticFunction(bLuaValue a, int b = 2, int c = 2)
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

#if UNITY_EDITOR
[CustomEditor(typeof(UnitTests))]
public class UnitTestsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(10);

        if (GUILayout.Button("Run Unit Tests"))
        {
            UnitTests unitTests = (UnitTests)target;
            if (unitTests)
            {
                unitTests.RunUnitTests();
            }
        }
        if (GUILayout.Button("Run Test Coroutine"))
        {
            UnitTests unitTests = (UnitTests)target;
            if (unitTests)
            {
                unitTests.RunTestCoroutine();
            }
        }
        if (GUILayout.Button("Run Threading Macros"))
        {
            UnitTests unitTests = (UnitTests)target;
            if (unitTests)
            {
                unitTests.RunThreadMacros();
            }
        }
    }
}
#endif // UNITY_EDITOR

public class UnitTests : MonoBehaviour
{


    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            RunUnitTests();
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            RunTestCoroutine();
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            RunThreadMacros();
        }
    }

    private void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 300, 150), "Unit Test Play Helper");

        GUI.Label(new Rect(20, 35, 260, 60), "Click `Unit Tests` in the Hierarchy, then view the Details panel for options. Open the Window > General > Console to view results.");

        GUI.Label(new Rect(20, 90, 260, 20), "Press 1 to run Unit Tests");
        GUI.Label(new Rect(20, 110, 260, 20), "Press 2 to run Test Coroutine");
        GUI.Label(new Rect(20, 130, 260, 20), "Press 3 to run Thread Macros");
    }

    public void RunUnitTests()
    {
        bLuaInstance instance = new bLuaInstance(new bLuaSettings()
        {
            features = bLuaSettings.SANDBOX_ALL,
            internalVerbosity = bLuaSettings.InternalErrorVerbosity.Verbose
        });

        Debug.Log("Starting Unit Tests");

        int stackSize = bLua.NativeLua.LuaLibAPI.lua_gettop(instance.state);

        instance.ExecBuffer("unit_tests",
            @"MyFunctions = {}

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

            function test_userdata(u)
                return u.StaticFunction(0) + u.StaticFunction(2,3,4) + u:MyFunction() + u.propertyTest
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

        using (bLuaValue fn = instance.GetGlobal("myfunction"))
        {
            var result = instance.Call(fn, 8);
            Assert.AreEqual(result.Number, 13.0);
        }

        using (bLuaValue fn = instance.FullLookup(instance.GetGlobal("MyFunctions"), "blah"))
        {
            Assert.AreEqual(instance.Call(fn, 12).Number, 12.0);

            Assert.AreEqual(bLua.NativeLua.LuaLibAPI.lua_gettop(instance.state), stackSize);
        }

        using (bLuaValue fn = instance.GetGlobal("make_table"))
        {
            bLuaValue t = instance.Call(fn);
            Dictionary<string, bLuaValue> tab = t.Dict();
            Assert.AreEqual(tab.Count, 3);
            Assert.AreEqual(tab["abc"].Number, 9);
        }

        using (bLuaValue fn = instance.GetGlobal("add_from_table"))
        {
            bLuaValue v = bLuaValue.CreateTable(instance);
            v.Set("a", bLuaValue.CreateNumber(instance, 4));
            v.Set("b", bLuaValue.CreateNumber(instance, 5));
            v.Set("c", bLuaValue.CreateNumber(instance, 6));
            bLuaValue t = instance.Call(fn, v);
            Assert.AreEqual(t.Number, 15);
        }

        using (bLuaValue fn = bLuaValue.CreateFunction(instance, TestCFunction))
        {
            Assert.AreEqual(instance.Call(fn).Number, 5);
        }

        using (bLuaValue fn = instance.GetGlobal("test_userdata"))
        {
            var userdata = bLuaValue.CreateUserData(instance, new TestUserDataClass() { n = 7 });
            Assert.AreEqual(instance.Call(fn, userdata).Number, 40);
            using (bLuaValue fn2 = instance.GetGlobal("incr_userdata"))
            {
                instance.Call(fn2, userdata);
                Assert.AreEqual(instance.Call(fn, userdata).Number, 42);
            }
        }

        using (bLuaValue fn = instance.GetGlobal("test_addstrings"))
        {
            var userdata = bLuaValue.CreateUserData(instance, new TestUserDataClass() { n = 7 });
            Assert.AreEqual(instance.Call(fn, userdata, "abc:", bLuaValue.CreateString(instance, "def")).String, "abc:def");
        }

        using (bLuaValue fn = instance.GetGlobal("test_varargs"))
        {
            var userdata = bLuaValue.CreateUserData(instance, new TestUserDataClass() { n = 7 });
            Assert.AreEqual(instance.Call(fn, userdata).Number, 20);
        }

        using (bLuaValue fn = instance.GetGlobal("test_field"))
        {
            var userdata = bLuaValue.CreateUserData(instance, new TestUserDataClass() { n = 7 });
            Assert.AreEqual(instance.Call(fn, userdata).Number, 9.0);
        }

        using (bLuaValue fn = instance.GetGlobal("test_field"))
        {
            var userdata = bLuaValue.CreateUserData(instance, new TestUserDataClassDerived() { n = 7 });
            Assert.AreEqual(instance.Call(fn, userdata).Number, 9.0);
        }

        using (bLuaValue fn = instance.GetGlobal("test_classproperty"))
        {
            var userdata = bLuaValue.CreateUserData(instance, new TestUserDataClassDerived() { n = 7 });
            Assert.AreEqual(instance.Call(fn, userdata, 7.0).Number, 7.0);
        }

        Assert.AreEqual(bLua.NativeLua.LuaLibAPI.lua_gettop(instance.state), stackSize);
        
        Debug.Log("Finished Unit Tests");
    }

    public void RunTestCoroutine()
    {
        bLuaInstance instance = new bLuaInstance(new bLuaSettings()
        {
            features = bLuaSettings.SANDBOX_ALL,
            internalVerbosity = bLuaSettings.InternalErrorVerbosity.Verbose
        });

        Debug.Log("Starting Test Coroutine");

        instance.ExecBuffer("test_coroutine",
            @"function testYield(x)
                for i=1,x do
                    print('test coroutine ' .. i)
                    coroutine.yield()
                end
                print('Finished Test Coroutine')
            end");

        using (bLuaValue fn = instance.GetGlobal("testYield"))
        {
            instance.CallAsCoroutine(fn, 5);
        }
    }

    public void RunThreadMacros()
    {
        bLuaInstance instance = new bLuaInstance(new bLuaSettings()
        {
            features = bLuaSettings.SANDBOX_ALL,
            internalVerbosity = bLuaSettings.InternalErrorVerbosity.Verbose
        });

        Debug.Log("Starting Thread Macros");

        instance.ExecBuffer("test_macros",
            @"function testMacros(x)
                print('I print first')
                delay(2, function()
                    print('I print third')
                end)
                spawn(function()
                    printStringAfter('I print second', 1)
                end)
                wait(x)
                print('I print last')
                print('Finished Thread Macros')
            end

            function printStringAfter(s, t)
                wait(t)
                print(s)
            end");

        using (bLuaValue fn = instance.GetGlobal("testMacros"))
        {
            instance.CallAsCoroutine(fn, 3);
        }
    }

    public static int TestCFunction(IntPtr state)
    {
        bLua.NativeLua.Lua.PushOntoStack(bLuaInstance.GetInstanceByState(bLua.NativeLua.Lua.GetMainThread(state)), 5);
        return 1;
    }
}
