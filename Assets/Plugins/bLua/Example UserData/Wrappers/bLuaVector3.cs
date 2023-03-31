using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua.ExampleUserData
{
    [bLuaUserData]
    public struct bLuaVector3
    {
        [bLuaHidden]
        public Vector3 __vector3;

        public float x
        {
            get
            {
                return __vector3.x;
            }
            set
            {
                __vector3.x = value;
            }
        }

        public float y
        {
            get
            {
                return __vector3.y;
            }
            set
            {
                __vector3.y = value;
            }
        }

        public float z
        {
            get
            {
                return __vector3.z;
            }
            set
            {
                __vector3.z = value;
            }
        }


        public bLuaVector3(Vector3 _vector3)
        {
            __vector3 = _vector3;
        }


        public static implicit operator Vector3(bLuaVector3 v)
        {
            return v.__vector3;
        }
        public static implicit operator bLuaVector3(Vector3 v)
        {
            return new bLuaVector3(v);
        }
    }
}
