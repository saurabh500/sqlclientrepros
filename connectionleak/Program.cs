using System;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;

namespace connectionleak
{
    class Program
    {
        static int MAX_CONNECTION_COUNT = 300;

        static void Main(string[] args)
        {
            if(args.Length < 2) {
                Console.WriteLine("Insufficient arguments: dotnet run <username> <pwd>");
                return;
            }
            Task<int>[] tasks = new Task<int>[MAX_CONNECTION_COUNT];
            for ( int i = 0; i < MAX_CONNECTION_COUNT; i++)
            {
                tasks[i] = ExecuteScalar(args[0], args[1]);
            }
            Task t  = Task.WhenAll(tasks);
            t.Wait();
            Console.WriteLine($"Successfully open {MAX_CONNECTION_COUNT} SqlConnections");
            Console.WriteLine("Execute : netstat -nat | grep 1433 to find out the current open outgoing connections. There should be only one connection open and in ESTABLISHED State.");
            Console.ReadLine();
        }

        private static async Task<int> ExecuteScalar(string username, string password)
        {
            int result = -1;
            string connectionString = $"Data Source=sql-fxtest; Initial Catalog = master; pwd={password};user Id={username}";
            for (int i = 0; i < 2; i++)
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    using (SqlCommand command = new SqlCommand("select 1", connection))
                    {
                        result = (int) await command.ExecuteScalarAsync();
                        
                    }
                }
            return result;
        }
    }
}
