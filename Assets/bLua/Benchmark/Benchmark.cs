using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Diagnostics;

public class Benchmark : MonoBehaviour
{
    /// <summary> The identifier for the benchmark. This will be printed with the benchmark resutls so you can keep track of which
    /// benchmark ran the test. </summary>
    protected string identifier;

    /// <summary> The benchmark Lua, stored here since all benchmarks runs the same Lua code. </summary>
    protected string lua = @"
MyFunctions = {
}

function MyFunctions.blah(y)
    return y
end

function myfunction(x)
    return x + 5
end

function lotsofargs(a,b,c,d,e,f,g)
    return a+b+c+d+e+f+g
end

function make_table()
    return {
        xyz = 5,
        abc = 9,
        def = 7,
    }
end

function make_big_table()
    local result = {}
    for i=1,1000 do
        result[tostring(i)] = i
    end
    return result
end";


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


    /// <summary> Runs all of the benchmark tests. This needs to be overridden with code that runs the benchmark lua with Lua interpreter is 
    /// being used for the benchmark. </summary>
    public virtual void RunAllBenchmarks()
    {
        throw new System.NotImplementedException();
    }

    /// <summary> Runs a benchmark test and prints the results to the console. </summary>
    /// <param name="name"> The name for the benchmark test. This will be printed with the benchmark results. </param>
    /// <param name="action"> An action containing the test you want to run. </param>
    protected void RunBenchmark(string name, System.Action action)
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

        if (currentIterations >= 1000000)
        {
            double ns = elapsed * 1000000.0;
            ns /= currentIterations;
            Debug.Log($"Benchmark: ({identifier}) {name} did {currentIterations} in {elapsed}ms; {string.Format("{0:n0}", (int)ns)}ns(nanosecond)/iteration");
        }
        else if (currentIterations >= 1000)
        {
            double us = elapsed * 1000.0;
            us /= currentIterations;
            Debug.Log($"Benchmark: ({identifier}) {name} did {currentIterations} in {elapsed}ms; {string.Format("{0:n0}", (int)us)}us(microsecond)/iteration");
        }
        else
        {
            double ms = elapsed;
            ms /= currentIterations;
            Debug.Log($"Benchmark: ({identifier}) {name} did {currentIterations} in {elapsed}ms; {string.Format("{0:n0}", (int)ms)}ms(millisecond)/iteration");
        }
    }
}
