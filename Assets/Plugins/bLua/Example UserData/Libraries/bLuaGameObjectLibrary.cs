using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua.ExampleUserData
{
    [bLuaUserData]
    public class bLuaGameObjectLibrary
    {
        public static bLuaGameObject New()
        {
            GameObject gameObject = new GameObject();
            return new bLuaGameObject(gameObject);
        }

        public static bLuaGameObject Find(string _name)
        {
            GameObject gameObject = GameObject.Find(_name);
            if (gameObject != null)
            {
                return new bLuaGameObject(gameObject);
            }
            return null;
        }
    }
} // bLua.ExampleUserData namespace
