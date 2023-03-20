using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua.ExampleUserData
{
    [bLuaUserData]
    public class bLuaGameObjectLibrary
    {
        public bLuaGameObject New()
        {
            GameObject gameObject = new GameObject();
            return new bLuaGameObject(gameObject);
        }
    }
} // bLua.ExampleUserData namespace
