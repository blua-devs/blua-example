using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using MoonSharp;
using MoonSharp.Interpreter;

#if UNITY_EDITOR
[CustomEditor(typeof(MoonSharpBenchmark))]
public class MoonSharpBenchmarkEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Run Benchmark"))
        {
            MoonSharpBenchmark benchmark = GameObject.Find("Benchmark")?.GetComponent<MoonSharpBenchmark>();
            benchmark.RunAllBenchmarks();
        }
    }
}
#endif // UNITY_EDITOR

[MoonSharpUserData]
public partial class BenchmarkUserData
{

}

public class MoonSharpBenchmark : Benchmark
{


    protected override void Init()
    {
        identifier = "MoonSharp";
    }


    protected override object GetScript()
    {
        return new Script();
    }

    protected override object RegisterUserData(object script)
    {
        Script moonSharpScript = script as Script;
        if (moonSharpScript != null)
        {
            UserData.RegisterType(typeof(BenchmarkUserData));
            moonSharpScript.Globals.Set("UserData", UserData.Create(new BenchmarkUserData()));
        }
        return moonSharpScript;
    }

    protected override void RunBenchmark(object script, string lua)
    {
        Script moonSharpScript = script as Script;
        if (moonSharpScript != null)
        {
            moonSharpScript.DoString(lua);
        }
    }
}
