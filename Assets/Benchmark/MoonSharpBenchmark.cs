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

public class MoonSharpBenchmark : Benchmark
{


    protected override void Init()
    {
        identifier = "MoonSharp";
    }


    protected override void RunBenchmark(string lua)
    {
        Script.RunString(lua);
    }
}
