using System;
using System.Collections.Generic;
using System.Reflection;

namespace MicroLua {
    public partial class LuaState {
        ///////////////////////////
        //*** INTEROP LIBRARY ***//
        ///////////////////////////
        /// 
        private static int _InteropAssembly(LuaState state) {
            // args:
            //   1: assname (string)
            state.CheckArg(LuaType.String, 1);
            var assname = state.ToString(1);
            state.PushCLR(Assembly.Load(assname));
            return 1;
        }

        private static int _InteropType(LuaState state) {
            // args:
            //   1: ass
            //   2: typename (string)
            var asm = state.CheckArg<Assembly>(1);
            state.CheckArg(LuaType.String, 2);
            var typename = state.ToString(2);
            state.PushType(asm.GetType(typename));
            return 1;
        }

        private static int _InteropNamespace(LuaState state) {
            // args:
            //   1: ass
            //   2: namespace (string)
            var asm = state.CheckArg<Assembly>(1);
            state.CheckArg(LuaType.String, 2);
            var @namespace = state.ToString(2);
            state.PushNewTable();
            var types = asm.GetTypes();
            for (int i = 0; i < types.Length; i++) {
                var type = types[i];
                var type_namespace = type.Namespace;
                if (type_namespace == null) type_namespace = "-.";
                if (type_namespace.StartsWith($"{@namespace}.", StringComparison.InvariantCulture)) {
                    state.PushCLR(type);
                    state.SetField(type.Name);
                }
            }
            return 1;
        }

        public void LoadInteropLibrary() {
            EnterArea();
            PushNewTable();

            PushLuaCLRFunction(_InteropAssembly);
            SetField("assembly");

            PushLuaCLRFunction(_InteropType);
            SetField("type");

            PushLuaCLRFunction(_InteropNamespace);
            SetField("namespace");

            SetGlobal("interop");
            LeaveAreaCleanup();
        }
    }
}
