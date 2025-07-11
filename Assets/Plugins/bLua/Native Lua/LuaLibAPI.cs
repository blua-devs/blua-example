using System.Runtime.InteropServices;

namespace bLua.NativeLua
{
    /// <summary> Container for all lua_* API calls to Lua </summary>
    public static class LuaLibAPI
    {
        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_setfield(System.IntPtr L, int index, System.IntPtr k);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getfield(System.IntPtr L, int index, System.IntPtr k);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_setglobal(System.IntPtr L, string name);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getglobal(System.IntPtr L, string name);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_seti(System.IntPtr L, int index, int i);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_geti(System.IntPtr L, int index, int i);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_setiuservalue(System.IntPtr L, int index, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getiuservalue(System.IntPtr L, int index, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_setmetatable(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_getmetatable(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_settable(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_gettable(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_settop(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_gettop(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_setupvalue(System.IntPtr L, int funcindex, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_getupvalue(System.IntPtr L, int funcindex, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_checkstack(System.IntPtr L, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_close(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_createtable(System.IntPtr L, int narr, int nrec);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_isyieldable(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_newthread(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_newuserdatauv(System.IntPtr L, System.IntPtr size, int nuvalue);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_next(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_pcallk(System.IntPtr L, int nargs, int nresults, int msgh, long ctx, System.IntPtr k);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushboolean(System.IntPtr L, int b);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushcclosure(System.IntPtr L, System.IntPtr fn, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushinteger(System.IntPtr L, int n);

        [DllImport(Lua.LUA_DLL, CharSet = CharSet.Ansi)]
        public static extern void lua_pushlstring(System.IntPtr L, System.IntPtr s, ulong len);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushnil(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushnumber(System.IntPtr L, double n);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_pushvalue(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_rawequal(System.IntPtr L, int index1, int index2);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_rawgeti(System.IntPtr L, int index, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern uint lua_rawlen(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_resume(System.IntPtr L, System.IntPtr from, int nargs, out int nresults);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_status(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_toboolean(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_tointegerx(System.IntPtr L, int index, System.IntPtr isnum);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_tolstring(System.IntPtr L, int index, StrLen len);

        [DllImport(Lua.LUA_DLL)]
        public static extern double lua_tonumberx(System.IntPtr L, int index, System.IntPtr isnum);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_topointer(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern System.IntPtr lua_tothread(System.IntPtr L, int index);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_type(System.IntPtr L, int tp);

        [DllImport(Lua.LUA_DLL)]
        public static extern void lua_xmove(System.IntPtr from, System.IntPtr to, int n);

        [DllImport(Lua.LUA_DLL)]
        public static extern int lua_yieldk(System.IntPtr L, int nresults, System.IntPtr ctx, System.IntPtr k);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_base(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_coroutine(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_debug(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_io(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_math(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_os(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_package(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_string(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_table(System.IntPtr L);

        [DllImport(Lua.LUA_DLL)]
        public static extern void luaopen_utf8(System.IntPtr L);
    }
} // bLua.NativeLua namespace
