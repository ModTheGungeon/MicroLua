using System;
namespace MicroLua {
    public class MicroLuaException : Exception {
        public MicroLuaException(string msg) : base(msg) {}
    }

    public class LuaException : Exception {
        public LuaException(string msg) : base(msg) { }
    }
}
