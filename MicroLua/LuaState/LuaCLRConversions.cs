using System;
namespace MicroLua {
    public partial class LuaState {
        internal static Type MatchingCLRType(IntPtr L, int index = -1) {
            var lua_type = Lua.lua_type(L, index);
            switch (lua_type) {
            case LuaType.Boolean: return typeof(bool);
            case LuaType.Nil: return typeof(object);
            case LuaType.Number:
                var num = Lua.lua_tonumber(L, index);
                if (num != (long)num) return typeof(double);
                if (Lua.lua_tointeger(L, index) > int.MaxValue) return typeof(long);
                return typeof(int);
            case LuaType.String: return typeof(string);
            case LuaType.Table: return _GetTableCLRType(L, index);
            case LuaType.LightUserdata: return typeof(IntPtr);
            case LuaType.Userdata:
                if (IsCLRObject(L, index)) return typeof(object);
                return typeof(IntPtr);
            default: throw new LuaException($"Can't match Lua type {lua_type} to a CLR type");
            }
        }

        public Type MatchingCLRType(int index = -1) {
            return MatchingCLRType(Pointer, index);
        }

        internal static object ConvertToCLR(IntPtr L, int index = -1) {
            var lua_type = Lua.lua_type(L, index);
            switch (lua_type) {
            case LuaType.Boolean: return Lua.lua_toboolean(L, index);
            case LuaType.Nil: return null;
            case LuaType.Number:
                var num = Lua.lua_tonumber(L, index);
                if (num != (long)num) return num;
                var numlong = Lua.lua_tointeger(L, index);
                if (numlong > int.MaxValue) return numlong;
                return (int)Lua.lua_tointeger(L, index);
            case LuaType.String: return ToString(L, index);
            case LuaType.Table: return _TableToCLR(L, index);
            case LuaType.LightUserdata: return Lua.lua_touserdata(L, index);
            case LuaType.Userdata:
                if (IsCLRObject(L, index)) {
                    return Refs.GetRef(_GetCLRReference(L, index));
                }
                return Lua.lua_touserdata(L, index);
            default: throw new LuaException($"Can't match Lua type {lua_type} to a CLR type");
            }
        }

        public object ConvertToCLR(int index = -1) {
            return ConvertToCLR(Pointer, index);
        }

        private static Type _GetTableCLRType(IntPtr L, int index) {
            Type table_type = null;

            index = Lua.abs_index(L, index);

            bool first = true;

            Lua.lua_pushnil(L);
            while (Lua.lua_next(L, index) != 0) {
                var clr_type = MatchingCLRType(L, -1);
                if (first) table_type = clr_type;
                first = false;
                    
                Lua.lua_pop(L, 1);
            }

            if (table_type == null) {
                throw new LuaException("Can't convert an empty table to a CLR object");
            }

            return typeof(Array).MakeGenericType(table_type);
        }

        private static object _TableToCLR(IntPtr L, int index) {
            int length = 0;

            index = Lua.abs_index(L, index);

            Lua.lua_pushnil(L);
            while (Lua.lua_next(L, index) != 0) {
                length += 1;

                Lua.lua_pop(L, 1);
            }

            Type table_type = null;
            Array ary = null;

            bool first = true;
            int i = 0;

            Lua.lua_pushnil(L);
            while (Lua.lua_next(L, index) != 0) {
                var clr_obj = ConvertToCLR(L, -1);
                if (first) {
                    table_type = clr_obj.GetType();
                    ary = Array.CreateInstance(table_type, length);
                }
                ary.SetValue(clr_obj, i);
                first = false;
                i++;

                Lua.lua_pop(L, 1);
            }

            if (table_type == null) {
                throw new LuaException("Can't convert an empty table to a CLR object");
            }

            return ary;
        }
    }
}
