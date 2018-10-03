using System;
using System.Reflection;

namespace MicroLua {
    public delegate int LuaCLRFunction(LuaState state);

    public abstract class LuaCLRInvokeProxy {
        public abstract object Invoke(object target, BindingFlags binding_flags, object[] @params, Type[] generic_params);

        public object Invoke(object target, BindingFlags binding_flags, params object[] @params) {
            Type[] generic_params = null;

            if (@params.Length > 0 && @params[0] != null && @params[0] is LuaGenericParams) {
                generic_params = (@params[0] as LuaGenericParams).Params;

                var new_params = new object[@params.Length - 1];
                Array.Copy(@params, 1, new_params, 0, new_params.Length);

                @params = new_params;
            }

            return Invoke(target, binding_flags, @params, generic_params);
        }
    }

    public class LuaCLRMethodProxy : LuaCLRInvokeProxy {
        public Type Type { get; internal set; }
        public string Name { get; internal set; }

        public LuaCLRMethodProxy(Type type, string name) {
            Type = type;
            Name = name;
        }

        private MethodInfo _AcquireMethod(BindingFlags binding_flags, object[] @params, Type[] generic_params) {
            MethodInfo method;

            if (@params.Length == 0) {
                method = Type.GetMethodExt(Name, binding_flags, generic_params != null, Type.EmptyTypes);
            } else {
                var types = new Type[@params.Length];
                for (int i = 0; i < @params.Length; i++) {
                    types[i] = @params[i].GetType();
                }
                method = Type.GetMethodExt(Name, binding_flags, generic_params != null, types);
            }

            if (method == null) return method;

            if (method.ContainsGenericParameters) {
                if (generic_params == null) {
                    throw new LuaException($"Method {Name} contains generic parameters, but they were not provided.");
                }

                MethodInfo generic_method;

                try {
                    generic_method = method.MakeGenericMethod(generic_params);
                } catch (ArgumentException e) {
                    if (e.Message == "Incorrect length") throw new LuaException($"Incorrect length of generic parameters for method {Name}.");
                    throw;
                }
                if (generic_method == null) {
                    throw new LuaException($"Invalid generic parameters for method {Name}.");
                }
                method = generic_method;
            } else if (generic_params != null) {
                throw new LuaException($"Method {Name} does not contain generic parameters, but it was called with generic parameters.");
            }

            return method;
        }

        public override object Invoke(object target, BindingFlags binding_flags, object[] @params, Type[] generic_params) {
            var method = _AcquireMethod(binding_flags, @params, generic_params);
            if (method == null) {
                throw new LuaException($"Failed acquiring overload for method {Name}");
            }

            try {
                return method.Invoke(target, @params);
            } catch (TargetInvocationException) {
                throw;
            }
        }
    }

    public class LuaCLRConstructorProxy: LuaCLRInvokeProxy {
        public Type Type { get; internal set; }

        public LuaCLRConstructorProxy(Type type) {
            Type = type;
        }

        private ConstructorInfo _AcquireConstructor(BindingFlags binding_flags, object[] @params) {
            if (@params.Length == 0) {
                return Type.GetConstructor(binding_flags, null, Type.EmptyTypes, null);
            } else {
                var types = new Type[@params.Length];
                for (int i = 0; i < @params.Length; i++) {
                    types[i] = @params[i].GetType();
                }
                return Type.GetConstructor(binding_flags, null, types, null);
            }
        }

        public override object Invoke(object target, BindingFlags binding_flags, object[] @params, Type[] generic_params) {
            var ctor = _AcquireConstructor(binding_flags, @params);
            if (ctor == null) {
                throw new LuaException($"Failed acquiring overload for constructor");
            }

            try {
                return ctor.Invoke(@params);
            } catch (TargetInvocationException) {
                throw;
            }
        }
    }
}
