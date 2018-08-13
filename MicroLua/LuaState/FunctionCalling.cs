using System;
using System.Linq;

namespace MicroLua {
    public partial class LuaState {
        ////////////////////////////
        //*** FUNCTION CALLING ***//
        ////////////////////////////

        public void PushTraceback() {
            PushLuaReference(_DebugTracebackRef);
            Lua.lua_call(Pointer, 0, 1);
        }

        public string Traceback() {
            PushTraceback();
            return ToString();
        }

        public string[] TracebackArray() {
            var trace = Traceback();
            var split = trace.Split('\n');
            var trace_ary = new string[split.Length - 1];

            for (int i = 1; i < split.Length; i++) {
                trace_ary[i - 1] = split[i].TrimStart();
            }

            return trace_ary;
        }

        private static int _PcallErrorHandler(IntPtr L) {
            var lua = GetCLRReferenceUpvalue(L, 1) as LuaState;

            Exception inner = null;
            string msg = "An error occured while executing a Lua function";
            object value = null;

            var obj = lua.ToCLR();
            if (obj is Exception) {
                inner = (Exception)obj;
            } else if (obj is String) {
                msg = (String)obj;
            } else {
                msg = obj.ToString();
                value = obj;
            }

            lua.Pop();
            var trace = lua.TracebackArray();
            var ex = new LuaException(msg, trace, value, inner);
            // the first line is always Environment.get_StackTrace
            ex.StackTraceOverride = string.Join("\n", Environment.StackTrace.Split('\n').Skip(1).ToArray());
            lua.Push(ex);

            return 1;
        }

        private void _PushPcallErrorHandler() {
            _PushCLRReference(SelfRef);
            PushLuaCClosure(_PcallErrorHandler, 1);
            //Insert(func_index);
        }


        public void BeginProtCall() {
            _PushPcallErrorHandler();
        }

        public LuaResult ExecProtCall(int args, int results = Lua.LUA_MULTRET) {
            _CheckStackMin(1 + args + 1);
            var top = StackTop;

            var results_start = StackTop - 1;

            var result = Lua.lua_pcall(Pointer, args, results, top - args - 1);
            var results_end = StackTop;
            var results_len = results_end - results_start;
            Lua.lua_remove(Pointer, StackTop - results_len); // pop errhandler
            return result;
        }

        public LuaResult ProtCall(int args, int results = Lua.LUA_MULTRET) {
            _CheckStackMin(1 + args);
            var top = StackTop;

            var result = Lua.lua_pcall(Pointer, args, results, 0);
            return result;
        }


        public LuaResult VoidProtCall(int args) {
            _CheckStackMin(1 + args);
            // function will be popped and then the first result
            // will be pushed in its place
            // current stack top points at the function so we
            // have to go 1 back
            var top = Lua.lua_gettop(Pointer) - 1;
            var result = ProtCall(args, Lua.LUA_MULTRET);

            // in case of error, we DO NOT CLEAN UP THE STACK!
            if (result == LuaResult.OK) Lua.lua_settop(Pointer, top);
            return result;
        }
    }
}
