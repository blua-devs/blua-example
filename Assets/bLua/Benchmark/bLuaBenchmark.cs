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

public class bLuaBenchmark : Benchmark
{


    protected override void Init()
    {
        identifier = "bLua";
    }


    public override void RunAllBenchmarks()
    {
        bLuaNative.Script script = bLuaNative.script;

        script.ExecBuffer("benchmarks", lua);

        RunBenchmark("NoOperation", () => { });

        using (bLuaValue fn = bLuaNative.GetGlobal("myfunction"))
        {
            RunBenchmark("Call", () => script.Call(fn, 8));
        }

        using (bLuaValue fn = bLuaNative.GetGlobal("lotsofargs"))
        {
            RunBenchmark("CallManyArgs", () => script.Call(fn, 8, 2, 3, 4, 5, 6, 2));
        }

        RunBenchmark("GetGlobal", () => bLuaNative.GetGlobal("MyFunctions"));
        RunBenchmark("FullLookup", () => bLuaNative.FullLookup(bLuaNative.GetGlobal("MyFunctions"), "blah"));

        using (bLuaValue t = script.Call(bLuaNative.GetGlobal("make_table")))
        {
            RunBenchmark("GetDict", () => bLuaValue.RunDispose(t.Dict()));
        }

        using (bLuaValue t = script.Call(bLuaNative.GetGlobal("make_big_table")))
        {
            RunBenchmark("GetBigDict", () => bLuaValue.RunDispose(t.Dict()));
        }

        using (bLuaValue fn = bLuaNative.GetGlobal("make_big_table"))
        {
            RunBenchmark("MakeBigTable", () => script.Call(fn));
        }
    }
}
