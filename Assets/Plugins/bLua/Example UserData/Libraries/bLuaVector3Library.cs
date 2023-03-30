using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua.ExampleUserData
{
    [bLuaUserData]
    public class bLuaVector3Library
    {
        public static Vector3 New(float _x, float _y, float _z)
        {
            return new Vector3(_x, _y, _z);
        }

        public static Vector3 Zero()
        {
            return new Vector3();
        }

        public static Vector3 Normalize(Vector3 _vector3)
        {
            return _vector3.normalized;
        }
    }
} // bLua.ExampleUserData namespace
