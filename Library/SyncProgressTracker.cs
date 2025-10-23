using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Kinopub.Plugin.Library
{
    /// <summary>
    /// Tracks real-time sync progress for reporting
    /// </summary>
    public class SyncProgressTracker
    {
        private readonly Stopwatch _stopwatch;
        private long _totalItems;
        private long _processedItems;
        private long _successfulItems;
        private long _failedItems;
        private long _skippedItems;
        
        private readonly ConcurrentDictionary<string, ItemProgress> _itemProgress;
        private readonly ConcurrentBag<string> _errors;
        
        public SyncProgressTracker()
        {
            _stopwatch = new Stopwatch();
            _itemProgress = new ConcurrentDictionary<string, ItemProgress>();
            _errors = new ConcurrentBag<string>();
        }

        /// <summary>
        /// Starts tracking progress
        /// </summary>
        public void Start(long totalItems)
        {
            _totalItems = totalItems;
            _processedItems = 0;
            _successfulItems = 0;
            _failedItems = 0;
            _skippedItems = 0;
            _itemProgress.Clear();
            _errors.Clear();
            _stopwatch.Restart();
        }

        /// <summary>
        /// Records an item as started
        /// </summary>
        public void RecordItemStarted(string itemId, string itemTitle)
        {
            var progress = new ItemProgress
            {
                ItemId = itemId,
                ItemTitle = itemTitle,
                StartTime = DateTime.UtcNow,
                Status = ItemStatus.InProgress
            };
            _itemProgress[itemId] = progress;
        }

        /// <summary>
        /// Records an item as successful
        /// </summary>
        public void RecordItemSuccess(string itemId)
        {
            if (_itemProgress.TryGetValue(itemId, out var progress))
            {
                progress.EndTime = DateTime.UtcNow;
                progress.Status = ItemStatus.Success;
                progress.Duration = progress.EndTime.Value - progress.StartTime;
            }
            
            Interlocked.Increment(ref _processedItems);
            Interlocked.Increment(ref _successfulItems);
        }

        /// <summary>
        /// Records an item as failed
        /// </summary>
        public void RecordItemFailure(string itemId, string errorMessage)
        {
            if (_itemProgress.TryGetValue(itemId, out var progress))
            {
                progress.EndTime = DateTime.UtcNow;
                progress.Status = ItemStatus.Failed;
                progress.ErrorMessage = errorMessage;
                progress.Duration = progress.EndTime.Value - progress.StartTime;
            }
            
            _errors.Add($"{itemId}: {errorMessage}");
            Interlocked.Increment(ref _processedItems);
            Interlocked.Increment(ref _failedItems);
        }

        /// <summary>
        /// Records an item as skipped
        /// </summary>
        public void RecordItemSkipped(string itemId, string reason)
        {
            if (_itemProgress.TryGetValue(itemId, out var progress))
            {
                progress.EndTime = DateTime.UtcNow;
                progress.Status = ItemStatus.Skipped;
                progress.ErrorMessage = reason;
                progress.Duration = progress.EndTime.Value - progress.StartTime;
            }
            
            Interlocked.Increment(ref _processedItems);
            Interlocked.Increment(ref _skippedItems);
        }

        /// <summary>
        /// Gets current progress percentage (0-100)
        /// </summary>
        public double GetProgressPercentage()
        {
            if (_totalItems == 0) return 0;
            return (double)_processedItems / _totalItems * 100.0;
        }

        /// <summary>
        /// Gets current status summary
        /// </summary>
        public ProgressStatus GetStatus()
        {
            var elapsed = _stopwatch.Elapsed;
            var processed = Interlocked.Read(ref _processedItems);
            var remaining = _totalItems - processed;
            
            var itemsPerSecond = elapsed.TotalSeconds > 0 
                ? processed / elapsed.TotalSeconds 
                : 0;
            
            var estimatedTimeRemaining = itemsPerSecond > 0 
                ? TimeSpan.FromSeconds(remaining / itemsPerSecond) 
                : TimeSpan.Zero;

            return new ProgressStatus
            {
                TotalItems = _totalItems,
                ProcessedItems = processed,
                SuccessfulItems = Interlocked.Read(ref _successfulItems),
                FailedItems = Interlocked.Read(ref _failedItems),
                SkippedItems = Interlocked.Read(ref _skippedItems),
                RemainingItems = remaining,
                ProgressPercentage = GetProgressPercentage(),
                ElapsedTime = elapsed,
                EstimatedTimeRemaining = estimatedTimeRemaining,
                ItemsPerSecond = itemsPerSecond,
                CurrentErrors = _errors.ToList()
            };
        }

        /// <summary>
        /// Gets detailed progress for all items
        /// </summary>
        public ItemProgressReport GetDetailedProgress()
        {
            var items = _itemProgress.Values.ToList();
            
            return new ItemProgressReport
            {
                TotalItems = _totalItems,
                InProgressItems = items.Count(i => i.Status == ItemStatus.InProgress),
                CompletedItems = items.Count(i => i.Status == ItemStatus.Success),
                FailedItems = items.Count(i => i.Status == ItemStatus.Failed),
                SkippedItems = items.Count(i => i.Status == ItemStatus.Skipped),
                Items = items,
                AverageDuration = items
                    .Where(i => i.Duration.HasValue)
                    .Select(i => i.Duration!.Value.TotalSeconds)
                    .DefaultIfEmpty(0)
                    .Average()
            };
        }

        /// <summary>
        /// Gets items currently in progress
        /// </summary>
        public ItemProgress[] GetItemsInProgress()
        {
            return _itemProgress.Values
                .Where(i => i.Status == ItemStatus.InProgress)
                .OrderBy(i => i.StartTime)
                .ToArray();
        }

        /// <summary>
        /// Gets recently completed items (last 10)
        /// </summary>
        public ItemProgress[] GetRecentlyCompleted()
        {
            return _itemProgress.Values
                .Where(i => i.Status == ItemStatus.Success || i.Status == ItemStatus.Failed)
                .OrderByDescending(i => i.EndTime)
                .Take(10)
                .ToArray();
        }

        /// <summary>
        /// Stops tracking and returns final statistics
        /// </summary>
        public ProgressStatus Stop()
        {
            _stopwatch.Stop();
            return GetStatus();
        }

        /// <summary>
        /// Resets all progress tracking
        /// </summary>
        public void Reset()
        {
            _stopwatch.Reset();
            _totalItems = 0;
            _processedItems = 0;
            _successfulItems = 0;
            _failedItems = 0;
            _skippedItems = 0;
            _itemProgress.Clear();
            _errors.Clear();
        }
    }

    /// <summary>
    /// Status of a single item
    /// </summary>
    public class ItemProgress
    {
        public string ItemId { get; set; } = string.Empty;
        public string ItemTitle { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan? Duration { get; set; }
        public ItemStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Overall progress status
    /// </summary>
    public class ProgressStatus
    {
        public long TotalItems { get; set; }
        public long ProcessedItems { get; set; }
        public long SuccessfulItems { get; set; }
        public long FailedItems { get; set; }
        public long SkippedItems { get; set; }
        public long RemainingItems { get; set; }
        public double ProgressPercentage { get; set; }
        public TimeSpan ElapsedTime { get; set; }
        public TimeSpan EstimatedTimeRemaining { get; set; }
        public double ItemsPerSecond { get; set; }
        public System.Collections.Generic.List<string> CurrentErrors { get; set; } = new();
    }

    /// <summary>
    /// Detailed progress report
    /// </summary>
    public class ItemProgressReport
    {
        public long TotalItems { get; set; }
        public int InProgressItems { get; set; }
        public int CompletedItems { get; set; }
        public int FailedItems { get; set; }
        public int SkippedItems { get; set; }
        public double AverageDuration { get; set; }
        public System.Collections.Generic.List<ItemProgress> Items { get; set; } = new();
    }

    /// <summary>
    /// Item processing status
    /// </summary>
    public enum ItemStatus
    {
        Pending,
        InProgress,
        Success,
        Failed,
        Skipped
    }
}
