using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

#if UNITY_EDITOR
[CustomEditor(typeof(BenchmarkPlayHelper))]
public class BenchmarkPlayHelperEditor : Editor
{
    public override void OnInspectorGUI()
    {
        GUILayout.Label("This component helps you run the benchmark");
        GUILayout.Label("tests while in play mode or in builds.");
    }
}
#endif // UNITY_EDITOR

public class BenchmarkPlayHelper : MonoBehaviour
{
    Benchmark[] benchmarks;
    Dictionary<Benchmark, BenchmarkResult[]> resultsByBenchmark = new();

    const float panelWidth = 600f;
    const float panelHeightMin = 30f;
    const float panelPadding = 10f;
    const float buttonWidth = panelWidth / 2f;
    const float buttonHeight = 20f;
    const float labelWidth = panelWidth;
    const float labelHeight = 20f;
    const float padding = 5f;


    private void Start()
    {
        benchmarks = FindObjectsByType<Benchmark>(FindObjectsSortMode.None);

        foreach (Benchmark benchmark in benchmarks)
        {
            benchmark.OnBenchmarksRan.AddListener(OnBenchmarksRan);
        }
    }

    private void OnGUI()
    {
        GUI.Box(
            new Rect(panelPadding, panelPadding, panelWidth, panelHeightMin + (benchmarks.Length * (buttonHeight + padding)) + (resultsByBenchmark.Keys.Select(k => resultsByBenchmark[k].Length).Sum() * 25f) + ((resultsByBenchmark.Count * (padding * 2f)) + (padding * 2f))),
            "Benchmark Play Helper");

        if (benchmarks.Length == 0)
        {
            GUI.Label(
                new Rect(panelPadding * 2f, panelHeightMin + padding, panelWidth - (panelPadding * 4f), panelPadding * 2f),
                "No benchmarks found!");
        }
        else
        {
            for (int i = 0; i < benchmarks.Length; i++)
            {
                if (GUI.Button(
                    new Rect((panelWidth - buttonWidth) / 2f, (panelHeightMin + padding) + (i * (buttonHeight + padding)), buttonWidth, buttonHeight),
                    benchmarks[i].identifier + " Benchmark"))
                {
                    benchmarks[i].RunAllBenchmarks();
                }
            }

            int rowsDrawn = 0;
            for (int i = 0; i < resultsByBenchmark.Count; i++)
            {
                KeyValuePair<Benchmark, BenchmarkResult[]> kvp = resultsByBenchmark.ElementAt(i);
                foreach (BenchmarkResult benchmarkResult in kvp.Value)
                {
                    GUI.Label(
                        new Rect(panelPadding + padding, (panelHeightMin + padding) + (benchmarks.Length * (buttonHeight + padding)) + (rowsDrawn * (labelHeight + padding)) + ((i * (padding * 2f)) + (padding * 2f)), labelWidth - ((panelPadding + padding) * 2f), labelHeight),
                        Benchmark.FormatResult(benchmarkResult));
                    rowsDrawn++;
                }
            }
        }
    }


    private void OnBenchmarksRan(Benchmark benchmark, BenchmarkResult[] results)
    {
        resultsByBenchmark[benchmark] = results;
    }
}
