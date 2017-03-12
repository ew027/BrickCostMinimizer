using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BrickCostMinimizer {
    /// <summary>
    /// A thread-safe queue.
    /// </summary>
    public class BlockingQueue<T> {
        private readonly Queue<T> _queue = new Queue<T>();
        private readonly object listLock = new object();
        private bool _stopped;
        private int _maxSize;

        public BlockingQueue(int maxSize) {
            _maxSize = maxSize;
        }
        
        public int Count { get { return _queue.Count; } }
        
        /// <summary>
        /// Add an item to the queue. If the queue is at its maximum size the calling thread is blocked until the item can be added.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Enqueue(T item) {
            lock (listLock) {
                while (_queue.Count > _maxSize) {
                    Monitor.Wait(listLock);
                }

                _queue.Enqueue(item);
                Monitor.Pulse(listLock);
            }
            return true;
        }
        
        /// <summary>
        /// Dequeue an item. If the queue is empty, the calling thread is blocked until another item is added
        /// </summary>
        public T Dequeue() {
            if (_stopped && _queue.Count == 0)
                return default(T);
            lock (listLock) {
                if (_stopped && _queue.Count == 0)
                    return default(T);
                while (_queue.Count == 0) {
                    Monitor.Wait(listLock);
                    if (_stopped)
                        return default(T);
                }

                if (_queue.Count == _maxSize) {
                    Monitor.PulseAll(listLock);
                }

                return _queue.Dequeue();
            }
        }

        /// <summary>
        /// Stop any additions or removals from the queue
        /// </summary>
        public void Stop() {
            if (_stopped)
                return;
            lock (listLock) {
                if (_stopped)
                    return;
                _stopped = true;
                Monitor.PulseAll(listLock);
            }
        }

        /// <summary>
        /// Allow items to be added/removed from the queue
        /// </summary>
        public void Start() {
            if (!_stopped)
                return;
            lock (listLock) {
                if (!_stopped)
                    return;
                _stopped = false;
                Monitor.PulseAll(listLock);
            }
        }
    }
}