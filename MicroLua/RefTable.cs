using System;
using System.Collections.Generic;

namespace MicroLua {
    public class RefTable {
        private Dictionary<int, object> _References = new Dictionary<int, object>();
        private int _RefCount = 0;

        private const int CHECK_POINT = 11;

        public int Count {
            get { return _RefCount; }
        }

        private int _Check() {
            for (int i = 0; i < _RefCount; i++) {
                if (_References[i] == null) return i;
            }
            return -1;
        }

        public int AddRef(object o) {
            int idx;

            if (_RefCount % CHECK_POINT == 0 && _RefCount > 0) {
                idx = _Check();

                if (idx != -1) {
                    _References[idx] = o;
                    return idx;
                }
            }

            idx = _RefCount;
            _References[_RefCount] = o;
            _RefCount++;
            return idx;
        }

        public void DelRef(int idx) {
            _References[idx] = null;
        }

        public object GetRef(int idx) {
            object o;
            if (_References.TryGetValue(idx, out o)) {
                return o;
            }
            return null;
        }

        public T GetRef<T>(int idx) where T : class {
            return GetRef(idx) as T;
        }
    }
}
