using NUnit.Framework;
using System;
using System.Reflection;

namespace MicroLua.Tests {
    public class Helper {
        public static Helper Instance = new Helper();

        public Helper() {}

        public Helper(string a) {
            TestField = a;
        }

        public Helper(int b) {
            TestField = b.ToString();
        }

        public static int Test() {
            return 42;
        }

        public string Test(string name) {
            return $"Hello, {name}!";
        }

        public object TestCLR() {
            return Instance;
        }

        public static string Overload(string a) {
            return a;
        }

        public static int Overload(int a) {
            return a;
        }

        public void CauseException() {
            throw new InvalidOperationException("Hello");
        }

        public string TestField = "TEST";
        private string _TestProp = "TEST";
        public string TestProp {
            get { return _TestProp; }
            set { _TestProp = value; }
        }

        public static string StaticTestField = "TEST";
        private static string _StaticTestProp = "TEST";
        public static string StaticTestProp {
            get { return _StaticTestProp; }
            set { _StaticTestProp = value; }
        }
    }

    [TestFixture]
    public class Basic {
        [Test]
        public void LoadsString() {
            using (var lua = new LuaState()) {
                Assert.AreEqual(lua.StackTop, 0);
                lua.LoadString("print()");
                Assert.AreEqual(lua.StackTop, 1);
            }
        }

        [Test]
        public void PushesAndGetsStrings() {
            using (var lua = new LuaState()) {
                Assert.AreEqual(lua.StackTop, 0);
                lua.Push("Hello, world!");
                Assert.AreEqual(lua.StackTop, 1);
                var str = lua.ToString();
                Assert.AreEqual("Hello, world!", str);
                Assert.AreEqual(lua.StackTop, 1);
                lua.Pop();
                Assert.AreEqual(lua.StackTop, 0);
            }
        }

        [Test]
        public void PushesAndGetsStringsWithNulls() {
            using (var lua = new LuaState()) {
                Assert.AreEqual(lua.StackTop, 0);
                lua.Push("Hello,\0world!");
                Assert.AreEqual(lua.StackTop, 1);
                var str = lua.ToString();
                Assert.AreEqual("Hello,\0world!", str);
                Assert.AreEqual(lua.StackTop, 1);
                lua.Pop();
                Assert.AreEqual(lua.StackTop, 0);
            }
        }

        [Test]
        public void LoadsStringAndGetsResult() {
            using (var lua = new LuaState()) {
                var top = lua.StackTop;
                lua.LoadString("return 'hi'");
                lua.ProtCall(0);
                var result = lua.ToString();
                Assert.AreEqual("hi", result);
                Assert.AreEqual(top + 1, lua.StackTop);
            }
        }

        [Test]
        public void ToStringTreatsStringsAndNumbersTheSame() {
            using (var lua = new LuaState()) {
                var top = lua.StackTop;
                lua.LoadString("return 3");
                lua.ProtCall(0);
                var result = lua.ToString();
                Assert.AreEqual("3", result);
                Assert.AreEqual(top + 1, lua.StackTop);
            }
        }

        [Test]
        public void StackStack() {
            using (var lua = new LuaState()) {
                lua.EnterArea();
                lua.Push("test");

                lua.EnterArea();
                lua.Push("welp");
                lua.LeaveAreaCleanup();

                lua.Pop();
                lua.LeaveArea();

                lua.EnterArea();
                lua.DoString("return 'hi'");
                lua.Pop();
                lua.LeaveArea();
            }
        }

        public class Test {
            public string TestString;

            public Test(string test) {
                TestString = test;
            }
        }

        [Test]
        public void CLRObjectBackAndForth() {
            using (var lua = new LuaState()) {
                lua.EnterArea();
                var t = new Test("This is a test");
                lua.PushCLR(t);
                var o = lua.ToCLR<Test>();
                Assert.NotNull(o);
                Assert.AreEqual("This is a test", o.TestString);
                lua.Pop();
                lua.LeaveArea();
            }
        }

        [Test]
        public void RefCleanup() {
            using (var lua = new LuaState()) {
                var t = new Test("This is a test");
                var refidx = lua.PushCLR(t);
                lua.Pop(); // put the object in a position where it can be collected
                lua.GCCollect(); // force gc collection
                Assert.IsNull(LuaState.Refs.GetRef(refidx));
            }
        }

        [Test]
        public void LuaRefTest() {
            using (var lua = new LuaState()) {
                lua.EnterArea();
                lua.Push("Hello, world!");
                int refidx = lua.MakeLuaReference();
                lua.Pop();
                lua.LeaveArea();

                lua.EnterArea();
                lua.PushLuaReference(refidx);
                var value = lua.ToString();
                Assert.AreEqual("Hello, world!", value);
                lua.Pop();
                lua.DeleteLuaReference(refidx);
                lua.LeaveArea();

                lua.EnterArea();
                lua.Push("Reuse");
                refidx = lua.MakeLuaReference();
                lua.Pop();
                lua.LeaveArea();

                lua.EnterArea();
                lua.PushLuaReference(refidx);
                value = lua.ToString();
                Assert.AreEqual("Reuse", value);
                lua.Pop();
                lua.DeleteLuaReference(refidx);
                lua.LeaveArea();
            }
        }

        [Test]
        public void LuaToCLRConversions() {
            using (var lua = new LuaState()) {
                lua.EnterArea();
                lua.DoString("a = 'hello'");
                lua.GetGlobal("a");
                var a = lua.ConvertToCLR();
                Assert.NotNull(a);
                Assert.AreEqual("hello", a);
                lua.Pop();
                lua.LeaveArea();

                lua.EnterArea();
                lua.DoString("b = {'hello', 'world'}");
                lua.GetGlobal("b");
                var b = lua.ConvertToCLR() as string[];
                Assert.NotNull(b);
                Assert.AreEqual(2, b.Length);
                Assert.AreEqual("hello", b[0]);
                Assert.AreEqual("world", b[1]);
                lua.Pop();
                lua.LeaveArea();

                lua.EnterArea();
                lua.DoString("c = {1, 2, 3, 4, 5}");
                lua.GetGlobal("c");
                var c_obj = lua.ConvertToCLR();
                Assert.IsInstanceOf(typeof(int[]), c_obj);
                var c = c_obj as int[];
                Assert.NotNull(c);
                Assert.AreEqual(5, c.Length);
                Assert.AreEqual(1, c[0]);
                Assert.AreEqual(2, c[1]);
                Assert.AreEqual(3, c[2]);
                Assert.AreEqual(4, c[3]);
                Assert.AreEqual(5, c[4]);
                lua.Pop();
                lua.LeaveArea();
            }
        }

        [Test]
        public void LuaCLRMethods() {
            using (var lua = new LuaState()) {
                lua.EnterArea();
                lua.PushLuaCLRMethod(
                    typeof(Helper),
                    "Test",
                    BindingFlags.Static | BindingFlags.Public
                );
                lua.SetGlobal("test");
                lua.LoadString("return test()");
                lua.ProtCall(0, results: 1);
                var val = lua.ToCLR();
                Assert.AreEqual(Helper.Test(), val);
                lua.Pop();
                lua.LeaveArea();

                var helper = new Helper();

                lua.EnterArea();
                lua.PushCLR(helper);
                lua.SetGlobal("helper");
                lua.LeaveArea();

                lua.EnterArea();
                lua.PushLuaCLRMethod(
                    typeof(Helper),
                    "Test",
                    BindingFlags.Instance | BindingFlags.Public,
                    helper
                );
                lua.SetGlobal("test2");
                lua.LoadString("return test2(helper, 'zath')");
                lua.ProtCall(0, results: 1);
                var val2 = lua.ToCLR();
                Assert.AreEqual(helper.Test("zath"), val2);
                lua.Pop();
                lua.LeaveArea();

                lua.EnterArea();
                lua.PushLuaCLRMethod(
                    typeof(Helper),
                    "TestCLR",
                    BindingFlags.Instance | BindingFlags.Public,
                    helper
                );
                lua.SetGlobal("test3");
                lua.LoadString("return test3(helper)");
                lua.ProtCall(0, results: 1);
                var val3 = lua.ToCLR();
                Assert.AreEqual(helper.TestCLR(), val3);
                lua.Pop();
                lua.LeaveArea();
            }
        }

        [Test]
        public void LuaCLRMethodOverloads() {
            using (var lua = new LuaState()) {
                lua.EnterArea();
                lua.PushLuaCLRMethod(
                    typeof(Helper),
                    "Overload",
                    BindingFlags.Static | BindingFlags.Public
                );
                lua.SetGlobal("overload");

                lua.LoadString("return overload('test')");
                lua.ProtCall(0, results: 1);
                var val1 = lua.ToCLR();
                lua.Pop();
                Assert.AreEqual("test", val1);

                lua.LoadString("return overload(42)");
                lua.ProtCall(0, results: 1);
                var val2 = lua.ToCLR();
                lua.Pop();
                Assert.AreEqual(42, val2);
                lua.LeaveArea();
            }
        }

        [Test]
        public void LuaCLRObjectGet() {
            using (var lua = new LuaState()) {
                lua.EnterArea();
                var helper = new Helper();
                lua.PushCLR(helper);
                lua.SetGlobal("test");

                lua.LoadString("return test.TestField");
                lua.ProtCall(0);
                var test_field = lua.ToCLR();
                Assert.AreEqual(helper.TestField, test_field);
                lua.Pop();


                lua.LoadString("return test.TestProp");
                lua.ProtCall(0);
                var test_prop = lua.ToCLR();
                Assert.AreEqual(helper.TestProp, test_prop);
                lua.Pop();

                lua.LoadString("return test:Test('zath')");
                lua.ProtCall(0);
                var test_result = lua.ToCLR();
                Assert.AreEqual(helper.Test("zath"), test_result);
                lua.Pop();
                lua.LeaveArea();
            }
        }

        [Test]
        public void LuaCLRObjectSet() {
            using (var lua = new LuaState()) {
                lua.EnterArea();
                var helper = new Helper();
                lua.PushCLR(helper);
                lua.SetGlobal("test");

                lua.LoadString("test.TestField = 'hacked'");
                lua.VoidProtCall(0);
                Assert.AreEqual("hacked", helper.TestField);

                lua.LoadString("test.TestProp = 'hacked'");
                lua.VoidProtCall(0);
                Assert.AreEqual("hacked", helper.TestProp);
                lua.LeaveArea();
            }
        }

        [Test]
        public void LuaCLRExceptions() {
            using (var lua = new LuaState()) {
                lua.EnterArea();
                var helper = new Helper();
                lua.PushCLR(helper);
                lua.SetGlobal("test");

                lua.LoadString("test:CauseException()");
                if (lua.VoidProtCall(0) != LuaResult.OK) {
                    var top = lua.StackTop;
                    var error = lua.ToCLR() as Exception;
                    Assert.NotNull(error);
                    Assert.IsInstanceOf(typeof(TargetInvocationException), error);
                    Assert.NotNull(error.InnerException);
                    Assert.IsInstanceOf(typeof(InvalidOperationException), error.InnerException);
                    Assert.AreEqual("Hello", error.InnerException.Message);
                    lua.Pop();
                }
                lua.LeaveArea();
            }
        }

        // This is a LuaCLRFunction
        // It allows you to interact with the stack directly
        // similarly to lua_CFunction, *but* it comes with
        // built in proper error handling (which means you
        // can safely throw inside here)
        public static int TestFunction(LuaState lua) {
            lua.CheckArg(LuaType.String, 1);
            lua.CheckArg(LuaType.Number, 2);
            lua.CheckArg(LuaType.Boolean, 3);

            var arg1 = lua.ToString(1);
            var arg2 = lua.ToLong(2);
            var arg3 = lua.ToBool(3);

            if (arg1 != "hello" || arg2 != 1000 || arg3 != false) {
                throw new InvalidOperationException("Wrong password!");
            }

            lua.PushString("Hello, world!");
            return 1;
        }

        [Test]
        public void LuaCLRFunctionUsage() {
            using (var lua = new LuaState()) {
                lua.EnterArea();

                lua.PushLuaCLRFunction(TestFunction);
                lua.SetGlobal("test");

                lua.BeginProtCall();
                lua.LoadString("return test('not hello', 0, true)");
                try {
                    lua.ExecProtCall(0);
                } catch (Exception ex) {
                    Assert.NotNull(ex);
                    Assert.IsInstanceOf(typeof(LuaException), ex);
                    Assert.NotNull(ex.InnerException);
                    Assert.IsInstanceOf(typeof(InvalidOperationException), ex.InnerException);
                    Assert.AreEqual("Wrong password!", ex.InnerException.Message);
                }

                lua.BeginProtCall();
                lua.LoadString("return test('hello', 1000, false)");
                lua.ExecProtCall(0);

                var obj = lua.ToCLR();
                lua.Pop();

                Assert.AreEqual("Hello, world!", obj);

                lua.LeaveArea();
            }
        }

        [Test]
        public void CLRLibrary() {
            using (var lua = new LuaState()) {
                lua.EnterArea();
                lua.LoadInteropLibrary();

                lua.LoadString("return interop.assembly('MicroLua.Tests')");
                if (lua.ProtCall(0) != LuaResult.OK) {
                    Assert.Fail(lua.ToCLR().ToString());
                }
                var ass = lua.ToCLR<Assembly>();
                lua.Pop();

                Assert.AreEqual(Assembly.Load("MicroLua.Tests"), ass);
                lua.Push(ass);
                lua.SetGlobal("microlua_tests");

                lua.LoadString("return interop.type(microlua_tests, 'MicroLua.Tests.Helper')");
                if (lua.ProtCall(0) != LuaResult.OK) {
                    Assert.Fail(lua.ToCLR().ToString());
                }
                var type = lua.ToCLR<Type>();
                lua.Pop();

                Assert.AreEqual(typeof(Helper), type);

                lua.LeaveArea();
            }
        }

        [Test]
        public void Traceback() {
            using (var lua = new LuaState()) {
                lua.EnterArea();

                var helper = new Helper();
                lua.PushCLR(helper);
                lua.SetGlobal("test");

                lua.BeginProtCall();
                lua.BeginProtCall();
                lua.LoadString(@"
                    function testf()
                        test:CauseException()
                    end
                    return testf
                ");
                lua.ExecProtCall(0);
                try { lua.ExecProtCall(0); }
                catch (LuaException ex) {
                    Assert.NotNull(ex);
                    Assert.NotNull(ex.InnerException);
                    Assert.NotNull(ex.InnerException.InnerException);
                    Assert.IsInstanceOf(typeof(TargetInvocationException), ex.InnerException);
                    Assert.IsInstanceOf(typeof(InvalidOperationException), ex.InnerException.InnerException);
                    Assert.AreEqual("Hello", ex.InnerException.InnerException.Message);

                    Assert.AreEqual("[C]: ?", ex.TracebackArray[0]);
                    Assert.AreEqual("[C]: in function '_error'", ex.TracebackArray[1]);
                    Assert.AreEqual("[string \"MicroLua\"]:25: in function <[string \"MicroLua\"]:21>", ex.TracebackArray[2]);
                    Assert.AreEqual("(tail call): ?", ex.TracebackArray[3]);
                    Assert.AreEqual("[string \"...\"]:3: in function <[string \"...\"]:2>", ex.TracebackArray[4]);
                }

                lua.LeaveArea();
            }
        }

        [Test]
        public void Environments() {
            using (var lua = new LuaState()) {
                lua.EnterArea();

                lua.BeginProtCall();
                lua.LoadString("return hello");
                var func_ref = lua.MakeLuaReference();

                lua.PushNewTable();
                lua.PushString("Hello, world!");
                lua.SetField("hello");
                var env_ref = lua.MakeLuaReference();

                lua.SetEnvironment();

                lua.ExecProtCall(0);
                var val = lua.ToCLR();
                lua.Pop();
                Assert.AreEqual("Hello, world!", val);

                lua.PushLuaReference(func_ref);
                lua.GetEnvironment();
                lua.PushLuaReference(env_ref);
                Assert.IsTrue(lua.AreEqual(-1, -2));
                lua.Pop(3);

                lua.LeaveArea();
            }
        }

        [Test]
        public void StaticType() {
            using (var lua = new LuaState()) {
                lua.EnterArea();

                lua.PushType(typeof(Helper));
                lua.SetGlobal("Helper");

                lua.BeginProtCall();
                lua.LoadString("return Helper.Overload('hello')");
                lua.ExecProtCall(0);
                Assert.AreEqual("hello", lua.ToString());
                lua.Pop();

                lua.LoadString("return Helper.StaticTestField");
                lua.ProtCall(0);
                var test_field = lua.ToCLR();
                Assert.AreEqual(Helper.StaticTestField, test_field);
                lua.Pop();

                lua.LoadString("return Helper.StaticTestProp");
                lua.ProtCall(0);
                var test_prop = lua.ToCLR();
                Assert.AreEqual(Helper.StaticTestProp, test_prop);
                lua.Pop();

                lua.LoadString("Helper.StaticTestField = 'hacked'");
                lua.VoidProtCall(0);
                Assert.AreEqual("hacked", Helper.StaticTestField);

                lua.LoadString("Helper.StaticTestProp = 'hacked'");
                lua.VoidProtCall(0);
                Assert.AreEqual("hacked", Helper.StaticTestProp);

                lua.LoadString("return Helper('ctor').TestField");
                lua.ProtCall(0);
                var val = lua.ToCLR();
                Assert.AreEqual("ctor", val);
                lua.Pop();

                lua.LoadString("return Helper(42).TestField");
                lua.ProtCall(0);
                var val2 = lua.ToCLR();
                Assert.AreEqual("42", val2);
                lua.Pop();

                lua.LeaveArea();
            }
        }
    }
}
