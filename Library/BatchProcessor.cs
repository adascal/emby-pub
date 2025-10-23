using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Logging;

namespace Kinopub.Plugin.Library
{
    /// <summary>
    /// Processes items in parallel batches with semaphore control
    /// </summary>
    public class BatchProcessor<T>
    {
        private readonly ILogger _logger;
        private readonly int _batchSize;
        private readonly int _maxConcurrentBatches;
        private readonly SemaphoreSlim _semaphore;

        public BatchProcessor(ILogger logger, int batchSize = 50, int maxConcurrentBatches = 4)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _batchSize = batchSize > 0 ? batchSize : throw new ArgumentException("Batch size must be positive", nameof(batchSize));
            _maxConcurrentBatches = maxConcurrentBatches > 0 ? maxConcurrentBatches : throw new ArgumentException("Max concurrent batches must be positive", nameof(maxConcurrentBatches));
            _semaphore = new SemaphoreSlim(_maxConcurrentBatches, _maxConcurrentBatches);
        }

        /// <summary>
        /// Processes items in batches with parallel execution
        /// </summary>
        public async Task<BatchProcessingResult> ProcessBatchesAsync(
            IEnumerable<T> items,
            Func<T, CancellationToken, Task<bool>> processFunc,
            CancellationToken cancellationToken = default,
            IProgress<BatchProgress>? progress = null)
        {
            var result = new BatchProcessingResult();
            var startTime = DateTime.UtcNow;
            
            var itemsList = items.ToList();
            var totalItems = itemsList.Count;
            
            if (totalItems == 0)
            {
                return result;
            }

            _logger.Info($"Starting batch processing: {totalItems} items, batch size {_batchSize}, max concurrent {_maxConcurrentBatches}");

            var batches = CreateBatches(itemsList);
            var totalBatches = batches.Count;
            var processedBatches = 0;
            var processedItems = 0;

            var batchTasks = batches.Select(async (batch, batchIndex) =>
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    _logger.Debug($"Processing batch {batchIndex + 1}/{totalBatches} ({batch.Count} items)");

                    var batchResult = await ProcessBatchAsync(batch, processFunc, cancellationToken);

                    result.TotalProcessed += batchResult.Processed;
                    result.TotalSuccessful += batchResult.Successful;
                    result.TotalFailed += batchResult.Failed;
                    result.Errors.AddRange(batchResult.Errors);

                    Interlocked.Increment(ref processedBatches);
                    Interlocked.Add(ref processedItems, batch.Count);

                    if (progress != null)
                    {
                        var batchProgress = new BatchProgress
                        {
                            TotalItems = totalItems,
                            ProcessedItems = processedItems,
                            TotalBatches = totalBatches,
                            ProcessedBatches = processedBatches,
                            CurrentBatchSize = batch.Count,
                            PercentComplete = (double)processedItems / totalItems * 100.0
                        };
                        progress.Report(batchProgress);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Batch {batchIndex + 1} failed: {ex.Message}");
                    result.Errors.Add($"Batch {batchIndex + 1}: {ex.Message}");
                }
                finally
                {
                    _semaphore.Release();
                }
            });

            await Task.WhenAll(batchTasks);

            result.Duration = DateTime.UtcNow - startTime;
            
            _logger.Info($"Batch processing complete: {result.TotalSuccessful}/{result.TotalProcessed} successful, {result.TotalFailed} failed, took {result.Duration.TotalSeconds:F2}s");

            return result;
        }

        /// <summary>
        /// Processes a single batch sequentially
        /// </summary>
        private async Task<BatchResult> ProcessBatchAsync(
            List<T> batch,
            Func<T, CancellationToken, Task<bool>> processFunc,
            CancellationToken cancellationToken)
        {
            var result = new BatchResult();

            foreach (var item in batch)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var success = await processFunc(item, cancellationToken);
                    result.Processed++;
                    
                    if (success)
                    {
                        result.Successful++;
                    }
                    else
                    {
                        result.Failed++;
                    }
                }
                catch (Exception ex)
                {
                    result.Failed++;
                    result.Errors.Add($"Item processing error: {ex.Message}");
                    _logger.Error($"Item processing error: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// Creates batches from items
        /// </summary>
        private List<List<T>> CreateBatches(List<T> items)
        {
            var batches = new List<List<T>>();
            
            for (int i = 0; i < items.Count; i += _batchSize)
            {
                var batch = items.Skip(i).Take(_batchSize).ToList();
                batches.Add(batch);
            }

            return batches;
        }

        /// <summary>
        /// Processes items in parallel with individual semaphore control (alternative approach)
        /// </summary>
        public async Task<BatchProcessingResult> ProcessParallelAsync(
            IEnumerable<T> items,
            Func<T, CancellationToken, Task<bool>> processFunc,
            CancellationToken cancellationToken = default,
            IProgress<double>? progress = null)
        {
            var result = new BatchProcessingResult();
            var startTime = DateTime.UtcNow;

            var itemsList = items.ToList();
            var totalItems = itemsList.Count;
            var processedCount = 0;
            var totalProcessed = 0;
            var totalSuccessful = 0;
            var totalFailed = 0;

            if (totalItems == 0)
            {
                return result;
            }

            _logger.Info($"Starting parallel processing: {totalItems} items, max concurrent {_maxConcurrentBatches}");

            var tasks = itemsList.Select(async item =>
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    var success = await processFunc(item, cancellationToken);

                    Interlocked.Increment(ref totalProcessed);

                    if (success)
                    {
                        Interlocked.Increment(ref totalSuccessful);
                    }
                    else
                    {
                        Interlocked.Increment(ref totalFailed);
                    }

                    var currentCount = Interlocked.Increment(ref processedCount);
                    progress?.Report((double)currentCount / totalItems * 100.0);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref totalFailed);
                    result.Errors.Add($"Item processing error: {ex.Message}");
                    _logger.Error($"Item processing error: {ex.Message}");
                }
                finally
                {
                    _semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            result.TotalProcessed = totalProcessed;
            result.TotalSuccessful = totalSuccessful;
            result.TotalFailed = totalFailed;
            result.Duration = DateTime.UtcNow - startTime;

            _logger.Info($"Parallel processing complete: {result.TotalSuccessful}/{result.TotalProcessed} successful, {result.TotalFailed} failed, took {result.Duration.TotalSeconds:F2}s");

            return result;
        }

        /// <summary>
        /// Disposes the semaphore
        /// </summary>
        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }

    /// <summary>
    /// Result of batch processing a single batch
    /// </summary>
    internal class BatchResult
    {
        public int Processed { get; set; }
        public int Successful { get; set; }
        public int Failed { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Overall result of batch processing
    /// </summary>
    public class BatchProcessingResult
    {
        public int TotalProcessed { get; set; }
        public int TotalSuccessful { get; set; }
        public int TotalFailed { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public TimeSpan Duration { get; set; }
        public bool IsSuccess => TotalFailed == 0;
    }

    /// <summary>
    /// Progress information for batch processing
    /// </summary>
    public class BatchProgress
    {
        public int TotalItems { get; set; }
        public int ProcessedItems { get; set; }
        public int TotalBatches { get; set; }
        public int ProcessedBatches { get; set; }
        public int CurrentBatchSize { get; set; }
        public double PercentComplete { get; set; }
    }
}
