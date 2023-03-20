using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using bLua;

namespace bLua.ExampleUserData
{
    [bLuaUserData]
    public class bLuaGameObject
    {
        [bLuaHidden]
        public GameObject __gameObject;

        public string name
        {
            get
            {
                if (__gameObject != null)
                {
                    return __gameObject.name;
                }
                return string.Empty;
            }
            set
            {
                if (__gameObject != null)
                {
                    __gameObject.name = value;
                }
            }
        }

        public Vector3 position
        {
            get
            {
                if (__gameObject != null
                    && __gameObject.transform != null)
                {
                    return __gameObject.transform.position;
                }
                return Vector3.zero;
            }
            set
            {
                if (__gameObject != null
                    && __gameObject.transform != null)
                {
                    __gameObject.transform.position = value;
                }
            }
        }


        public bLuaGameObject(GameObject _gameObject)
        {
            __gameObject = _gameObject;
        }
    }
} // bLua.ExampleUserData namespace
