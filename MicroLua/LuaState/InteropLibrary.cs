using System;
using System.Collections.Generic;
using System.Reflection;

namespace MicroLua {
    public partial class LuaState {
        private static object _InteropAssembly(LuaState state) {
            // args:
            //   1: assname (string)
            state.CheckArg(LuaType.String, 1);
            var assname = state.ToString(1);
            return Assembly.Load(assname);
        }

        private static object _InteropType(LuaState state) {
            // args:
            //   1: ass
            //   2: typename (string)
            var asm = state.CheckArg<Assembly>(1);
            state.CheckArg(LuaType.String, 2);
            var typename = state.ToString(2);
            return asm.GetType(typename);
        }

        ///////////////////////////
        //*** INTEROP LIBRARY ***//
        ///////////////////////////
        public void LoadInteropLibrary() {
            EnterArea();
            PushNewTable();

            PushLuaCLRFunction(_InteropAssembly);
            SetField("assembly");

            PushLuaCLRFunction(_InteropType);
            SetField("type");

            SetGlobal("interop");
            LeaveAreaCleanup();
        }
    }
}
