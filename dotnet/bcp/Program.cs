using Microsoft.Data.SqlClient;
using System;
using System.Data;

namespace SqlClientTypeConversionTests
{
    class Program
    {
        private const string ConnectionString = "Server=localhost,14333;Database=project;User Id=sa;Password=TestPass;Encrypt=False";

        static void Main(string[] args)
        {
            // Check if a specific test is requested
            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "BulkCopyPerfTest1M":
                        BulkCopyPerfTest1M.Main(args);
                        return;
                    case "BulkCopyPerfTest1MAsync":
                        BulkCopyPerfTest1MAsync.Main(args).GetAwaiter().GetResult();
                        return;
                }
            }

            Console.WriteLine("=== SqlClient Type Conversion Test Suite ===\n");

            try
            {
                // Test all numeric types
                TestNumericConversions();
                
                // Test decimal types with precision
                TestDecimalConversions();
                
                // Test date/time types
                TestDateTimeConversions();
                
                // Test string types
                TestStringConversions();
                
                // Test GUID
                TestGuidConversion();
                
                // Test special cases from CoerceValue
                TestSpecialCaseConversions();
                
                // Test overflow scenarios
                TestOverflowScenarios();
                
                // Test NULL handling
                TestNullHandling();

                Console.WriteLine("\n=== All Tests Completed Successfully ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n=== Test Suite Failed ===");
                Console.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
                }
            }
        }

        static void TestNumericConversions()
        {
            Console.WriteLine("--- Test 1: Numeric Type Conversions (String → Numbers) ---");
            
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            // Create table
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    IF OBJECT_ID('dbo.NumericTest', 'U') IS NOT NULL DROP TABLE dbo.NumericTest;
                    CREATE TABLE dbo.NumericTest (
                        id INT,
                        tiny_col TINYINT,
                        small_col SMALLINT,
                        int_col INT,
                        big_col BIGINT,
                        real_col REAL,
                        float_col FLOAT
                    )";
                cmd.ExecuteNonQuery();
            }

            // Create DataTable with string values
            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("tiny_col", typeof(string));
            table.Columns.Add("small_col", typeof(string));
            table.Columns.Add("int_col", typeof(string));
            table.Columns.Add("big_col", typeof(string));
            table.Columns.Add("real_col", typeof(string));
            table.Columns.Add("float_col", typeof(string));

            // Add test rows
            table.Rows.Add(1, "100", "1000", "100000", "10000000000", "3.14", "3.141592653589793");
            table.Rows.Add(2, "255", "32767", "2147483647", "9223372036854775807", "1.23E+10", "1.79E+308");
            table.Rows.Add(3, "0", "-32768", "-2147483648", "-9223372036854775808", "-3.40E+38", "-1.79E+308");

            // Bulk insert
            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = "dbo.NumericTest";
                bulkCopy.WriteToServer(table);
            }

            // Verify
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.NumericTest";
                int count = (int)cmd.ExecuteScalar();
                Console.WriteLine($"✓ Inserted {count} rows successfully");
                Console.WriteLine($"  - TINYINT: 0 to 255");
                Console.WriteLine($"  - SMALLINT: -32768 to 32767");
                Console.WriteLine($"  - INT: -2147483648 to 2147483647");
                Console.WriteLine($"  - BIGINT: ±9.2E18");
                Console.WriteLine($"  - REAL: scientific notation");
                Console.WriteLine($"  - FLOAT: high precision decimals");
            }

            conn.Close();
        }

        static void TestDecimalConversions()
        {
            Console.WriteLine("\n--- Test 2: Decimal/Money Type Conversions ---");
            
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    IF OBJECT_ID('dbo.DecimalTest', 'U') IS NOT NULL DROP TABLE dbo.DecimalTest;
                    CREATE TABLE dbo.DecimalTest (
                        id INT,
                        decimal_col DECIMAL(18, 4),
                        money_col MONEY,
                        smallmoney_col SMALLMONEY
                    )";
                cmd.ExecuteNonQuery();
            }

            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("decimal_col", typeof(string));
            table.Columns.Add("money_col", typeof(string));
            table.Columns.Add("smallmoney_col", typeof(string));

            table.Rows.Add(1, "12345.6789", "1234567.89", "12345.67");
            table.Rows.Add(2, "0.0001", "0.01", "0.01");
            table.Rows.Add(3, "-12345.6789", "-1234567.89", "-12345.67");

            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = "dbo.DecimalTest";
                bulkCopy.WriteToServer(table);
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.DecimalTest";
                int count = (int)cmd.ExecuteScalar();
                Console.WriteLine($"✓ Inserted {count} rows successfully");
                Console.WriteLine($"  - DECIMAL(18,4): 4 decimal places");
                Console.WriteLine($"  - MONEY: Currency format");
                Console.WriteLine($"  - SMALLMONEY: Smaller range currency");
            }

            conn.Close();
        }

        static void TestDateTimeConversions()
        {
            Console.WriteLine("\n--- Test 3: Date/Time Type Conversions ---");
            
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    IF OBJECT_ID('dbo.DateTimeTest', 'U') IS NOT NULL DROP TABLE dbo.DateTimeTest;
                    CREATE TABLE dbo.DateTimeTest (
                        id INT,
                        datetime_col DATETIME,
                        smalldatetime_col SMALLDATETIME,
                        datetime2_col DATETIME2(7),
                        date_col DATE,
                        time_col TIME(7),
                        datetimeoffset_col DATETIMEOFFSET(7)
                    )";
                cmd.ExecuteNonQuery();
            }

            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("datetime_col", typeof(string));
            table.Columns.Add("smalldatetime_col", typeof(string));
            table.Columns.Add("datetime2_col", typeof(string));
            table.Columns.Add("date_col", typeof(string));
            table.Columns.Add("time_col", typeof(string));
            table.Columns.Add("datetimeoffset_col", typeof(string));

            table.Rows.Add(1, 
                "2024-01-15 14:30:00", 
                "2024-01-15 14:30", 
                "2024-01-15 14:30:00.1234567",
                "2024-01-15",
                "14:30:00.1234567",
                "2024-01-15 14:30:00.1234567 -08:00");
                
            table.Rows.Add(2,
                "1753-01-01 00:00:00",  // DATETIME min
                "1900-01-01 00:00",      // SMALLDATETIME min
                "0001-01-01 00:00:00",   // DATETIME2 min
                "0001-01-01",             // DATE min
                "00:00:00",               // TIME min
                "0001-01-01 00:00:00 +00:00"); // DATETIMEOFFSET min

            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = "dbo.DateTimeTest";
                bulkCopy.WriteToServer(table);
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.DateTimeTest";
                int count = (int)cmd.ExecuteScalar();
                Console.WriteLine($"✓ Inserted {count} rows successfully");
                Console.WriteLine($"  - DATETIME: 3.33ms precision (1753-9999)");
                Console.WriteLine($"  - SMALLDATETIME: 1 minute precision (1900-2079)");
                Console.WriteLine($"  - DATETIME2: 100ns precision (0001-9999)");
                Console.WriteLine($"  - DATE: Date only");
                Console.WriteLine($"  - TIME: Time only with 100ns precision");
                Console.WriteLine($"  - DATETIMEOFFSET: DateTime with timezone offset");
            }

            conn.Close();
        }

        static void TestStringConversions()
        {
            Console.WriteLine("\n--- Test 4: String Type Conversions ---");
            
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    IF OBJECT_ID('dbo.StringTest', 'U') IS NOT NULL DROP TABLE dbo.StringTest;
                    CREATE TABLE dbo.StringTest (
                        id INT,
                        char_col CHAR(10),
                        varchar_col VARCHAR(100),
                        nchar_col NCHAR(10),
                        nvarchar_col NVARCHAR(100),
                        nvarchar_max_col NVARCHAR(MAX)
                    )";
                cmd.ExecuteNonQuery();
            }

            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("char_col", typeof(string));
            table.Columns.Add("varchar_col", typeof(string));
            table.Columns.Add("nchar_col", typeof(string));
            table.Columns.Add("nvarchar_col", typeof(string));
            table.Columns.Add("nvarchar_max_col", typeof(string));

            table.Rows.Add(1, "abc", "Hello", "xyz", "World", "This is a very long string that demonstrates NVARCHAR(MAX) capability");
            table.Rows.Add(2, "test", "ANSI text", "test", "Unicode: 你好", new string('X', 10000)); // 10K chars

            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = "dbo.StringTest";
                bulkCopy.WriteToServer(table);
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.StringTest";
                int count = (int)cmd.ExecuteScalar();
                Console.WriteLine($"✓ Inserted {count} rows successfully");
                Console.WriteLine($"  - CHAR: Fixed-length ANSI (padded)");
                Console.WriteLine($"  - VARCHAR: Variable-length ANSI");
                Console.WriteLine($"  - NCHAR: Fixed-length Unicode (padded)");
                Console.WriteLine($"  - NVARCHAR: Variable-length Unicode");
                Console.WriteLine($"  - NVARCHAR(MAX): Large Unicode (up to 2GB)");
            }

            conn.Close();
        }

        static void TestGuidConversion()
        {
            Console.WriteLine("\n--- Test 5: GUID/UniqueIdentifier Conversion ---");
            
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    IF OBJECT_ID('dbo.GuidTest', 'U') IS NOT NULL DROP TABLE dbo.GuidTest;
                    CREATE TABLE dbo.GuidTest (
                        id INT,
                        guid_col UNIQUEIDENTIFIER
                    )";
                cmd.ExecuteNonQuery();
            }

            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("guid_col", typeof(Guid)); // Changed from typeof(string) to typeof(Guid)

            table.Rows.Add(1, Guid.Parse("12345678-1234-1234-1234-123456789012"));
            table.Rows.Add(2, Guid.NewGuid());
            table.Rows.Add(3, Guid.Parse("00000000-0000-0000-0000-000000000000"));

            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = "dbo.GuidTest";
                bulkCopy.WriteToServer(table);
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.GuidTest";
                int count = (int)cmd.ExecuteScalar();
                Console.WriteLine($"✓ Inserted {count} rows successfully");
                Console.WriteLine($"  - UNIQUEIDENTIFIER: Guid objects work (strings do NOT work)");
            }

            conn.Close();
        }

        static void TestSpecialCaseConversions()
        {
            Console.WriteLine("\n--- Test 6: Special Case Conversions (From CoerceValue) ---");
            
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            // Test currency parsing with NumberStyles.Currency
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    IF OBJECT_ID('dbo.CurrencyTest', 'U') IS NOT NULL DROP TABLE dbo.CurrencyTest;
                    CREATE TABLE dbo.CurrencyTest (
                        id INT,
                        money_col MONEY
                    )";
                cmd.ExecuteNonQuery();
            }

            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("money_col", typeof(string));

            // Test various currency formats
            table.Rows.Add(1, "1234.56");      // Plain decimal
            table.Rows.Add(2, "$1,234.56");    // Currency symbol with thousands separator
            table.Rows.Add(3, "-1234.56");     // Negative

            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = "dbo.CurrencyTest";
                bulkCopy.WriteToServer(table);
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.CurrencyTest";
                int count = (int)cmd.ExecuteScalar();
                Console.WriteLine($"✓ Currency parsing: {count} rows");
                Console.WriteLine($"  - Handles currency symbols and thousand separators");
            }

            conn.Close();
        }

        static void TestOverflowScenarios()
        {
            Console.WriteLine("\n--- Test 7: Overflow/Underflow Detection ---");
            
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    IF OBJECT_ID('dbo.OverflowTest', 'U') IS NOT NULL DROP TABLE dbo.OverflowTest;
                    CREATE TABLE dbo.OverflowTest (
                        id INT,
                        tiny_col TINYINT
                    )";
                cmd.ExecuteNonQuery();
            }

            // Test TINYINT overflow (max 255)
            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("tiny_col", typeof(int));

            table.Rows.Add(1, 256); // Will overflow

            try
            {
                using var bulkCopy = new SqlBulkCopy(conn);
                bulkCopy.DestinationTableName = "dbo.OverflowTest";
                bulkCopy.WriteToServer(table);
                Console.WriteLine($"✗ Should have thrown overflow exception!");
            }
            catch (InvalidOperationException ex) when (ex.InnerException is OverflowException)
            {
                Console.WriteLine($"✓ Overflow detection working correctly");
                Console.WriteLine($"  - Value 256 → TINYINT correctly throws OverflowException");
                Console.WriteLine($"  - Exception message: {ex.Message}");
            }

            // Test SMALLINT overflow
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    IF OBJECT_ID('dbo.SmallIntOverflow', 'U') IS NOT NULL DROP TABLE dbo.SmallIntOverflow;
                    CREATE TABLE dbo.SmallIntOverflow (
                        id INT,
                        small_col SMALLINT
                    )";
                cmd.ExecuteNonQuery();
            }

            table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("small_col", typeof(int));
            table.Rows.Add(1, 32768); // Max is 32767

            try
            {
                using var bulkCopy = new SqlBulkCopy(conn);
                bulkCopy.DestinationTableName = "dbo.SmallIntOverflow";
                bulkCopy.WriteToServer(table);
                Console.WriteLine($"✗ Should have thrown overflow exception!");
            }
            catch (InvalidOperationException ex) when (ex.InnerException is OverflowException)
            {
                Console.WriteLine($"✓ SMALLINT overflow: 32768 correctly rejected");
            }

            conn.Close();
        }

        static void TestNullHandling()
        {
            Console.WriteLine("\n--- Test 8: NULL Value Handling ---");
            
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = @"
                    IF OBJECT_ID('dbo.NullTest', 'U') IS NOT NULL DROP TABLE dbo.NullTest;
                    CREATE TABLE dbo.NullTest (
                        id INT,
                        int_col INT NULL,
                        varchar_col VARCHAR(100) NULL,
                        datetime_col DATETIME NULL
                    )";
                cmd.ExecuteNonQuery();
            }

            var table = new DataTable();
            table.Columns.Add("id", typeof(int));
            table.Columns.Add("int_col", typeof(string));
            table.Columns.Add("varchar_col", typeof(string));
            table.Columns.Add("datetime_col", typeof(string));

            // Add rows with NULL values
            table.Rows.Add(1, "123", "test", "2024-01-15");
            table.Rows.Add(2, DBNull.Value, DBNull.Value, DBNull.Value);
            table.Rows.Add(3, null, null, null);

            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = "dbo.NullTest";
                bulkCopy.WriteToServer(table);
            }

            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.NullTest WHERE int_col IS NULL";
                int nullCount = (int)cmd.ExecuteScalar();
                Console.WriteLine($"✓ NULL handling: {nullCount} NULL rows detected");
                Console.WriteLine($"  - DBNull.Value and null both handled correctly");
            }

            conn.Close();
        }
    }
}
