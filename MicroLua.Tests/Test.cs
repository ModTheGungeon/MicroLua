using NUnit.Framework;
using System;
using System.Reflection;

namespace MicroLua.Tests {
    public class Helper {
        public static Helper Instance = new Helper();

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
                int refidx = lua.MakeReference();
                lua.Pop();
                lua.LeaveArea();

                lua.EnterArea();
                lua.PushReference(refidx);
                var value = lua.ToString();
                Assert.AreEqual("Hello, world!", value);
                lua.Pop();
                lua.DeleteReference(refidx);
                lua.LeaveArea();

                lua.EnterArea();
                lua.Push("Reuse");
                refidx = lua.MakeReference();
                lua.Pop();
                lua.LeaveArea();

                lua.EnterArea();
                lua.PushReference(refidx);
                value = lua.ToString();
                Assert.AreEqual("Reuse", value);
                lua.Pop();
                lua.DeleteReference(refidx);
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
                lua.PushCLRMethod(
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
                lua.PushCLRMethod(
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
                lua.PushCLRMethod(
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
                lua.PushCLRMethod(
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
                    var error = lua.ToCLR();
                    Console.WriteLine(error);
                    lua.Pop();
                }
                lua.LeaveArea();
            }
        }
    }
}
