using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using bLua;

#if UNITY_EDITOR
[CustomEditor(typeof(bLuaBenchmark))]
public class bLuaBenchmarkEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Run Benchmark"))
        {
            bLuaBenchmark benchmark = GameObject.Find("Benchmark")?.GetComponent<bLuaBenchmark>();
            benchmark.RunAllBenchmarks();
        }
    }
}
#endif // UNITY_EDITOR

[bLuaUserData]
public partial class BenchmarkUserData
{

}

public class bLuaBenchmark : Benchmark
{


    protected override void Init()
    {
        identifier = "bLua";
    }


    protected override object GetScript()
    {
        return new bLuaInstance(new bLuaSettings()
        {
            sandbox = Sandbox.AllFeatures,
            autoRegisterAllUserData = false
        });
    }

    protected override object RegisterUserData(object script)
    {
        bLuaInstance instance = script as bLuaInstance;
        if (instance != null)
        {
            bLuaUserData.Register(instance, typeof(BenchmarkUserData));
            instance.SetGlobal("UserData", bLuaValue.CreateUserData(instance, new BenchmarkUserData()));
        }
        return instance;
    }

    protected override void RunBenchmark(object script, string lua)
    {
        bLuaInstance instance = script as bLuaInstance;
        if (instance != null)
        {
            instance.DoString(lua);
        }
    }
}
