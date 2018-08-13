using System;
namespace MicroLua {
    public partial class LuaState {
        ////////////////////////////
        //*** TABLE OPERATIONS ***//
        ////////////////////////////
        public void PushNewTable() {
            Lua.lua_newtable(Pointer);
        }

        public void GetField(string name, int index = -1) {
            Lua.lua_getfield(Pointer, index, name);
        }

        public void GetGlobal(string name) {
            Lua.lua_getglobal(Pointer, name);
        }

        public void SetField(string name, int index = -2) {
            Lua.lua_setfield(Pointer, index, name);
        }

        public void SetGlobal(string name) {
            Lua.lua_setglobal(Pointer, name);
        }

        public void StartIter() {
            Lua.lua_pushnil(Pointer);
        }

        public bool Iter(int index = -1) {
            return Lua.lua_next(Pointer, index) != 0;
        }

        public void NextIter() {
            Lua.lua_pop(Pointer, 1);
        }
    }
}
