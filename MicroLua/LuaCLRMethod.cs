using System;
using System.Reflection;

namespace MicroLua {
    public delegate int LuaCLRFunction(LuaState state);

    public class LuaCLRMethodInfo {
        public Type Type { get; internal set; }
        public string Name { get; internal set; }

        public LuaCLRMethodInfo(Type type, string name) {
            Type = type;
            Name = name;
        }

        private MethodInfo _AcquireMethod(BindingFlags binding_flags, object[] @params) {
            if (@params.Length == 0) {
                return Type.GetMethod(Name, binding_flags, null, Type.EmptyTypes, null);
            } else {
                var types = new Type[@params.Length];
                for (int i = 0; i < @params.Length; i++) {
                    types[i] = @params[i].GetType();
                }
                return Type.GetMethod(Name, binding_flags, null, types, null);
            }
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

        private object _InvokeConstructor(BindingFlags binding_flags, params object[] @params) {
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

        public object Invoke(object target, BindingFlags binding_flags, params object[] @params) {
            if (Name == ".ctor") return _InvokeConstructor(binding_flags, @params);

            var method = _AcquireMethod(binding_flags, @params);
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
}
