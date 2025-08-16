using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Economy_sim;

namespace Economy_sim
{
    /// <summary>
    /// Simple test class for the AuthoritativeGridManager functionality
    /// </summary>
    public class AuthoritativeGridTest
    {
        public static async Task RunTests()
        {
            Console.WriteLine("[AUTHORITATIVE GRID TEST] Starting tests...");
            
            try
            {
                await TestBasicEdit();
                await TestUndoRedo();
                await TestMultipleEdits();
                Console.WriteLine("[AUTHORITATIVE GRID TEST] All tests passed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUTHORITATIVE GRID TEST] Test failed: {ex.Message}");
            }
        }
        
        private static async Task TestBasicEdit()
        {
            Console.WriteLine("[TEST] Basic edit functionality...");
            
            string testDir = Path.Combine(Path.GetTempPath(), "grid_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(testDir);
            
            try
            {
                var gridManager = new AuthoritativeGridManager(testDir);
                
                // Apply a simple edit
                var editRegion = new Rectangle(100, 100, 10, 10);
                await gridManager.ApplyEditAsync(1, editRegion, 42, EditPolicy.FillAllSubcells);
                
                Console.WriteLine("[TEST] Basic edit completed successfully");
                
                gridManager.Dispose();
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }
        
        private static async Task TestUndoRedo()
        {
            Console.WriteLine("[TEST] Undo/Redo functionality...");
            
            string testDir = Path.Combine(Path.GetTempPath(), "grid_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(testDir);
            
            try
            {
                var gridManager = new AuthoritativeGridManager(testDir);
                
                // Apply an edit
                var editRegion = new Rectangle(50, 50, 5, 5);
                await gridManager.ApplyEditAsync(1, editRegion, 123, EditPolicy.FillAllSubcells);
                
                // Test undo
                bool undoResult = await gridManager.UndoAsync();
                if (!undoResult)
                {
                    throw new Exception("Undo operation failed");
                }
                
                // Test redo
                bool redoResult = await gridManager.RedoAsync();
                if (!redoResult)
                {
                    throw new Exception("Redo operation failed");
                }
                
                Console.WriteLine("[TEST] Undo/Redo completed successfully");
                
                gridManager.Dispose();
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }
        
        private static async Task TestMultipleEdits()
        {
            Console.WriteLine("[TEST] Multiple edits functionality...");
            
            string testDir = Path.Combine(Path.GetTempPath(), "grid_test_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(testDir);
            
            try
            {
                var gridManager = new AuthoritativeGridManager(testDir);
                
                // Apply multiple edits
                for (int i = 0; i < 5; i++)
                {
                    var editRegion = new Rectangle(i * 20, i * 20, 3, 3);
                    await gridManager.ApplyEditAsync(1, editRegion, (uint)(i + 1), EditPolicy.FillAllSubcells);
                }
                
                Console.WriteLine("[TEST] Multiple edits completed successfully");
                
                gridManager.Dispose();
            }
            finally
            {
                if (Directory.Exists(testDir))
                {
                    Directory.Delete(testDir, true);
                }
            }
        }
    }
}