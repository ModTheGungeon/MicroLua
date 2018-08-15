using System;
using System.Runtime.InteropServices;

namespace MicroLua {
    public partial class LuaState {
        ///////////////////////////
        //*** STACK RETRIEVAL ***//
        ///////////////////////////

        public IntPtr ToLightUserdata(int index = -1) {
            return Lua.lua_touserdata(Pointer, index);
        }

        public IntPtr ToUserdata(int index = -1) {
            return Lua.lua_touserdata(Pointer, index);
        }

        public bool ToBool(int index = -1) {
            return Lua.lua_toboolean(Pointer, index);
        }

        public double ToDouble(int index = -1) {
            return Lua.lua_tonumber(Pointer, index);
        }

        public int ToInt(int index = -1) {
            return (int)Lua.lua_tointeger(Pointer, index);
        }

        public long ToLong(int index = -1) {
            return Lua.lua_tointeger(Pointer, index);
        }

        public lua_CFunction ToCFunction(int index = -1) {
            return Lua.lua_tocfunction(Pointer, index);
        }

        internal static string ToString(IntPtr L, int index = -1) {
            var type = Lua.lua_type(L, index);
            if (type == LuaType.Number) {
                Lua.lua_insert(L, -1);
                index = -1;
            }

            var len = UIntPtr.Zero;
            var ptr = Lua.lua_tolstring(L, index, ref len);
            if (ptr == IntPtr.Zero) {
                return null;
            }
            var str = Marshal.PtrToStringAnsi(ptr, (int)len);

            return str;
        }

        public string ToString(int index = -1) {
            return ToString(Pointer, index);
        }

        private object _ToRefObject(int index = -1) {
            var idx = _GetCLRReference(index);
            return Refs.GetRef(idx);
        }

        private object _ToNumber(int index = -1) {
            var @double = ToDouble(index);
            var @long = ToLong(index);
            if (@double != @long) {
                return @double;
            } else if (@long > int.MaxValue) {
                return @long;
            }
            return (int)@long;
        }
    }
}
