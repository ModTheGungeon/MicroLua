using System;
using System.Collections.Generic;
using System.Reflection;

namespace MicroLua {
    public partial class LuaState {
        ////////////////////////////
        //*** CLR TYPES IN LUA ***//
        ////////////////////////////

        private void _ConstructTypeObjectMetatable() {
            EnterArea();
            if (Lua.luaL_newmetatable(Pointer, TYPE_OBJECT_METATABLE_NAME) == 1) {
                PushLuaCFunction(_ClrObjectFinalizer);
                SetField("__gc");

                _PushCLRReference(SelfRef);
                PushBool(true);
                PushLuaCClosure(_ClrObjectIndex, 2);
                SetField("__index");

                _PushCLRReference(SelfRef);
                PushBool(true);
                PushLuaCClosure(_ClrObjectNewIndex, 2);
                SetField("__newindex");

                PushLuaReference(_MicroLuaMakeCallWrapperRef);
                _PushCLRReference(SelfRef);
                PushCLR(new LuaCLRConstructorProxy(null));
                // type will be filled in by _ConstructorInvoke
                PushInt((int)(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic));
                PushLuaCClosure(_ConstructorInvoke, 3);
                ProtCall(1, results: 1);
                SetField("__call");

                _PushCLRReference(SelfRef);
                PushLuaCClosure(_ClrObjectToString, 1);
                SetField("__tostring");
            }
            LeaveAreaCleanup();
        }

        public int PushType(Type t) {
            var refidx = _MakeCLRReference(t);
            _PushCLRReference(refidx);
            Lua.luaL_getmetatable(Pointer, TYPE_OBJECT_METATABLE_NAME);
            Lua.lua_setmetatable(Pointer, -2);
            return refidx;
        }
    }
}
