using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Economy_sim
{
    /// <summary>
    /// Manages sparse delta log for edit operations, enabling fast saves and undo/redo
    /// </summary>
    public class EditDeltaManager : IDisposable
    {
        private readonly string _deltaLogPath;
        private readonly LinkedList<EditOperation> _undoStack = new();
        private readonly LinkedList<EditOperation> _redoStack = new();
        private const int MaxUndoOperations = 1000;

        // Serialize file access to delta.log to prevent sharing violations
        private static readonly SemaphoreSlim _fileIoLock = new(1, 1);

        public EditDeltaManager(string editsDirectory)
        {
            Directory.CreateDirectory(editsDirectory);
            _deltaLogPath = Path.Combine(editsDirectory, "delta.log");

            _ = LoadDeltaLogAsync();
        }

        /// <summary>
        /// Records a new edit operation
        /// </summary>
        public void RecordEdit(EditOperation operation)
        {
            _undoStack.AddFirst(operation);

            // Clear redo stack when new edit is made
            _redoStack.Clear();

            // Limit undo stack size
            while (_undoStack.Count > MaxUndoOperations)
            {
                _undoStack.RemoveLast();
            }

            // Persist to disk asynchronously
            _ = SaveDeltaLogAsync();
        }

        /// <summary>
        /// Pops the last edit operation for undo
        /// </summary>
        public EditOperation? PopLastEdit()
        {
            if (_undoStack.Count == 0) return null;

            var operation = _undoStack.First.Value;
            _undoStack.RemoveFirst();
            _redoStack.AddFirst(operation);

            // Persist changes
            _ = SaveDeltaLogAsync();

            return operation;
        }

        /// <summary>
        /// Gets the next operation for redo
        /// </summary>
        public EditOperation? GetNextRedo()
        {
            if (_redoStack.Count == 0) return null;

            var operation = _redoStack.First.Value;
            _redoStack.RemoveFirst();
            _undoStack.AddFirst(operation);

            // Persist changes
            _ = SaveDeltaLogAsync();

            return operation;
        }

        /// <summary>
        /// Saves the current delta log to disk with serialization and atomic replace to avoid sharing violations.
        /// </summary>
        private async Task SaveDeltaLogAsync()
        {
            try
            {
                var deltaData = new DeltaLogData
                {
                    UndoStack = new List<EditOperation>(_undoStack),
                    RedoStack = new List<EditOperation>(_redoStack),
                    Version = 1
                };

                var json = JsonSerializer.Serialize(deltaData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                string tempPath = _deltaLogPath + ".tmp";

                // Ensure only one writer at a time
                await _fileIoLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    // Write to temp file first
                    await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);

                    // Atomically replace/overwrite delta.log
#if NET8_0_OR_GREATER
                    // Prefer File.Move with overwrite on .NET 8+
                    File.Move(tempPath, _deltaLogPath, overwrite: true);
#else
                    if (File.Exists(_deltaLogPath))
                    {
                        File.Delete(_deltaLogPath);
                    }
                    File.Move(tempPath, _deltaLogPath);
#endif
                }
                finally
                {
                    // Clean up temp if something failed mid-way
                    try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                    _fileIoLock.Release();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving delta log: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the delta log from disk with serialized access to avoid conflicts with a concurrent save.
        /// </summary>
        private async Task LoadDeltaLogAsync()
        {
            try
            {
                if (!File.Exists(_deltaLogPath)) return;

                await _fileIoLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    var json = await File.ReadAllTextAsync(_deltaLogPath).ConfigureAwait(false);
                    var deltaData = JsonSerializer.Deserialize<DeltaLogData>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    if (deltaData != null)
                    {
                        _undoStack.Clear();
                        _redoStack.Clear();

                        foreach (var operation in deltaData.UndoStack)
                        {
                            _undoStack.AddLast(operation);
                        }

                        foreach (var operation in deltaData.RedoStack)
                        {
                            _redoStack.AddLast(operation);
                        }
                    }
                }
                finally
                {
                    _fileIoLock.Release();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading delta log: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears all edit history
        /// </summary>
        public void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _ = SaveDeltaLogAsync();
        }

        /// <summary>
        /// Gets the number of operations that can be undone
        /// </summary>
        public int UndoCount => _undoStack.Count;

        /// <summary>
        /// Gets the number of operations that can be redone
        /// </summary>
        public int RedoCount => _redoStack.Count;

        public void Dispose()
        {
            // Final save before disposal
            try
            {
                _fileIoLock.Wait();
                // Synchronously ensure last save completes
                SaveDeltaLogAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing EditDeltaManager: {ex.Message}");
            }
            finally
            {
                if (_fileIoLock.CurrentCount == 0)
                    _fileIoLock.Release();
            }
        }

        /// <summary>
        /// Data structure for serializing delta log
        /// </summary>
        private class DeltaLogData
        {
            public List<EditOperation> UndoStack { get; set; } = new();
            public List<EditOperation> RedoStack { get; set; } = new();
            public int Version { get; set; }
        }
    }
}