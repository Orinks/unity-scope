using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace UnityScope.Server
{
    // Marshals work from background transport threads onto Unity's main thread.
    // Every Unity API call MUST go through this — Unity is not thread-safe.
    internal class MainThreadDispatcher : MonoBehaviour
    {
        private readonly ConcurrentQueue<WorkItem> _queue = new ConcurrentQueue<WorkItem>();

        public T Run<T>(Func<T> action, int timeoutMs = 5000)
        {
            var item = new WorkItem { Action = () => action() };
            _queue.Enqueue(item);
            if (!item.Done.WaitOne(timeoutMs))
                throw new TimeoutException("Main-thread dispatcher timed out.");
            if (item.Error != null) throw item.Error;
            return (T)item.Result;
        }

        private void Update()
        {
            while (_queue.TryDequeue(out var item))
            {
                try { item.Result = item.Action(); }
                catch (Exception ex) { item.Error = ex; }
                finally { item.Done.Set(); }
            }
        }

        private class WorkItem
        {
            public Func<object> Action;
            public object Result;
            public Exception Error;
            public ManualResetEvent Done = new ManualResetEvent(false);
        }
    }
}
