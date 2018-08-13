using System;
namespace MicroLua {
    public partial class LuaState {
        /////////////////////////
        //*** GC OPERATIONS ***//
        /////////////////////////

        public void GCStop() {
            Lua.lua_gc(Pointer, LuaGcOperation.Stop, 0);
        }

        public void GCRestart() {
            Lua.lua_gc(Pointer, LuaGcOperation.Restart, 0);
        }

        public void GCCollect() {
            Lua.lua_gc(Pointer, LuaGcOperation.Collect, 0);
        }

        public int GCCountKilobytes() {
            return Lua.lua_gc(Pointer, LuaGcOperation.Count, 0);
        }

        public int GCCountBytes() {
            return GCCountKilobytes() * 1024 + Lua.lua_gc(Pointer, LuaGcOperation.Countb, 0);
        }

        public bool GCStep(int steps = 1) {
            return Lua.lua_gc(Pointer, LuaGcOperation.Step, steps) == 1;
        }

        public int GCSetPause(int pause) {
            return Lua.lua_gc(Pointer, LuaGcOperation.SetPause, pause);
        }

        public int GCSetStepMultiplier(int mul) {
            return Lua.lua_gc(Pointer, LuaGcOperation.SetStepMul, mul);
        }
    }
}
