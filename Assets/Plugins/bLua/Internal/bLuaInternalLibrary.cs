using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace bLua.Internal
{
    [bLuaUserData]
    public class bLuaInternalLibrary
    {
        /// <summary> Returns a Lua-accessible version of Unity's Time.time. Also works when not in play mode. </summary>
        public static float time
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
    }
} // bLua.Internal namespace
