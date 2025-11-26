using System;
using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

public class BulkCopyPerfTest1M
{
    public static void Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("C# Bulk Copy Performance Test - 1 Million Rows");
        Console.WriteLine("==============================================\n");

        string? sqlPassword = Environment.GetEnvironmentVariable("SQL_PASSWORD");
        if (string.IsNullOrEmpty(sqlPassword))
        {
            try
            {
                sqlPassword = System.IO.File.ReadAllText("/tmp/password").Trim();
            }
            catch
            {
                Console.Error.WriteLine("SQL_PASSWORD environment variable not set and /tmp/password could not be read");
                Environment.Exit(1);
            }
        }

        string? dbHost = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        string? dbPort = Environment.GetEnvironmentVariable("DB_PORT") ?? "1433";
        string? dbUsername = Environment.GetEnvironmentVariable("DB_USERNAME") ?? "sa";
        string? trustServerCert = Environment.GetEnvironmentVariable("TRUST_SERVER_CERTIFICATE") ?? "false";

        string connectionString = $"Server={dbHost},{dbPort};User Id={dbUsername};Password={sqlPassword};TrustServerCertificate={trustServerCert};Encrypt=true;";

        int rowCount = 1_000_000;

        // Measure connection time
        var connStart = Stopwatch.StartNew();
        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            connStart.Stop();
            Console.WriteLine($"Testing with {rowCount:N0} rows...");
            Console.WriteLine($"  Connection time: {connStart.Elapsed}");

            // Measure table setup time
            var setupStart = Stopwatch.StartNew();
            using (SqlCommand createCmd = new SqlCommand(
                @"CREATE TABLE #PerfTest (
                    id INT NOT NULL,
                    name NVARCHAR(100) NOT NULL,
                    value FLOAT NOT NULL,
                    active BIT NOT NULL
                )", connection))
            {
                createCmd.ExecuteNonQuery();
            }
            setupStart.Stop();
            Console.WriteLine($"  Table setup time: {setupStart.Elapsed}");

            // Generate test data
            var genStart = Stopwatch.StartNew();
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("id", typeof(int));
            dataTable.Columns.Add("name", typeof(string));
            dataTable.Columns.Add("value", typeof(double));
            dataTable.Columns.Add("active", typeof(bool));

            for (int i = 1; i <= rowCount; i++)
            {
                dataTable.Rows.Add(
                    i,
                    $"Record_{i:D6}",
                    i * 1.5,
                    i % 2 == 0
                );
            }
            genStart.Stop();
            Console.WriteLine($"  Data generation: {genStart.Elapsed}");

            // Measure memory before bulk copy
            long memBefore = GC.GetTotalMemory(true) / 1024;

            // Execute bulk copy
            var copyStart = Stopwatch.StartNew();
            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = "#PerfTest";
                bulkCopy.BatchSize = 0; // Match Rust implementation
                bulkCopy.BulkCopyTimeout = 300; // 5 minutes timeout
                
                bulkCopy.ColumnMappings.Add("id", "id");
                bulkCopy.ColumnMappings.Add("name", "name");
                bulkCopy.ColumnMappings.Add("value", "value");
                bulkCopy.ColumnMappings.Add("active", "active");

                bulkCopy.WriteToServer(dataTable);
            }
            copyStart.Stop();

            // Measure memory after bulk copy
            long memAfter = GC.GetTotalMemory(false) / 1024;

            double rowsPerSecond = rowCount / copyStart.Elapsed.TotalSeconds;
            
            // Calculate total time and percentages
            var totalTime = connStart.Elapsed + setupStart.Elapsed + copyStart.Elapsed;
            double connPct = connStart.Elapsed.TotalSeconds / totalTime.TotalSeconds * 100;
            double setupPct = setupStart.Elapsed.TotalSeconds / totalTime.TotalSeconds * 100;
            double copyPct = copyStart.Elapsed.TotalSeconds / totalTime.TotalSeconds * 100;

            Console.WriteLine($"\n  === Timing Breakdown ===");
            Console.WriteLine($"  Connection: {connStart.Elapsed.TotalMilliseconds:F4}ms ({connPct:F1}% of total)");
            Console.WriteLine($"  Table setup: {setupStart.Elapsed.TotalMilliseconds:F4}ms ({setupPct:F1}% of total)");
            Console.WriteLine($"  Bulk copy: {copyStart.Elapsed} ({copyPct:F1}% of total)");
            Console.WriteLine($"  Total time: {totalTime}");
            Console.WriteLine($"\n  Throughput: {rowsPerSecond:N0} rows/sec");
            Console.WriteLine($"  Memory before: {memBefore:N0} KB");
            Console.WriteLine($"  Memory after: {memAfter:N0} KB");
            Console.WriteLine($"  Memory delta: {memAfter - memBefore:N0} KB");

            // Verify count
            using (SqlCommand countCmd = new SqlCommand("SELECT COUNT(*) FROM #PerfTest", connection))
            {
                int actualCount = (int)countCmd.ExecuteScalar()!;
                if (actualCount != rowCount)
                {
                    throw new Exception($"Row count mismatch: expected {rowCount}, got {actualCount}");
                }
                Console.WriteLine($"  Verified: {actualCount:N0} rows inserted");
            }

            // Drop temp table
            using (SqlCommand dropCmd = new SqlCommand("DROP TABLE #PerfTest", connection))
            {
                dropCmd.ExecuteNonQuery();
            }

            Console.WriteLine();
        }

        Console.WriteLine("==============================================");
        Console.WriteLine("Performance Test Complete");
        Console.WriteLine("==============================================");
    }
}
