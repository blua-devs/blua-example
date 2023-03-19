using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using XLua;

#if UNITY_EDITOR
[CustomEditor(typeof(XLuaBenchmark))]
public class XLuaBenchmarkEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Run Benchmark"))
        {
            XLuaBenchmark benchmark = GameObject.Find("Benchmark")?.GetComponent<XLuaBenchmark>();
            benchmark.RunAllBenchmarks();
        }
    }
}
#endif // UNITY_EDITOR

public class XLuaBenchmarkUserData : BenchmarkUserData
{


    // Overwritten to return the `xLuaBenchmarkUserData` type
    new public static XLuaBenchmarkUserData CreateUserDataStatic(int a)
    {
        return new XLuaBenchmarkUserData()
        {
            testField = 30 + a,
            testProperty = 40 + a
        };
    }

    // Overwritten to cast from `object` to `int` the way xLua expects (object -> Int64 -> int)
    public override int AddValuesVariableArgs(int a, params object[] b)
    {
        int c = a;
        for (int i = 0; i < b.Length; i++)
        {
            c += (int)(System.Int64)b[i];
        }
        return c;
    }
}

public class XLuaBenchmark : Benchmark
{
    LuaTable scriptEnv;

    // Changed `UserData.CreateUserDataStatic(1)` to `CreateUserDataStatic(1)`
    // Changed `UserData.AddValuesStatic(4, 5)` to `AddValuesStatic(4, 5)`
    protected override string script_ArgsToUserData =>
        @"local ud = CreateUserDataStatic(1)
        local a = ud:AddValues(2, 3)
        local b = AddValuesStatic(4, 5)
        local c = ud:AddValuesVariableArgs(6, 7, 8, 9)
        local d = ud:AddValuesLotsOfArgs(10, 11, 12, 13, 14, 15, 16, 17)";

    // Changed `UserData.CreateUserDataStatic(0)` to `CreateUserDataStatic(0)`
    protected override string script_AffectGameObject =>
        @"local ud = CreateUserDataStatic(0)
        ud:CreateGameObject()
        for i=1,100 do
            ud:TransformGameObject(i)
        end
        ud:DestroyGameObject()";


    protected override void Init()
    {
        identifier = "XLua";
    }


    protected override object GetScript()
    {
        return new LuaEnv();
    }

    protected override object RegisterUserData(object script)
    {
        LuaEnv luaEnv = script as LuaEnv;
        if (luaEnv != null)
        {
            scriptEnv = luaEnv.NewTable();
            scriptEnv.Set("UserData", new XLuaBenchmarkUserData());

            // Manually create some of the calls used in the benchmark that XLua has trouble accessing
            scriptEnv.Set<string, Func<int, string>>("tostring", (i) => { return i.ToString(); });
            scriptEnv.Set<string, Func<int, XLuaBenchmarkUserData>>("CreateUserDataStatic", (i) => { return XLuaBenchmarkUserData.CreateUserDataStatic(i); });
            scriptEnv.Set<string, Func<int, int, int>>("AddValuesStatic", (a, b) => { return XLuaBenchmarkUserData.AddValuesStatic(a, b); });
        }
        return luaEnv;
    }

    protected override void RunBenchmark(object script, string lua)
    {
        LuaEnv luaEnv = script as LuaEnv;
        if (luaEnv != null)
        {
            luaEnv.DoString(lua, "chunk", scriptEnv);
        }
    }
}
