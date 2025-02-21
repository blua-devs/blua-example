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
            benchmark?.RunAllBenchmarks();
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
    private List<bLuaInstance> instances = new();


    protected override void Init()
    {
        identifier = "bLua";
    }


    protected override object GetScript()
    {
        bLuaInstance instance = new bLuaInstance(new bLuaSettings()
        {
            features = bLuaSettings.SANDBOX_SAFE,
            autoRegisterTypes = bLuaSettings.AutoRegisterTypes.None,
            internalVerbosity = bLuaSettings.InternalErrorVerbosity.Minimal
        });
        instances.Add(instance);

        return instance;
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
        if (script is bLuaInstance instance)
        {
            instance.DoString(lua);
        }
    }

    protected override void Cleanup()
    {
        foreach (bLuaInstance instance in instances.ToArray())
        {
            instance.Dispose();
        }
    }
}
