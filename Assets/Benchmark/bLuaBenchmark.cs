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
        return bLuaNative.script;
    }

    protected override object RegisterUserData(object script)
    {
        bLuaNative.Script bLuaScript = script as bLuaNative.Script;
        if (bLuaScript != null)
        {
            bLuaUserData.Register(typeof(BenchmarkUserData));
            bLuaNative.SetGlobal("UserData", bLuaValue.CreateUserData(new BenchmarkUserData()));
        }
        return bLuaScript;
    }

    protected override void RunBenchmark(object script, string lua)
    {
        bLuaNative.Script bLuaScript = script as bLuaNative.Script;
        if (bLuaScript != null)
        {
            bLuaScript.ExecBuffer("benchmark", lua);
        }
    }
}
