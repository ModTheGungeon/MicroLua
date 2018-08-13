using System;
using System.Runtime.InteropServices;

namespace MicroLua {
    public partial class LuaState {
        //////////////////////////
        //*** LUA REFERENCES ***//
        //////////////////////////

        private const int LUA_REFTABLE_CHECK_POINT = 50;
        private int _LuaRefCount = 0;

        private int _CheckLuaReference() {
            for (int i = 0; i < _LuaRefCount; i++) {
                Lua.lua_rawgeti(Pointer, -1, i);
                var type = Type();
                Pop();

                if (type == LuaType.Nil) {
                    return i;
                }
            }
            return -1;
        }

        public int _GetNextLuaReference() {
            int idx;

            if (_LuaRefCount % LUA_REFTABLE_CHECK_POINT == 0 && _LuaRefCount > 0) {
                idx = _CheckLuaReference();

                if (idx != -1) {
                    return idx;
                }
            }

            idx = _LuaRefCount;
            _LuaRefCount++;
            return idx;
        }

        private int _MakeLuaReference(int index) {
            index = Lua.abs_index(Pointer, index);
            Lua.lua_getfield(Pointer, Lua.LUA_REGISTRYINDEX, LUA_REFTABLE_KEY);
            var refidx = _GetNextLuaReference();

            Lua.lua_pushvalue(Pointer, index);
            Lua.lua_rawseti(Pointer, -2, refidx);
            Pop();

            return refidx;
        }

        private void _DeleteLuaReference(int refidx) {
            Lua.lua_getfield(Pointer, Lua.LUA_REGISTRYINDEX, LUA_REFTABLE_KEY);
            PushNil();
            Lua.lua_rawseti(Pointer, -2, refidx);
            Pop();
        }

        private void _PushLuaReference(int refidx) {
            Lua.lua_getfield(Pointer, Lua.LUA_REGISTRYINDEX, LUA_REFTABLE_KEY);
            Lua.lua_rawgeti(Pointer, -1, refidx);
            Lua.lua_insert(Pointer, -2); // swap the last 2 stack elements
            Pop(); // pop the top element which is now the registry table
            // we're left with just the ref object
        }

        private void _SetupLuaReftable() {
            Lua.lua_newtable(Pointer);
            Lua.lua_setfield(Pointer, Lua.LUA_REGISTRYINDEX, LUA_REFTABLE_KEY);
        }

        public int MakeLuaReference(int index = -1) {
            return _MakeLuaReference(index);
        }

        public void DeleteLuaReference(int refidx) {
            _DeleteLuaReference(refidx);
        }

        public void PushLuaReference(int refidx) {
            _PushLuaReference(refidx);
        }

        //////////////////////////
        //*** CLR REFERENCES ***//
        //////////////////////////

        private int _MakeCLRReference(object o) {
            var refidx = Refs.AddRef(o);
            OwnedRefs.Add(refidx);
            return refidx;
        }

        private static void _PushCLRReference(IntPtr L, int reference) {
            var ud = Lua.lua_newuserdata(L, (UIntPtr)Marshal.SizeOf(typeof(IntPtr)));
            Marshal.WriteIntPtr(ud, new IntPtr(reference));
        }

        private void _PushCLRReference(int reference) {
            _PushCLRReference(Pointer, reference);
        }

        private static int _GetCLRReference(IntPtr L, int index) {
            var ud = Lua.lua_touserdata(L, index);
            if (ud == IntPtr.Zero) {
                throw new LuaException($"Object at index {index} is not a MicroLua CLR object");
            }
            var refidx = Marshal.ReadIntPtr(ud);
            return (int)refidx;
            // this is okay, because when we push we push an int converted to an intptr
            // so overflow is impossible without tampering which we don't care about
        }

        private int _GetCLRReference(int index) {
            return _GetCLRReference(Pointer, index);
        }

        public static object GetCLRReference(IntPtr L, int index = -1) {
            return Refs.GetRef(
                _GetCLRReference(L, index)
            );
        }

        public static object GetCLRReferenceUpvalue(IntPtr L, int upvalue) {
            return Refs.GetRef(
                _GetCLRReference(L, Lua.lua_upvalueindex(upvalue))
            );
        }

        public object GetCLRReference(int index = -1) {
            return GetCLRReference(Pointer, index);
        }

        public object GetCLRReferenceUpvalue(int upvalue) {
            return GetCLRReferenceUpvalue(Pointer, upvalue);
        }
    }
}
