using System;
using System.Reflection;

namespace MicroLua {
    public partial class LuaState {
        private static int _ClrObjectFinalizer(IntPtr L) {
            var idx = _GetCLRReference(L, 1);
            Refs.DelRef(idx);
            return 0;
        }

        private static object[] _EmptyObjectArray = new object[] { };
        private static bool _TypeHasMethod(Type type, BindingFlags binding_flags, string name) {
            try {
                return type.GetMethod(name, binding_flags) != null;
            } catch (AmbiguousMatchException) {
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
                _GetCLRReference(L, Lua.lua_upvalueindex(1))
            );
            var self = Refs.GetRef(
                _GetCLRReference(L, 1)
            );
            state.PushString(self.ToString());
            return 1;
        }

        private static int _ClrObjectIndex(IntPtr L) {
            // upvalues:
            //   1 - LuaState
            //   2 - static (true/false)
            //       if true, self is Type
            // args:
            //   1 - self
            //   2 - key
            var state = Refs.GetRef<LuaState>(
                _GetCLRReference(L, Lua.lua_upvalueindex(1))
            );

            var @static = state.ToBool(Lua.lua_upvalueindex(2));

            var self = Refs.GetRef(_GetCLRReference(L, 1));
            var binding_flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            Type type;
            if (@static) {
                type = self as Type;
                self = null;
                binding_flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            } else {
                type = self.GetType();
            }
            var key = state.ToString(2);

            // try field first
            var field = type.GetField(key, binding_flags);
            if (field != null) {
                var value = field.GetValue(self);
                state.Push(value);
                return 1;
            }
            // then property
            var prop = type.GetProperty(key, binding_flags);
            if (prop != null) {
                var get = prop.GetGetMethod();
                if (get != null) {
                    var value = get.Invoke(self, _EmptyObjectArray);
                    state.Push(value);
                    return 1;
                } else {
                    state.PushNil();
                    return 1;
                }
            }
            // and now, method
            if (_TypeHasMethod(type, binding_flags, key)) {
                // for now we allow access of all methods, including private                
                // need to figure out how to handle that
                state.PushLuaCLRMethod(type, key, binding_flags);
                return 1;
            }

            // found nothing? return nil
            state.PushNil();
            return 1;
        }

        private static int _ClrObjectNewIndex(IntPtr L) {
            // upvalues:
            //   1 - LuaState
            //   2 - static (true/false)
            //       if true, self is Type
            // args:
            //   1 - self
            //   2 - key
            //   3 - value

            var state = Refs.GetRef<LuaState>(
                _GetCLRReference(L, Lua.lua_upvalueindex(1))
            );
            var @static = state.ToBool(Lua.lua_upvalueindex(2));
            var self = Refs.GetRef(_GetCLRReference(L, 1));
            var binding_flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            Type type;
            if (@static) {
                type = self as Type;
                self = null;
                binding_flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            } else {
                type = self.GetType();
            }
            var key = state.ToString(2);
            var target_value = state.ToCLR(3);

            // try field first
            var field = type.GetField(key, binding_flags);
            if (field != null) {
                field.SetValue(self, target_value);
                return 0;
            }
            // then property
            var prop = type.GetProperty(key, binding_flags);
            if (prop != null) {
                var set = prop.GetSetMethod();
                if (set != null) {
                    var value = set.Invoke(self, new object[] { target_value });
                    return 0;
                } else {
                    state.PushString($"Can't set property '{key}' as it does not have a setter");
                    return Lua.lua_error(L);
                }
            }
            // no methods here
            // but in the future we could somehow use this for
            // native patching?

                 // TODO: don't use lua_error
            state.PushString($"Field/property '{key}' does not exist");
            return Lua.lua_error(L);
        }

        private static int _MethodInvoke(IntPtr L) {
            // upvalues:
            //   1 - LuaState
            //   2 - LuaCLRMethodInfo
            //   3 - binding_flags (as int)
            // args:
            //   1 - self/target
            //   ... - params
            var state = Refs.GetRef<LuaState>(
                _GetCLRReference(L, Lua.lua_upvalueindex(1))
            );

            var method = Refs.GetRef<LuaCLRMethodInfo>(
                _GetCLRReference(L, Lua.lua_upvalueindex(2))
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
                        _GetCLRReference(L, 1)
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
                state.PushBool(false);
                //state.Push($"[{e.GetType()}] {e.Message}");
                state.PushCLR(e);
                return 2;
            }
            // ErrorMechanism.lua protocol:
            //   return 2 values
            //   1 - flag (true - success, false - failure)
            //   2 - error string or return value

            state.PushBool(true);
            state.Push(result);
            return 2;
        }

        private static int _LuaCLRFunctionInvoke(IntPtr L) {
            // upvalues:
            //   1 - LuaState
            //   2 - LuaCLRFunction
            // args:
            //   ... - params
            var state = Refs.GetRef<LuaState>(
                _GetCLRReference(L, Lua.lua_upvalueindex(1))
            );

            var func = Refs.GetRef<LuaCLRFunction>(
                _GetCLRReference(L, Lua.lua_upvalueindex(2))
            );

            var params_len = Lua.lua_gettop(L);
            var params_ary = new object[params_len];
            var params_begin = 1;
            for (int i = params_begin; i <= params_len; i++) {
                var param = ConvertToCLR(L, i);
                params_ary[i - (params_begin)] = param;
            }

            var top = Lua.lua_gettop(L);
            int results = 0;
            try {
                results = func.Invoke(state);
            } catch (Exception e) {
                state.PushBool(false);
                //state.Push($"[{e.GetType()}] {e.Message}");
                state.PushCLR(e);
                return 2;
            }
            // ErrorMechanism.lua protocol:
            //   return 2 values
            //   1 - flag (true - success, false - failure)
            //   2 - error string or return value

            state.PushBool(true);
            state.Insert(top + 1);

            return 1 + results;
        }
    }
}
