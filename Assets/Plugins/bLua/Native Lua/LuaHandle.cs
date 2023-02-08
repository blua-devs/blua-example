using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using bLua;
using bLua.NativeLua;

public sealed class LuaHandle : IDisposable
{
    private static Dictionary<IntPtr, LuaHandle> handleRegistry = new Dictionary<IntPtr, LuaHandle>();

    public static LuaHandle GetHandleFromRegistry(IntPtr _state)
    {
        LuaHandle handle = null;
        handleRegistry.TryGetValue(_state, out handle);
        return handle;
    }

    public bLuaInstance instance;

    private IntPtr m_handle = IntPtr.Zero;

    public IntPtr state => m_handle;


    public LuaHandle(bLuaInstance _instance)
    {
        instance = _instance;
        m_handle = LuaXLibAPI.luaL_newstate();
        handleRegistry.Add(m_handle, this);
    }
    
    public void SetState(IntPtr _state)
    {
        m_handle = _state;
    }

    public void Dispose()
    {
        handleRegistry.Remove(m_handle);

        if (m_handle != IntPtr.Zero)
        {
            LuaLibAPI.lua_close(m_handle); // Closes the current Lua environment and releases all objects, threads, and dynamic memory
        }

        m_handle = IntPtr.Zero;
    }
}
