using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace MicroLua {
    public partial class LuaState : IDisposable {
        public static RefTable Refs = new RefTable();

        public const string CLR_OBJECT_METATABLE_NAME = "MICROLUA_CLROBJECT";
        public const string TYPE_OBJECT_METATABLE_NAME = "MICROLUA_TYPEOBJECT";
        public const string LUA_REFTABLE_KEY = "MICROLUA_REFERENCES";

        public void Dispose() {
            for (int i = 0; i < OwnedRefs.Count; i++) {
                Refs.DelRef(OwnedRefs[i]);
            }
            DeleteLuaReference(_MicroLuaMakeCallWrapperRef);
            Lua.lua_close(Pointer);

            //Console.WriteLine("REFS:");
            //for (int i = 0; i < Refs.Count; i++) {
            //    Console.WriteLine($"- {i}: {Refs.GetRef(i)}");
            //}
        }

        public IntPtr Pointer;
        public int SelfRef = -1;
        public List<int> OwnedRefs = new List<int>();
        private int _MicroLuaMakeCallWrapperRef;
        private int _DebugTracebackRef;

        public LuaState() {
            Pointer = Lua.luaL_newstate();
            SelfRef = _MakeCLRReference(this);
            _ConstructCLRObjectMetatable();
            _ConstructTypeObjectMetatable();
            _SetupLuaReftable();
            Lua.luaL_openlibs(Pointer);
            _LoadErrorMechanism();
            // this ref will not be GC'd if the lua object is collected
            // because _MakeRef/_PushRef don't assign the metatable
        }

        private void _LoadErrorMechanism() {
            string code;
            using (var s = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream("ErrorMechanism.lua"), Encoding.UTF8)) {
                code = s.ReadToEnd();
            }
            LoadBuffer("MicroLua", code);
            if (VoidProtCall(0) != LuaResult.OK) {
                var ex = new LuaException(ToString());
                Pop();
                throw ex;
            }

            GetGlobal("microlua_make_call_wrapper");
            _MicroLuaMakeCallWrapperRef = MakeLuaReference();
            Pop();

            GetGlobal("debug");
            GetField("traceback");
            _DebugTracebackRef = MakeLuaReference();
            Pop(2);
        }
    }
}
