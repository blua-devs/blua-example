using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua.ExampleUserData
{
    [bLuaUserData(reliantUserData = new Type[1] { typeof(Vector3) })]
    public class bLuaVector3Library
    {
        public static Vector3 zero
        {
            get
            {
                return Vector3.zero;
            }
        }


        public static Vector3 New(float _x, float _y, float _z)
        {
            return new Vector3(_x, _y, _z);
        }
    }
} // bLua.ExampleUserData namespace
