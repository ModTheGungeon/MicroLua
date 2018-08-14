using System;
using System.Reflection;

namespace MicroLua {
    public delegate int LuaCLRFunction(LuaState state);

    public class LuaCLRMethodInfo {
        public Type Type { get; private set; }
        public string Name { get; private set; }

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

        public object Invoke(object target, BindingFlags binding_flags, params object[] @params) {
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
