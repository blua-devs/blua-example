using System.Collections;
using System.Collections.Generic;
using UnityEngine.Assertions;
using UnityEngine;
using bLua;
#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
[CustomEditor(typeof(bLuaUnitTests))]
public class bLuaUnitTestsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Run Unit Tests"))
        {
            bLuaUnitTests unitTests = (bLuaUnitTests)target;
            if (unitTests)
            {
                unitTests.RunUnitTests();
            }
        }
        if (GUILayout.Button("Run Test Coroutines"))
        {
            bLuaUnitTests unitTests = (bLuaUnitTests)target;
            if (unitTests)
            {
                unitTests.RunTestCoroutines();
            }
        }
    }
}
#endif // UNITY_EDITOR

public class bLuaUnitTests : MonoBehaviour
{


    public void RunUnitTests()
    {
        bLuaNative.Init();

        int stackSize = bLua.NativeLua.LuaLibAPI.lua_gettop(bLuaNative._state);

        bLuaNative.ExecBuffer("main", @"
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

        using (bLuaValue fn = bLuaNative.GetGlobal("myfunction"))
        {
            var result = bLuaNative.Call(fn, 8);
            Assert.AreEqual(result.Number, 13.0);
        }

        using (bLuaValue fn = bLuaNative.FullLookup(bLuaNative.GetGlobal("MyFunctions"), "blah"))
        {
            Assert.AreEqual(bLuaNative.Call(fn, 12).Number, 12.0);

            Assert.AreEqual(bLua.NativeLua.LuaLibAPI.lua_gettop(bLuaNative._state), stackSize);
        }

        using (bLuaValue fn = bLuaNative.GetGlobal("make_table"))
        {
            bLuaValue t = bLuaNative.Call(fn);
            Dictionary<string, bLuaValue> tab = t.Dict();
            Assert.AreEqual(tab.Count, 3);
            Assert.AreEqual(tab["abc"].Number, 9);
        }

        using (bLuaValue fn = bLuaNative.GetGlobal("add_from_table"))
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

        using (bLuaValue fn = bLuaNative.GetGlobal("test_userdata"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClass() { n = 7 });
            Assert.AreEqual(fn.Call(userdata).Number, 40);
            using (bLuaValue fn2 = bLuaNative.GetGlobal("incr_userdata"))
            {
                fn2.Call(userdata);
                Assert.AreEqual(fn.Call(userdata).Number, 42);
            }
        }

        using (bLuaValue fn = bLuaNative.GetGlobal("test_addstrings"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClass() { n = 7 });
            Assert.AreEqual(fn.Call(userdata, "abc:", bLuaValue.CreateString("def")).String, "abc:def");
        }

        using (bLuaValue fn = bLuaNative.GetGlobal("test_varargs"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClass() { n = 7 });
            Assert.AreEqual(fn.Call(userdata).Number, 20);
        }


        using (bLuaValue fn = bLuaNative.GetGlobal("test_field"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClass() { n = 7 });
            Assert.AreEqual(fn.Call(userdata).Number, 9.0);
        }

        using (bLuaValue fn = bLuaNative.GetGlobal("test_field"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClassDerived() { n = 7 });
            Assert.AreEqual(fn.Call(userdata).Number, 9.0);
        }

        using (bLuaValue fn = bLuaNative.GetGlobal("test_classproperty"))
        {
            var userdata = bLuaValue.CreateUserData(new TestUserDataClassDerived() { n = 7 });
            Assert.AreEqual(fn.Call(userdata, 7.0).Number, 7.0);
        }

        Debug.Log("Lua: Ran unit tests");

        Assert.AreEqual(bLua.NativeLua.LuaLibAPI.lua_gettop(bLuaNative._state), stackSize);
    }

    public void RunTestCoroutines()
    {
        bLuaNative.Init();

        if (Feature.Coroutines.Enabled())
        {
            bLuaNative.ExecBuffer("co", @"
function testYield(x)
    for i=1,x do
        blua.print('co: ' .. i)
        coroutine.yield()
    end
end");
            using (bLuaValue fn = bLuaNative.GetGlobal("testYield"))
            {
                bLuaNative.CallCoroutine(fn, 5);
            }

            if (Feature.Wait.Enabled())
            {
                bLuaNative.ExecBuffer("wait", @"
function testWait(x)
    blua.print('waiting ' .. x .. ' seconds')
    wait(x)
    blua.print('done waiting')
end");
                using (bLuaValue fn = bLuaNative.GetGlobal("testWait"))
                {
                    bLuaNative.CallCoroutine(fn, 2);
                }
            }
            else
            {
                Debug.Log("Did not test the wait function because the " + Feature.Wait.ToString() + " feature was not enabled.");
            }
        }
        else
        {
            Debug.Log("Did not test coroutines because the " + Feature.Coroutines.ToString() + " feature was not enabled.");
        }
    }

    public static int TestCFunction(System.IntPtr state)
    {
        bLua.NativeLua.Lua.PushObjectOntoStack(5);
        return 1;
    }
}
