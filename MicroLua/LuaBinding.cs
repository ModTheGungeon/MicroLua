using System;
using System.Reflection;

namespace MicroLua.LuaBinding {
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class LuaClassAttribute : Attribute {}

    [AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
    public class ExposeAttribute : Attribute { }
}
