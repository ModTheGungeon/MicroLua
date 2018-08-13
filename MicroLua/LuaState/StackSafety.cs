using System;
using System.Collections.Generic;

namespace MicroLua {
    public partial class LuaState {
        ////////////////////////
        //*** STACK SAFETY ***//
        ////////////////////////
        internal List<int> StackTopStack = new List<int>();

        public void EnterArea() {
            StackTopStack.Add(StackTop);
        }

        public void LeaveAreaCleanup() {
            if (StackTopStack.Count == 0) {
                throw new MicroLuaException("Can't pop stack because the stack stack is empty.");
            }
            var top = StackTopStack[StackTopStack.Count - 1];
            StackTopStack.RemoveAt(StackTopStack.Count - 1);
            StackTop = top;
        }

        public void LeaveArea() {
            if (StackTopStack.Count == 0) {
                throw new MicroLuaException("Can't pop stack because the stack stack is empty.");
            }
            var top = StackTopStack[StackTopStack.Count - 1];
            StackTopStack.RemoveAt(StackTopStack.Count - 1);
            if (top != StackTop) {
                throw new MicroLuaException("Stack wasn't clean");
            }
        }
    }
}
