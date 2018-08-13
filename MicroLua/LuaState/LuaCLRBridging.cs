using System;
using System.Reflection;

namespace MicroLua {
    public partial class LuaState {
        //////////////////////////////
        //*** LUA<->CLR BRIDGING ***//
        //////////////////////////////

        public bool IsCLRObject(int index = -1) {
            if (Type(index) != LuaType.Userdata) return false;
            if (Lua.lua_getmetatable(Pointer, index) == 0) return false;
            Lua.luaL_getmetatable(Pointer, CLR_OBJECT_METATABLE_NAME);
            var eq = Lua.lua_rawequal(Pointer, -1, -2);
            Lua.lua_pop(Pointer, 2);
            return eq;
        }

        private void _ConstructCLRObjectMetatable() {
            EnterArea();
            if (Lua.luaL_newmetatable(Pointer, CLR_OBJECT_METATABLE_NAME) == 1) {
                PushLuaCFunction(_ClrObjectFinalizer);
                SetField("__gc");

                _PushCLRReference(SelfRef);
                PushLuaCClosure(_ClrObjectIndex, 1);
                SetField("__index");

                _PushCLRReference(SelfRef);
                PushLuaCClosure(_ClrObjectNewIndex, 1);
                SetField("__newindex");

                _PushCLRReference(SelfRef);
                PushLuaCClosure(_ClrObjectToString, 1);
                SetField("__tostring");
            }
            LeaveAreaCleanup();
        }

        public int PushCLR(object o) {
            var refidx = _MakeCLRReference(o);
            _PushCLRReference(refidx);
            Lua.luaL_getmetatable(Pointer, CLR_OBJECT_METATABLE_NAME);
            Lua.lua_setmetatable(Pointer, -2);
            return refidx;
        }

        public void PushLuaCLRMethod(Type type, string name, BindingFlags binding_flags, object target = null) {
            var methodinfo = new LuaCLRMethodInfo(type, name);
            PushLuaReference(_MicroLuaMakeCallWrapperRef);

            _PushCLRReference(SelfRef);
            PushCLR(methodinfo);
            PushInt((int)binding_flags);
            PushLuaCClosure(_MethodInvoke, 3);

            ProtCall(1, results: 1);
        }

        public void PushLuaCLRFunction(LuaCLRFunction f) {
            PushLuaReference(_MicroLuaMakeCallWrapperRef);

            _PushCLRReference(SelfRef);
            PushCLR(f);
            PushLuaCClosure(_LuaCLRFunctionInvoke, 2);

            ProtCall(1, results: 1);
        }

        public object ToCLR(int index = -1) {
            var type = Type(index);
            switch (type) {
            case LuaType.Boolean: return ToBool(index);
            case LuaType.LightUserdata: return ToLightUserdata(index);
            case LuaType.Nil: return null;
            case LuaType.None: return null;
            case LuaType.Number: return _ToNumber(index);
            case LuaType.String: return ToString(index);
            case LuaType.Userdata:
                int refidx;
                try {
                    refidx = _GetCLRReference(index);
                } catch {
                    return ToUserdata(index);
                }
                return Refs.GetRef(refidx);
            default: throw new LuaException($"Unsupported Lua->CLR conversion for type {type}");
            }
        }

        public T ToCLR<T>(int index = -1) where T : class {
            return ToCLR(index) as T;
        }
    }
}
