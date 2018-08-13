using System;
using System.Runtime.InteropServices;

namespace MicroLua{
    public partial class LuaState {
        ////////////////////////////
        //*** STACK OPERATIONS ***//
        ////////////////////////////
         
        private void _CheckStackMin(int val) {
            if (StackTop < val) {
                throw new LuaException("Stack is too small for this operation");
            }
        }

        public int StackTop {
            get { return Lua.lua_gettop(Pointer); }
            set { Lua.lua_settop(Pointer, value); }
        }

        public void Insert(int index) {
            Lua.lua_insert(Pointer, index);
        }

        public void Pop(int n = 1) {
            _CheckStackMin(n);
            Lua.lua_pop(Pointer, n);
        }

        public LuaType Type(int n = -1) {
            return Lua.lua_type(Pointer, n);
        }

        public void PushInt(int n) {
            Lua.lua_pushinteger(Pointer, new IntPtr(n));
        }

        public void PushLong(long n) {
            Lua.lua_pushinteger(Pointer, new IntPtr(n));
        }

        public void PushDouble(double n) {
            Lua.lua_pushnumber(Pointer, n);
        }

        private IntPtr _CopyString(string s) {
            IntPtr sptr = Marshal.AllocHGlobal(s.Length + 1);
            for (int i = 0; i < s.Length; i++) {
                Marshal.WriteByte(sptr, i, (byte)s[i]);
            }
            Marshal.WriteByte(sptr, s.Length, 0);
            return sptr;
        }

        public void PushString(string s) {
            Lua.lua_pushlstring(Pointer, _CopyString(s), s.Length);
        }

        public void PushLuaCFunction(lua_CFunction f) {
            Lua.lua_pushcfunction(Pointer, f);
        }

        public void PushLuaCClosure(lua_CFunction f, int n) {
            Lua.lua_pushcclosure(Pointer, f, n);
        }

        public void PushBool(bool b) {
            Lua.lua_pushboolean(Pointer, b);
        }

        public void PushNil() {
            Lua.lua_pushnil(Pointer);
        }

        public void PushThread() {
            Lua.lua_pushthread(Pointer);
        }


        public void Push(object o) {
            if (o == null) {
                PushNil();
                return;
            }
            var type = o.GetType();
            if (type == typeof(bool)) {
                PushBool((bool)o);
                return;
            } else if (type == typeof(short) || type == typeof(int) || type == typeof(long)) {
                PushLong((long)Convert.ChangeType(o, typeof(long)));
                return;
            } else if (type == typeof(string)) {
                PushString((string)o);
                return;
            }
            PushCLR(o);
        }

        // To be used only within a proper error handled context
        // (LuaCLRFunctions and LuaCLRMethods)

        public void CheckArg(LuaType type, int index = -1) {
            if (Type(index) != type) {
                throw new LuaException($"Argument #{index}: expected {type}, got {Type(index)}");
            }
        }


        public object CheckArg(Type type, int index = -1) {
            if (Type(index) != LuaType.Userdata) {
                throw new LuaException($"Argument #{index}: expected {type} userdata, got {Type(index)}");
            }

            if (IsCLRObject(index)) {
                var obj = GetCLRReference(index);
                var actual_type = obj.GetType();
                if (!type.IsAssignableFrom(actual_type)) {
                    throw new LuaException($"Argument #{index}: expected {type} userdata, got {actual_type} userdata");
                }
                return obj;
            }
            return null;
        }


        public T CheckArg<T>(int index = -1) {
            return (T)CheckArg(typeof(T), index);
        }
    }
}
