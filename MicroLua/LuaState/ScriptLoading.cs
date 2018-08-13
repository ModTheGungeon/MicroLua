using System;
namespace MicroLua{
    public partial class LuaState {
        //////////////////////////
        //*** SCRIPT LOADING ***//
        //////////////////////////
        /// 
        public LuaResult LoadFile(string path) {
            return Lua.luaL_loadfile(Pointer, path);
        }

        public LuaResult LoadString(string str) {
            return Lua.luaL_loadstring(Pointer, str);
        }

        public LuaResult LoadBuffer(string chunk_name, string str) {
            return Lua.luaL_loadbuffer(Pointer, str, (UIntPtr)str.Length, chunk_name);
        }

        public void DoString(string str) {
            Lua.lua_dostring(Pointer, str);
        }
    }
}
