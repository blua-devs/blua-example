using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Diagnostics;
using UnityEditor;
using UnityEngine.Events;
using System;

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

/// <summary> Testing user data for benchmarking purposes. Make an additional partial class for BenchmarkUserData that
/// has any attributes you need to register your custom user data. </summary>
public partial class BenchmarkUserData
{
    public int testField = 10;

    public int testProperty
    {
        get
        {
            return testField + 2;
        }
        set
        {
            testField = testField - 3;
        }
    }

    public static int testFieldStatic = 20;

    public static int testPropertyStatic
    {
        get
        {
            return testFieldStatic + 4;
        }
        set
        {
            testFieldStatic = testFieldStatic - 6;
        }
    }

    public static BenchmarkUserData CreateUserDataStatic(int a)
    {
        return new BenchmarkUserData()
        {
            testField = 30 + a,
            testProperty = 40 + a
        };
    }

    public int AddValues(int a, int b)
    {
        return a + b + testField + testProperty;
    }

    public static int AddValuesStatic(int a, int b)
    {
        return a + b + testFieldStatic + testPropertyStatic;
    }

    public int AddValuesLotsOfArgs(int a, int b, int c, int d, int e, int f, int g, int h)
    {
        return a + b + c + d + e + f + g + h;
    }

    public int AddValuesVariableArgs(int a, params object[] b)
    {
        int c = a;
        for (int i = 0; i < b.Length; i++)
        {
            c += (int)(double)b[i];
        }
        return c;
    }

    GameObject gameObject;

    public void CreateGameObject()
    {
        gameObject = new GameObject();
    }

    public void DestroyGameObject()
    {
        if (gameObject != null)
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isPlaying)
            {
                MonoBehaviour.Destroy(gameObject);
            }
            else
            {
                MonoBehaviour.DestroyImmediate(gameObject);
            }
#else
            MonoBehaviour.Destroy(gameObject);
#endif
        }
    }

    public void TransformGameObject(int a)
    {
        gameObject.transform.rotation *= Quaternion.Euler(1 + a, 2 + a, 3 + a);
        gameObject.transform.position += new Vector3(4f + a, 5f + a, 6f + a);
    }
}

public class Benchmark : MonoBehaviour
{
    /// <summary> This event is called whenever the benchmarks have been run. </summary>
    [HideInInspector] public UnityEvent<Benchmark, BenchmarkResult[]> OnBenchmarksRan = new UnityEvent<Benchmark, BenchmarkResult[]>();

    /// <summary> The identifier for the benchmark. This will be printed with the benchmark resutls so you can keep track of which
    /// benchmark ran the test. </summary>
    [HideInInspector] public string identifier;

    /// <summary> All of the scripts to test for benchmarking. </summary>
    protected readonly Tuple<string /* Test name */, string /* Lua */>[] benchmarkScripts =
    {
        new Tuple<string, string>(
            "NoOperation",
@""),
        new Tuple<string, string>(
            "BasicAddition",
@"function add(x)
    return x + 5
end

local val
for i=1,1000 do
    val = add(i)
end"),
        new Tuple<string, string>(
            "LotsOfArgsAddition",
@"function add(a, b, c, d, e, f, g, h)
    return a + b + c + d + e + f + g + h + 5
end

local val
for i=1,1000 do
    val = add(i, 2, 3, 4, 5, 6, 7, 8)
end"),
        new Tuple<string, string>(
            "MakeBigTable",
@"function make_big_table()
    local result = {}
    for i=1,1000 do
        result[tostring(i)] = i
    end
    return result
end

make_big_table()"),
        new Tuple<string, string>(
            "ArgsToUserData",
@"local ud = UserData.CreateUserDataStatic(1)
local a = ud:AddValues(2, 3)
local b = UserData.AddValuesStatic(4, 5)
local c = ud:AddValuesVariableArgs(6, 7, 8, 9)
local d = ud:AddValuesLotsOfArgs(10, 11, 12, 13, 14, 15, 16, 17)"),
        new Tuple<string, string>(
            "AffectGameObject",
@"local ud = UserData.CreateUserDataStatic(0)
ud:CreateGameObject()
for i=1,100 do
    ud:TransformGameObject(i)
end
ud:DestroyGameObject()")
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
            results[i] = TimeBenchmark(benchmarkScripts[i].Item1,
                () => {
                    object script = GetScript();
                    script = RegisterUserData(script);
                    RunBenchmark(script, benchmarkScripts[i].Item2);
                });
        }

        // Print the results to the console/log
        for (int i = 0; i < results.Length; i++)
        {
            Debug.Log(FormatResult(results[i]));
        }

        OnBenchmarksRan.Invoke(this, results);
    }

    /// <summary> This needs to be overridden to return a script class for your interpreter. This is used later so you can initialize things 
    /// like the custom benchmark user data before running Lua code that requires a user data. </summary>
    /// <returns>The Lua-capable script object.</returns>
    protected virtual object GetScript()
    {
        throw new System.NotImplementedException();
    }

    /// <summary> This needs to be overridden to register BenchmarkUserData to the passed script object so 
    /// that the benchmark Lua code can run tests on it. </summary>
    /// <param name="script">The script object to register your custom user data to.</param>
    protected virtual object RegisterUserData(object script)
    {
        throw new System.NotImplementedException();
    }

    /// <summary> This needs to be overridden to simply run the Lua code that is passed into the function using whatever Lua interpreter 
    /// is being benchmarked. Use the script object passed so that your custom user data registered will exist on it. </summary>
    /// <param name="script">The script object to run the Lua on.</param>
    /// <param name="lua">The Lua code to be tested.</param>
    protected virtual void RunBenchmark(object script, string lua)
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
