using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace bLua.ExampleUserData
{
    [bLuaUserData]
    public static class bLuaGameObjectExtensionLibrary
    {
        public static bLuaGameObject Duplicate(this bLuaGameObject _gameObject)
        {
            if (_gameObject != null)
            {
                GameObject duplicatedGameObject = GameObject.Instantiate(_gameObject.__gameObject);
                return new bLuaGameObject(duplicatedGameObject);
            }
            return null;
        }

        public static void Destroy(this bLuaGameObject _gameObject)
        {
            if (_gameObject != null)
            {
                MonoBehaviour.Destroy(_gameObject.__gameObject);
            }
        }
    }
} // bLua.ExampleUserData namespace
