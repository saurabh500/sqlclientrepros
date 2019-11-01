using System;
using System.Data.SqlClient;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Threading.Tasks;
using System.Data;
using System.Data.Common;

namespace asyncperf
{
    [MemoryDiagnoser]
    public class Benchmarks
    {
        const string ConnectionString = "Server=tcp:sausing-desktop;Database=test;User=sa;Password=pwd;Connect Timeout=60;ConnectRetryCount=0";

        [Params(CommandBehavior.Default, CommandBehavior.SequentialAccess)]

        public CommandBehavior Behavior { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            using var conn = new SqlConnection(ConnectionString);
            conn.Open();

            using (var cmd = new SqlCommand("IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TextTable' AND xtype='U') CREATE TABLE [TextTable] ([Text] VARCHAR(MAX))", conn))
                cmd.ExecuteNonQuery();

            using (var cmd = new SqlCommand("INSERT INTO [TextTable] ([Text]) VALUES (@p)", conn))
            {
                cmd.Parameters.AddWithValue("p", new string('x', 1024 * 1024 * 5));
                cmd.ExecuteNonQuery();
            }
        }

        [Benchmark]
        public async ValueTask<int> Async()
        {
            using var conn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("SELECT [Text] FROM [TextTable]", conn);
            await conn.OpenAsync();

            await using var reader = await cmd.ExecuteReaderAsync(Behavior);
            await reader.ReadAsync();
            return (await reader.GetFieldValueAsync<string>(0)).Length;
        }

        [Benchmark]
        public async ValueTask<int> Sync()
        {
            using var conn = new SqlConnection(ConnectionString);
            using var cmd = new SqlCommand("SELECT [Text] FROM [TextTable]", conn);
            conn.Open();

            using var reader = cmd.ExecuteReader(Behavior);
            reader.Read();
            return reader.GetFieldValue<string>(0).Length;
        }
    }
}