using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Diagnostics;
using UnityEditor;
using UnityEngine.Events;

/// <summary> A collection of data returned from running a benchmark test. </summary>
public struct BenchmarkResult
{
    /// <summary> The name of the benchmark test that was run. </summary>
    public string testName;
    /// <summary> The name/identifier for the interpreter used. For bLua this will be "bLua". </summary>
    public string interpreter;
    /// <summary> The total number of iterations run during the test. </summary>
    public long iterations;
    /// <summary> The total duration of the test. </summary>
    public double timeElapsed;
}

public class Benchmark : MonoBehaviour
{
    /// <summary> This event is called whenever the benchmarks have been run. </summary>
    [HideInInspector] public UnityEvent<Benchmark, BenchmarkResult[]> OnBenchmarksRan = new UnityEvent<Benchmark, BenchmarkResult[]>();

    /// <summary> The identifier for the benchmark. This will be printed with the benchmark resutls so you can keep track of which
    /// benchmark ran the test. </summary>
    [HideInInspector] public string identifier;

    /// <summary> All of the scripts to test for benchmarking. </summary>
    protected readonly KeyValuePair<string, string>[] benchmarkScripts =
    {
        new KeyValuePair<string, string>(
            "NoOperation",
@""),
        new KeyValuePair<string, string>(
            "BasicAddition",
@"function add(x)
    return x + 5
end

local val
for i=1,1000 do
    val = add(i)
end"),
        new KeyValuePair<string, string>(
            "LotsOfArgsAddition",
@"function add(a, b, c, d, e, f, g, h)
    return a + b + c + d + e + f + g + h + 5
end

local val
for i=1,1000 do
    val = add(i, 2, 3, 4, 5, 6, 7, 8)
end"),
        new KeyValuePair<string, string>(
            "MakeBigTable",
@"function make_big_table()
    local result = {}
    for i=1,1000 do
        result[tostring(i)] = i
    end
    return result
end

make_big_table()")
    };


    void Awake()
    {
        Init();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        Init();
    }
#endif

    /// <summary> Initialize any values that need to be set from their defaults. </summary>
    protected virtual void Init()
    {
        identifier = "none";
    }


    /// <summary> Runs all of the benchmark tests. </summary>
    public void RunAllBenchmarks()
    {
        // Gather the results from all of the benchmark tests
        BenchmarkResult[] results = new BenchmarkResult[benchmarkScripts.Length];
        for (int i = 0; i < benchmarkScripts.Length; i++)
        {
            results[i] = TimeBenchmark(benchmarkScripts[i].Key, () => RunBenchmark(benchmarkScripts[i].Value));
        }

        // Print the results to the console/log
        for (int i = 0; i < results.Length; i++)
        {
            Debug.Log(FormatResult(results[i]));
        }

        OnBenchmarksRan.Invoke(this, results);
    }

    /// <summary> This needs to be overridden to simply run the Lua code that is passed into the function using whatever Lua interpreter 
    /// is being benchmarked </summary>
    /// <param name="lua">The Lua code to be tested.</param>
    protected virtual void RunBenchmark(string lua)
    {
        throw new System.NotImplementedException();
    }

    /// <summary> Runs a benchmark test and prints the results to the console. </summary>
    /// <param name="name"> The name for the benchmark test. This will be printed with the benchmark results. </param>
    /// <param name="action"> An action containing the test you want to run. </param>
    protected BenchmarkResult TimeBenchmark(string name, System.Action action)
    {
        action();

        long currentIterations = 1;
        long elapsed = 0;

        while (elapsed < 10)
        {
            currentIterations *= 10;
            Stopwatch iterationTimer = Stopwatch.StartNew();

            for (long i = 0; i < currentIterations; ++i)
            {
                action();
            }

            iterationTimer.Stop();
            elapsed = iterationTimer.ElapsedMilliseconds;
        }

        BenchmarkResult result = new BenchmarkResult();
        result.testName = name;
        result.interpreter = identifier;
        result.iterations = currentIterations;
        result.timeElapsed = elapsed;

        return result;
    }

    public static string FormatResult(BenchmarkResult result)
    {
        if (result.iterations >= 1000000)
        {
            double ns = result.timeElapsed * 1000000.0;
            ns /= result.iterations;
            return $"Benchmark: ({result.interpreter}) {result.testName} did {result.iterations} in {result.timeElapsed}ms; {string.Format("{0:n0}", (int)ns)}ns(nanosecond)/iteration";
        }
        else if (result.iterations >= 1000)
        {
            double us = result.timeElapsed * 1000.0;
            us /= result.iterations;
            return $"Benchmark: ({result.interpreter}) {result.testName} did {result.iterations} in {result.timeElapsed}ms; {string.Format("{0:n0}", (int)us)}us(microsecond)/iteration";
        }
        else
        {
            double ms = result.timeElapsed;
            ms /= result.iterations;
            return $"Benchmark: ({result.interpreter}) {result.testName} did {result.iterations} in {result.timeElapsed}ms; {string.Format("{0:n0}", (int)ms)}ms(millisecond)/iteration";
        }
    }
}
