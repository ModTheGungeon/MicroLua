using System;
namespace MicroLua {
    internal class LuaGenericParams {
        public Type[] Params;

        public LuaGenericParams(Type[] @params) {
            Params = @params;
        }

        public override string ToString() {
            return $"Generic params proxy object: {Params.Length} generic params";
        }
    }
}
