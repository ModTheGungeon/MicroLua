using System;
using System.Collections.Generic;
using System.Text;

namespace MicroLua {
    public class MicroLuaException : Exception {
        public MicroLuaException(string msg) : base(msg) {}
    }

    public class LuaException : Exception {
        public LuaException(string msg) : base(msg) { }
        public LuaException(string msg, string[] traceback, object value = null, Exception inner = null) : base(msg, inner) {
            TracebackArray = traceback;
            Value = value;
        }

        private string _CachedTraceback;
        public string Traceback {
            get {
                if (_CachedTraceback != null) return _CachedTraceback;
                return _CachedTraceback = string.Join("\n", TracebackArray);
            }
        }

        public string[] TracebackArray { get; private set; }
        public object Value { get; private set; }

        private string _StackTraceOverride;
        public override string StackTrace {
            get {
                var s = new StringBuilder();
                for (int i = 0; i < TracebackArray.Length; i++) {
                    s.Append("    ").AppendLine(TracebackArray[i]);
                }
                s.AppendLine();
                var stacktrace = _StackTraceOverride ?? base.StackTrace;
                if (stacktrace != null) {
                    var stacktrace_ary = stacktrace.Split('\n');
                    for (int i = 0; i < stacktrace_ary.Length; i++) {
                        s.Append("  ").Append(stacktrace_ary[i]);
                        if (i != stacktrace_ary.Length - 1) s.AppendLine();
                    }
                }
                return s.ToString();
            }
        }

        internal string StackTraceOverride {
            set {
                _StackTraceOverride = value;
            }
            get {
                return _StackTraceOverride;
            }
        }

        internal string OriginalStackTrace {
            get { return _StackTraceOverride ?? base.StackTrace; }
        }

        public override string ToString() {
            var s = new StringBuilder();
            var exceptions = new List<Exception>();
            Exception ex = this;
            while (ex != null) {
                exceptions.Add(ex);
                ex = ex.InnerException;
            }

            s.Append(GetType().Name).Append(": ").AppendLine(Message);

            for (int i = 0; i < exceptions.Count; i++) {
                var curex = exceptions[i];
                s.Append("  ----> ").Append(curex.GetType().Name);
                s.Append(": ").AppendLine(curex.Message);
            }

            s.AppendLine();
            s.AppendLine("-- Lua traceback");

            for (int i = 0; i < TracebackArray.Length; i++) {
                s.Append("  ").AppendLine(TracebackArray[i]);
            }

            for (int i = 0; i < exceptions.Count; i++) {
                var curex = exceptions[i];
                s.AppendLine();
                s.Append("-- ").AppendLine(curex.GetType().Name);
                if (curex is LuaException) {
                    s.AppendLine(((LuaException)curex).OriginalStackTrace);
                } else {
                    s.AppendLine(curex.StackTrace);
                }
            }

            return s.ToString();
        }
    }
}
