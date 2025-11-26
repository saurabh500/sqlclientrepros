using System;
using System.Data;
using System.Diagnostics;
using Microsoft.Data.SqlClient;

class BulkCopyGuidTest
{
    static void Main(string[] args)
    {
        Console.WriteLine("==============================================");
        Console.WriteLine("C# Bulk Copy GUID/UniqueIdentifier Test");
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

        // Enable TDS trace if requested
        string? enableTrace = Environment.GetEnvironmentVariable("ENABLE_TRACE");
        if (!string.IsNullOrEmpty(enableTrace) && enableTrace.ToLower() == "true")
        {
            Console.WriteLine("TDS Tracing ENABLED via AppContext");
            AppContext.SetSwitch("Switch.Microsoft.Data.SqlClient.EnableDiagnosticLogging", true);
        }

        string connectionString = $"Server={dbHost},{dbPort};User Id={dbUsername};Password={sqlPassword};TrustServerCertificate={trustServerCert};Encrypt=true;";

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            connection.Open();
            Console.WriteLine("Connected to SQL Server");

            // Create temp table with uniqueidentifier column
            using (SqlCommand createCmd = new SqlCommand(
                @"CREATE TABLE #BulkCopyGuid (
                    id UNIQUEIDENTIFIER NOT NULL,
                    counter INT NOT NULL
                )", connection))
            {
                createCmd.ExecuteNonQuery();
                Console.WriteLine("Created temp table #BulkCopyGuid");
            }

            // Generate test data with known GUIDs
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("id", typeof(Guid));
            dataTable.Columns.Add("counter", typeof(int));

            dataTable.Rows.Add(Guid.Parse("550e8400-e29b-41d4-a716-446655440000"), 1);
            dataTable.Rows.Add(Guid.Parse("6ba7b810-9dad-11d1-80b4-00c04fd430c8"), 2);
            dataTable.Rows.Add(Guid.Parse("6ba7b811-9dad-11d1-80b4-00c04fd430c8"), 3);

            Console.WriteLine($"Prepared {dataTable.Rows.Count} rows with GUID data");

            // Execute bulk copy
            Console.WriteLine("\nExecuting bulk copy...");
            var copyStart = Stopwatch.StartNew();
            using (SqlBulkCopy bulkCopy = new SqlBulkCopy(connection))
            {
                bulkCopy.DestinationTableName = "#BulkCopyGuid";
                bulkCopy.BatchSize = 1000;
                bulkCopy.BulkCopyTimeout = 30;
                
                bulkCopy.ColumnMappings.Add("id", "id");
                bulkCopy.ColumnMappings.Add("counter", "counter");

                bulkCopy.WriteToServer(dataTable);
            }
            copyStart.Stop();

            Console.WriteLine($"Bulk copy completed in {copyStart.Elapsed}");

            // Verify the data
            using (SqlCommand selectCmd = new SqlCommand("SELECT id, counter FROM #BulkCopyGuid ORDER BY counter", connection))
            using (SqlDataReader reader = selectCmd.ExecuteReader())
            {
                Console.WriteLine("\nVerifying inserted data:");
                while (reader.Read())
                {
                    Guid id = reader.GetGuid(0);
                    int counter = reader.GetInt32(1);
                    Console.WriteLine($"  Row {counter}: {id}");
                }
            }

            Console.WriteLine("\nTest completed successfully!");
        }
    }
}
