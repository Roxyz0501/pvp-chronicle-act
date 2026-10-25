using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PvPChronicle
{
    internal sealed class HistoryWriter : IDisposable
    {
        internal const int DefaultCapacity = 4096;
        internal const int DefaultBatchSize = 64;
        private readonly object gate = new object();
        private readonly Queue<HistoryEntry> pending = new Queue<HistoryEntry>();
        private readonly Func<IList<HistoryEntry>, HistoryWriteResult> append;
        private readonly Action<IList<HistoryEntry>> persisted;
        private readonly AutoResetEvent wake = new AutoResetEvent(false);
        private readonly Thread worker;
        private readonly int capacity, batchSize, maximumAttempts;
        private readonly Func<int, int> retryDelay;
        private bool accepting = true, stopping, faulted, disposed, workerStarted;
        private int failures, writeOperations;
        private long bytesWritten;
        private Exception lastError;

        internal HistoryWriter(HistoryStore store, Action<IList<HistoryEntry>> persisted)
            : this(store.AppendBatch, persisted, DefaultCapacity, DefaultBatchSize, 5, attempt => Math.Min(1600, 100 * (1 << (attempt - 1))), true) { }

        internal HistoryWriter(Func<IList<HistoryEntry>, HistoryWriteResult> append, Action<IList<HistoryEntry>> persisted,
            int capacity, int batchSize, int maximumAttempts, Func<int, int> retryDelay, bool startWorker)
        {
            this.append = append ?? throw new ArgumentNullException("append");
            this.persisted = persisted;
            if (capacity < 1 || batchSize < 1 || maximumAttempts < 1) throw new ArgumentOutOfRangeException();
            this.capacity = capacity; this.batchSize = batchSize; this.maximumAttempts = maximumAttempts;
            this.retryDelay = retryDelay ?? throw new ArgumentNullException("retryDelay");
            worker = new Thread(Work) { IsBackground = true, Name = "PvP Chronicle history writer" };
            if (startWorker) Start();
        }

        internal void Start()
        {
            lock (gate)
            {
                if (workerStarted) throw new InvalidOperationException("History writer already started.");
                workerStarted = true; worker.Start();
            }
        }

        internal int PendingCount { get { lock (gate) return pending.Count; } }
        internal int WriteOperations { get { lock (gate) return writeOperations; } }
        internal long BytesWritten { get { lock (gate) return bytesWritten; } }
        internal Exception LastError { get { lock (gate) return lastError; } }
        internal bool IsFaulted { get { lock (gate) return faulted; } }

        internal bool Enqueue(HistoryEntry entry)
        {
            if (entry == null) return false;
            lock (gate)
            {
                if (!accepting || pending.Count >= capacity) return false;
                pending.Enqueue(entry);
            }
            wake.Set();
            return true;
        }

        internal bool StopAndFlush()
        {
            lock (gate)
            {
                if (!workerStarted) { workerStarted = true; worker.Start(); }
                accepting = false;
                if (!stopping)
                {
                    stopping = true;
                    if (faulted) { faulted = false; failures = 0; }
                }
            }
            wake.Set();
            if (worker.IsAlive) worker.Join();
            lock (gate) return pending.Count == 0;
        }

        private void Work()
        {
            while (true)
            {
                List<HistoryEntry> batch;
                lock (gate)
                {
                    if (faulted) { if (stopping) return; batch = null; }
                    else if (pending.Count == 0) { if (stopping) return; batch = null; }
                    else batch = pending.Take(Math.Min(batchSize, pending.Count)).ToList();
                }
                if (batch == null) { wake.WaitOne(); continue; }

                try
                {
                    var result = append(batch);
                    lock (gate)
                    {
                        foreach (var item in batch)
                        {
                            if (pending.Count == 0 || !ReferenceEquals(pending.Peek(), item)) throw new InvalidOperationException("History queue order changed.");
                            pending.Dequeue();
                        }
                        writeOperations += result.WriteOperations; bytesWritten += result.BytesWritten;
                        failures = 0; faulted = false; lastError = null;
                    }
                    try { persisted?.Invoke(batch); } catch { }
                }
                catch (Exception ex)
                {
                    lock (gate)
                    {
                        failures++;
                        lastError = ex;
                        if (failures >= maximumAttempts) faulted = true;
                    }
                    if (!IsFaulted) Thread.Sleep(Math.Max(0, retryDelay(failures)));
                }
            }
        }

        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed = true; }
            StopAndFlush();
            wake.Dispose();
        }
    }
}
