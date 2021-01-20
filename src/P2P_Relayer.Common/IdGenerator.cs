using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace P2P_Relayer.Common
{
    public class IdGenerator
    {
        private int _counter;
        private readonly ConcurrentQueue<int> _freeIds = new ConcurrentQueue<int>();

        public int Get()
        {
            if (_freeIds.TryDequeue(out int id))
                return id;
            return Interlocked.Increment(ref _counter);
        }

        public void Free(int id)
        {
            _freeIds.Enqueue(id);
        }
    }
}
