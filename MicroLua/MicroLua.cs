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
        public const string LUA_REFTABLE_KEY = "MICROLUA_REFERENCES";

        private static int _ClrObjectFinalizer(IntPtr L) {
            var idx = _GetRef(L, 1);
            Refs.DelRef(idx);
            return 0;
        }

        private static object[] _EmptyObjectArray = new object[] { };
        private static bool _TypeHasMethod(Type type, string name) {
            try {
                return type.GetMethod(name) != null;
            } catch(AmbiguousMatchException) {
                // I don't like this, this is ugly
                return true;
            }
        }

        private static int _ClrObjectToString(IntPtr L) {
            // upvalues:
            //   1 - LuaState
            // args:
            //   1 - self
            var state = Refs.GetRef<LuaState>(
                _GetRef(L, Lua.lua_upvalueindex(1))
            );
            var self = Refs.GetRef(
                _GetRef(L, 1)
            );
            state.Push(self.ToString());
            return 1;
        }

        private static int _ClrObjectIndex(IntPtr L) {
            // upvalues:
            //   1 - LuaState
            // args:
            //   1 - self
            //   2 - key

            var state = Refs.GetRef<LuaState>(
                _GetRef(L, Lua.lua_upvalueindex(1))
            );
            var self = Refs.GetRef(_GetRef(L, 1));
            var type = self.GetType();
            var key = state.ToString(2);

            // try field first
            var field = type.GetField(key);
            if (field != null) {
                var value = field.GetValue(self);
                state.PushAny(value);
                return 1;
            }
            // then property
            var prop = type.GetProperty(key);
            if (prop != null) {
                var get = prop.GetGetMethod();
                if (get != null) {
                    var value = get.Invoke(self, _EmptyObjectArray);
                    state.PushAny(value);
                    return 1;
                } else {
                    state.PushNil();
                    return 1;
                }
            }
            // and now, method
            if (_TypeHasMethod(type, key)) {
                // for now we allow access of all methods, including private                
                // need to figure out how to handle that
                state.PushCLRMethod(type, key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                return 1;
            }
                
            // found nothing? return nil
            state.PushNil();
            return 1;
        }

        private static int _ClrObjectNewIndex(IntPtr L) {
            // upvalues:
            //   1 - LuaState
            // args:
            //   1 - self
            //   2 - key
            //   3 - value

            var state = Refs.GetRef<LuaState>(
                _GetRef(L, Lua.lua_upvalueindex(1))
            );
            var self = Refs.GetRef(_GetRef(L, 1));
            var type = self.GetType();
            var key = state.ToString(2);
            var target_value = state.ToCLR(3);

            // try field first
            var field = type.GetField(key);
            if (field != null) {
                field.SetValue(self, target_value);
                return 0;
            }
            // then property
            var prop = type.GetProperty(key);
            if (prop != null) {
                var set = prop.GetSetMethod();
                if (set != null) {
                    var value = set.Invoke(self, new object[] { target_value });
                    return 0;
                } else {
                    state.Push($"Can't set property '{key}' as it does not have a setter");
                    return Lua.lua_error(L);
                }
            }
            // no methods here
            // but in the future we could somehow use this for
            // native patching?

            // TODO: don't use lua_error
            state.Push($"Field/property '{key}' does not exist");
            return Lua.lua_error(L);
        }

        private static int _MethodInvoke(IntPtr L) {
            // args:
            //   1 - self/target
            //   ... - params
            // upvalues:
            //   1 - LuaState
            //   2 - LuaCLRMethodInfo
            //   3 - binding_flags (as int)
            var state = Refs.GetRef<LuaState>(
                _GetRef(L, Lua.lua_upvalueindex(1))
            );

            var method = Refs.GetRef<LuaCLRMethodInfo>(
                _GetRef(L, Lua.lua_upvalueindex(2))
            );

            var binding_flags = (BindingFlags)(int)Lua.lua_tointeger(L, Lua.lua_upvalueindex(3));


            object target = null;
            var params_offset = 0;
            if ((binding_flags & BindingFlags.Static) == 0) {
                // if not static
                params_offset = 1;
                if (Lua.lua_type(L, 1) == LuaType.Nil) {
                    target = null;
                } else {
                    target = Refs.GetRef<object>(
                        _GetRef(L, 1)
                    );
                }
            }

            var params_len = Lua.lua_gettop(L) - params_offset;
            var params_ary = new object[params_len];
            var params_begin = 1 + params_offset;
            for (int i = params_begin; i <= params_len + params_offset; i++) {
                var param = ConvertToCLR(L, i);
                params_ary[i - (params_begin)] = param;
            }

            object result = null;
            try {
                result = method.Invoke(target, binding_flags, params_ary);
            } catch (Exception e) {
                state.Push(false);
                //state.Push($"[{e.GetType()}] {e.Message}");
                state.PushCLR(e);
                return 2;
            }
            // ErrorMechanism.lua protocol:
            //   return 2 values
            //   1 - flag (true - success, false - failure)
            //   2 - error string or return value

            state.Push(true);
            state.PushAny(result);
            return 2;
        }



        public enum UserdataType {
            Generic,
            Method
        }

        public void Dispose() {
            for (int i = 0; i < OwnedRefs.Count; i++) {
                Refs.DelRef(OwnedRefs[i]);
            }
            DeleteReference(_MicroLuaMakeCallWrapperRef);
            Lua.lua_close(Pointer);

            //Console.WriteLine("REFS:");
            //for (int i = 0; i < Refs.Count; i++) {
            //    Console.WriteLine($"- {i}: {Refs.GetRef(i)}");
            //}
        }

        public IntPtr Pointer;
        public bool LibsOpened = false;
        public int SelfRef = -1;
        public List<int> OwnedRefs = new List<int>();
        private int _MicroLuaMakeCallWrapperRef;

        public LuaState() {
            Pointer = Lua.luaL_newstate();
            SelfRef = _MakeRef(this);
            _ConstructCLRObjectMetatable();
            _SetupLuaReftable();
            OpenLibs();
            _LoadErrorMechanism();
            // this ref will not be GC'd if the lua object is collected
            // because _MakeRef/_PushRef don't assign the metatable
        }

        public void OpenLibs() {
            if (LibsOpened) return;
            LibsOpened = true;
            Lua.luaL_openlibs(Pointer);
        }

        public int StackTop {
            get { return Lua.lua_gettop(Pointer); }
            set { Lua.lua_settop(Pointer, value); }
        }

        public LuaResult LoadFile(string path) {
            return Lua.luaL_loadfile(Pointer, path);
        }

        public LuaResult LoadString(string str) {
            return Lua.luaL_loadstring(Pointer, str);
        }

        public LuaResult LoadBuffer(string chunk_name, string str) {
            return Lua.luaL_loadbuffer(Pointer, str, (UIntPtr)str.Length, chunk_name);
        }

        public void Pop(int n = 1) {
            _CheckStackMin(n);
            Lua.lua_pop(Pointer, n);
        }

        public void Push(int n) {
            Lua.lua_pushinteger(Pointer, new IntPtr(n));
        }

        public void Push(double n) {
            Lua.lua_pushnumber(Pointer, n);
        }

        public void PushCLRMethod(Type type, string name, BindingFlags binding_flags, object target = null) {
            var methodinfo = new LuaCLRMethodInfo(type, name);
            PushReference(_MicroLuaMakeCallWrapperRef);

            _PushRef(SelfRef);
            PushCLR(methodinfo);
            Push((int)binding_flags);
            Push(_MethodInvoke, 3);

            ProtCall(1, results: 1);
        }

        internal IntPtr CopyString(string s) {
            IntPtr sptr = Marshal.AllocHGlobal(s.Length + 1);
            for (int i = 0; i < s.Length; i++) {
                Marshal.WriteByte(sptr, i, (byte)s[i]);
            }
            Marshal.WriteByte(sptr, s.Length, 0);
            return sptr;
        }

        public void Push(string s) {
            Lua.lua_pushlstring(Pointer, CopyString(s), s.Length);
        }

        public void Push(lua_CFunction f) {
            Lua.lua_pushcfunction(Pointer, f);
        }

        public void Push(lua_CFunction f, int n) {
            Lua.lua_pushcclosure(Pointer, f, n);
        }

        public void Push(bool b) {
            Lua.lua_pushboolean(Pointer, b);
        }

        public void PushNil() {
            Lua.lua_pushnil(Pointer);
        }

        public void PushThread() {
            Lua.lua_pushthread(Pointer);
        }

        public int PushCLR(object o) {
            var refidx = _MakeRef(o);
            _PushRef(refidx);
            Lua.luaL_getmetatable(Pointer, CLR_OBJECT_METATABLE_NAME);
            Lua.lua_setmetatable(Pointer, -2);
            return refidx;
        }

        internal void PushAny(object o) {
            // TODO: make Push overloads into PushInt, PushLong etc
            // then have PushCLR be PushAny

            if (o == null) {
                PushNil();
                return;
            }
            var type = o.GetType();
            if (type == typeof(bool)) {
                Push((bool)o);
            } else if (type == typeof(short) || type == typeof(int) || type == typeof(long)) {
                Push((long)Convert.ChangeType(o, typeof(long)));
                return;
            } else if (type == typeof(string)) {
                Push((string)o);
                return;
            }
            PushCLR(o);
        }

        public LuaType Type(int n = -1) {
            return Lua.lua_type(Pointer, n);
        }

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

        public void Insert(int index) {
            Lua.lua_insert(Pointer, index);
        }

        internal static string ToString(IntPtr L, int index = -1) {
            var type = Lua.lua_type(L, index);
            if (type == LuaType.Number) {
                Lua.lua_insert(L, -1);
                index = -1;
            }

            var len = UIntPtr.Zero;
            var ptr = Lua.lua_tolstring(L, index, ref len);
            if (ptr == IntPtr.Zero) return null;
            var str = Marshal.PtrToStringAuto(ptr, (int)len);

            return str;
        }

        public string ToString(int index = -1) {
            return ToString(Pointer, index);
        }

        private object _ToRefObject(int index = -1) {
            var idx = _GetRef(index);
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

        public object ToCLR(int index = -1) {
            var type = Type(index);
            switch(type) {
            case LuaType.Boolean: return ToBool(index);
            case LuaType.LightUserdata: return ToLightUserdata(index);
            case LuaType.Nil: return null;
            case LuaType.None: return null;
            case LuaType.Number: return _ToNumber(index);
            case LuaType.String: return ToString(index);
            case LuaType.Userdata: 
                int refidx;
                try {
                    refidx = _GetRef(index);
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

        public void GetField(string name, int index = -2) {
            Lua.lua_getfield(Pointer, index, name);
        }

        public void GetGlobal(string name) {
            Lua.lua_getglobal(Pointer, name);
        }

        public void SetField(string name, int index = -2) {
            Lua.lua_setfield(Pointer, index, name);
        }

        public void SetGlobal(string name) {
            Lua.lua_setglobal(Pointer, name);
        }

        public void StartIter() {
            Lua.lua_pushnil(Pointer);
        }

        public bool Iter(int index = -1) {
            return Lua.lua_next(Pointer, index) != 0;
        }

        public void NextIter() {
            Lua.lua_pop(Pointer, 1);
        }

        public LuaResult ProtCall(int args, int results = Lua.LUA_MULTRET) {
            _CheckStackMin(1);
            var top = StackTop;
            var result = Lua.lua_pcall(Pointer, args, results, 0);
            return result;
        }


        public LuaResult VoidProtCall(int args) {
            _CheckStackMin(1);
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

        public void DoString(string str) {
            Lua.lua_dostring(Pointer, str);
        }

        internal List<int> StackTopStack = new List<int>();

        public void EnterArea() {
            StackTopStack.Add(StackTop);
        }

        public void LeaveAreaCleanup() {
            if (StackTopStack.Count == 0) {
                throw new MicroLuaException("Can't pop stack because the stack stack is empty.");
            }
            var top = StackTopStack[StackTopStack.Count - 1];
            StackTopStack.RemoveAt(StackTopStack.Count - 1);
            StackTop = top;
        }

        public void LeaveArea() {
            if (StackTopStack.Count == 0) {
                throw new MicroLuaException("Can't pop stack because the stack stack is empty.");
            }
            var top = StackTopStack[StackTopStack.Count - 1];
            StackTopStack.RemoveAt(StackTopStack.Count - 1);
            if (top != StackTop) {
                throw new MicroLuaException("Stack wasn't clean");
            }
        }

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

        public int MakeReference(int index = -1) {
            return _MakeLuaRef(index);
        }

        public void DeleteReference(int refidx) {
            _DeleteLuaRef(refidx);
        }

        public void PushReference(int refidx) {
            _PushLuaRef(refidx);
        }

        private void _CheckStackMin(int val) {
            if (StackTop < val) {
                throw new LuaException("Stack is too small for this operation");
            }
        }

        private int _MakeRef(object o) {
            var refidx = Refs.AddRef(o);
            OwnedRefs.Add(refidx);
            return refidx;
        }

        private static void _PushRef(IntPtr L, int reference) {
            var ud = Lua.lua_newuserdata(L, (UIntPtr)Marshal.SizeOf(typeof(IntPtr)));
            Marshal.WriteIntPtr(ud, new IntPtr(reference));
        }

        private void _PushRef(int reference) {
            _PushRef(Pointer, reference);
        }

        private static int _GetRef(IntPtr L, int index) {
            var ud = Lua.lua_touserdata(L, index);
            if (ud == IntPtr.Zero) {
                throw new LuaException($"Object at index {index} is not a MicroLua CLR object");
            }
            var refidx = Marshal.ReadIntPtr(ud);
            return (int)refidx;
            // this is okay, because when we push we push an int converted to an intptr
            // so overflow is impossible without tampering which we don't care about
        }

        private int _GetRef(int index) {
            return _GetRef(Pointer, index);
        }

        private void _ConstructCLRObjectMetatable() {
            EnterArea();
            if (Lua.luaL_newmetatable(Pointer, CLR_OBJECT_METATABLE_NAME) == 1) {
                Push((int)UserdataType.Generic);
                SetField("__type");

                Push(_ClrObjectFinalizer);
                SetField("__gc");

                _PushRef(SelfRef);
                Push(_ClrObjectIndex, 1);
                SetField("__index");

                _PushRef(SelfRef);
                Push(_ClrObjectNewIndex, 1);
                SetField("__newindex");

                _PushRef(SelfRef);
                Push(_ClrObjectToString, 1);
                SetField("__tostring");
            }
            LeaveAreaCleanup();
        }

        private const int LUA_REFTABLE_CHECK_POINT = 50;
        private int _LuaRefCount = 0;

        private int _CheckLuaRef() {
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

        public int _GetNextLuaRef() {
            int idx;

            if (_LuaRefCount % LUA_REFTABLE_CHECK_POINT == 0 && _LuaRefCount > 0) {
                idx = _CheckLuaRef();

                if (idx != -1) {
                    return idx;
                }
            }

            idx = _LuaRefCount;
            _LuaRefCount++;
            return idx;
        }

        private int _MakeLuaRef(int index) {
            index = Lua.abs_index(Pointer, index);
            Lua.lua_getfield(Pointer, Lua.LUA_REGISTRYINDEX, LUA_REFTABLE_KEY);
            var refidx = _GetNextLuaRef();

            Lua.lua_pushvalue(Pointer, index);
            Lua.lua_rawseti(Pointer, -2, refidx);
            Pop();

            return refidx;
        }

        private void _DeleteLuaRef(int refidx) {
            Lua.lua_getfield(Pointer, Lua.LUA_REGISTRYINDEX, LUA_REFTABLE_KEY);
            PushNil();
            Lua.lua_rawseti(Pointer, -2, refidx);
            Pop();
        }

        private void _PushLuaRef(int refidx) {
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
            _MicroLuaMakeCallWrapperRef = MakeReference();
            Pop();
        }
    }
}
