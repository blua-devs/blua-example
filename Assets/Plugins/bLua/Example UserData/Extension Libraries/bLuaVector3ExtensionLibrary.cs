using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua.ExampleUserData
{
    [bLuaUserData(reliantUserData = new Type[1] { typeof(Vector3) } )]
    public static class bLuaVector3ExtensionLibrary
    {
        public static Vector3 Normalize(this Vector3 _vector3)
        {
            return _vector3.normalized;
        }
    }
} // bLua.ExampleUserData namespace
