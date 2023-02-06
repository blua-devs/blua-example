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

        bLua.bLua.Init();
    }


    protected override object GetScript()
    {
        return null;
    }

    protected override object RegisterUserData(object script)
    {
        bLuaUserData.Register(typeof(BenchmarkUserData));
        bLua.bLua.SetGlobal("UserData", bLuaValue.CreateUserData(new BenchmarkUserData()));
        return script;
    }

    protected override void RunBenchmark(object script, string lua)
    {
        bLua.bLua.ExecBuffer("benchmark", lua);
    }
}
