using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TACEUI.Core
{
    public static class DeviceCommandQueue
    {
        public enum EnqueueStatus
        {
            Started,
            Queued,
            Duplicate
        }

        public readonly struct EnqueueResult
        {
            public EnqueueResult(EnqueueStatus status, int position)
            {
                Status = status;
                Position = position;
            }

            public EnqueueStatus Status { get; }
            public int Position { get; }
        }

        private sealed class Entry
        {
            public string Key = string.Empty;
            public Action Action = delegate { };
        }

        private sealed class DeviceQueue
        {
            public readonly Queue<Entry> Pending = new Queue<Entry>();
            public Entry? Current;
            public bool WorkerRunning;
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, DeviceQueue> Devices =
            new Dictionary<string, DeviceQueue>(StringComparer.OrdinalIgnoreCase);

        public static EnqueueResult Enqueue(string serial, string commandKey, Action action)
        {
            if (string.IsNullOrWhiteSpace(serial))
                throw new ArgumentException("Serial obrigatorio.", nameof(serial));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            string key = string.IsNullOrWhiteSpace(commandKey) ? "CMD" : commandKey.Trim();

            lock (Sync)
            {
                if (!Devices.TryGetValue(serial, out DeviceQueue? queue))
                {
                    queue = new DeviceQueue();
                    Devices[serial] = queue;
                }

                if (IsDuplicate(queue, key))
                {
                    return new EnqueueResult(EnqueueStatus.Duplicate, 0);
                }

                queue.Pending.Enqueue(new Entry { Key = key, Action = action });
                int position = queue.Pending.Count + (queue.Current != null ? 1 : 0);

                if (!queue.WorkerRunning)
                {
                    queue.WorkerRunning = true;
                    Task.Run(() => Process(serial, queue));
                }

                return new EnqueueResult(
                    position > 1 ? EnqueueStatus.Queued : EnqueueStatus.Started,
                    position);
            }
        }

        public static int Clear(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return 0;

            lock (Sync)
            {
                if (!Devices.TryGetValue(serial, out DeviceQueue? queue))
                    return 0;

                int removed = queue.Pending.Count;
                queue.Pending.Clear();
                return removed;
            }
        }

        public static int ClearAll()
        {
            lock (Sync)
            {
                int removed = 0;
                foreach (DeviceQueue queue in Devices.Values)
                {
                    removed += queue.Pending.Count;
                    queue.Pending.Clear();
                }
                return removed;
            }
        }

        private static bool IsDuplicate(DeviceQueue queue, string key)
        {
            if (queue.Current != null &&
                string.Equals(queue.Current.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (Entry entry in queue.Pending)
            {
                if (string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static void Process(string serial, DeviceQueue queue)
        {
            while (true)
            {
                Entry entry;

                lock (Sync)
                {
                    if (queue.Pending.Count == 0)
                    {
                        queue.Current = null;
                        queue.WorkerRunning = false;
                        Devices.Remove(serial);
                        return;
                    }

                    entry = queue.Pending.Dequeue();
                    queue.Current = entry;
                }

                try
                {
                    entry.Action();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[DeviceCommandQueue] {serial} {entry.Key}: {ex}");
                }
                finally
                {
                    lock (Sync)
                    {
                        if (ReferenceEquals(queue.Current, entry))
                            queue.Current = null;
                    }
                }
            }
        }
    }
}
