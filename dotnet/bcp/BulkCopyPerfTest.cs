using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.Diagnostics;

namespace SqlClientBulkCopyPerf
{
    class BulkCopyPerfTest
    {
        private const string ConnectionString = "Server=localhost,14333;Database=project;User Id=sa;Password=TestPass;Encrypt=False;TrustServerCertificate=True";

        static void Main(string[] args)
        {
            Console.WriteLine("==============================================");
            Console.WriteLine("Bulk Copy Performance Test - C# Implementation");
            Console.WriteLine("==============================================");
            Console.WriteLine();

            try
            {
                SetupDatabase();

                // Test with different row counts
                int[] rowCounts = { 100, 1000, 10000, 100000 };

                foreach (int rowCount in rowCounts)
                {
                    TestBulkCopy(rowCount);
                    Console.WriteLine();
                }

                Console.WriteLine("==============================================");
                Console.WriteLine("All tests completed successfully!");
                Console.WriteLine("==============================================");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
                }
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }
        }

        static void SetupDatabase()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                IF OBJECT_ID('dbo.BulkCopyPerfTest', 'U') IS NOT NULL 
                    DROP TABLE dbo.BulkCopyPerfTest;
                
                CREATE TABLE dbo.BulkCopyPerfTest (
                    id INT NOT NULL,
                    name NVARCHAR(100) NOT NULL,
                    value FLOAT NOT NULL,
                    active BIT NOT NULL
                )";
            cmd.ExecuteNonQuery();
        }

        static void TestBulkCopy(int rowCount)
        {
            Console.WriteLine($"Testing with {rowCount:N0} rows...");

            // Measure data generation time
            var genStopwatch = Stopwatch.StartNew();
            var table = GenerateTestData(rowCount);
            genStopwatch.Stop();
            Console.WriteLine($"  Data generation: {genStopwatch.Elapsed.TotalMilliseconds:F1}ms");

            // Get memory before
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long memBefore = GC.GetTotalMemory(false);

            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            // Truncate table
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "TRUNCATE TABLE dbo.BulkCopyPerfTest";
                cmd.ExecuteNonQuery();
            }

            // Measure bulk copy time
            var bcpStopwatch = Stopwatch.StartNew();
            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = "dbo.BulkCopyPerfTest";
                bulkCopy.BatchSize = 1000;
                bulkCopy.BulkCopyTimeout = 300;

                // Map columns
                bulkCopy.ColumnMappings.Add("id", "id");
                bulkCopy.ColumnMappings.Add("name", "name");
                bulkCopy.ColumnMappings.Add("value", "value");
                bulkCopy.ColumnMappings.Add("active", "active");

                bulkCopy.WriteToServer(table);
            }
            bcpStopwatch.Stop();

            // Get memory after
            long memAfter = GC.GetTotalMemory(false);
            long memDelta = memAfter - memBefore;

            // Verify row count
            int actualRows = 0;
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.BulkCopyPerfTest";
                actualRows = (int)cmd.ExecuteScalar();
            }

            // Display results
            Console.WriteLine($"  Bulk copy duration: {bcpStopwatch.Elapsed.TotalMilliseconds:F3}ms");
            Console.WriteLine($"  Rows inserted: {actualRows:N0}");
            Console.WriteLine($"  Throughput: {(actualRows / bcpStopwatch.Elapsed.TotalSeconds):F0} rows/sec");
            Console.WriteLine($"  Memory before: {memBefore / 1024:N0} KB");
            Console.WriteLine($"  Memory after: {memAfter / 1024:N0} KB");
            Console.WriteLine($"  Memory delta: {memDelta / 1024:N0} KB");

            if (actualRows != rowCount)
            {
                throw new Exception($"Row count mismatch! Expected {rowCount}, got {actualRows}");
            }
        }

        static DataTable GenerateTestData(int rowCount)
        {
            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("name", typeof(string));
            table.Columns.Add("value", typeof(double));
            table.Columns.Add("active", typeof(bool));

            for (int i = 0; i < rowCount; i++)
            {
                table.Rows.Add(
                    i + 1,
                    $"Test Record {i + 1}",
                    i * 1.5,
                    i % 2 == 0
                );
            }

            return table;
        }
    }
}
