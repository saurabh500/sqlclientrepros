using System;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace connectionleak
{
    class Program
    {
        static int MAX_CONNECTION_COUNT = 300;

        static void Main(string[] args)
        {
            if(args.Length < 3) {
                Console.WriteLine("Insufficient arguments: dotnet run <server endpoint> <username> <pwd>");
                return;
            }
            Task<int>[] tasks = new Task<int>[MAX_CONNECTION_COUNT];
            for ( int i = 0; i < MAX_CONNECTION_COUNT; i++)
            {
                tasks[i] = ExecuteScalar(args[0], args[1], args[2]);
            }
            Task t  = Task.WhenAll(tasks);
            t.Wait();
            Console.WriteLine("Execute : netstat -nat | grep 1433 to find out the current open outgoing connections. There should be only one connection open and in ESTABLISHED State.");
            Console.ReadLine();
        }

        private static async Task<int> ExecuteScalar(string server, string username, string password)
        {
            int result = -1;
            string connectionString = $"Data Source={server}; Initial Catalog = master; pwd={password};user Id={username}";
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                await connection.OpenAsync();
            }
            return result;
        }
    }
}
