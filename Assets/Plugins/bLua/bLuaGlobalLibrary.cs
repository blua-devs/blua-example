using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using bLua;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary> A library of commonly used Lua functions. </summary>
[bLuaUserData]
public class bLuaGlobalLibrary
{
    /// <summary> Returns a Lua-accessible version of Unity's Time.time. Also works when not in play mode. </summary>
    static public float time
    {
        get
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                return (float)EditorApplication.timeSinceStartup - Time.time;
            }

            return Time.time;
#else
            return Time.time;
#endif
        }
    }

    /// <summary> Prints a string to Unity's logs. </summary>
    static public void print(string _string)
    {
        Debug.Log(_string);
    }
}
