using System;
using Microsoft.Data.SqlClient;

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
            string connectionString = $"Data Source=sql-fxtest; Initial Catalog = master; pwd={args[1]};user Id={args[0]}";
            for(int i = 0; i < MAX_CONNECTION_COUNT; i++)
            using(SqlConnection connection = new SqlConnection(connectionString)) {
                connection.Open();
                using(SqlCommand command = new SqlCommand("select 1", connection)) {
                    var a =command.ExecuteScalar();
                    Console.WriteLine(a);
                }
            }
            Console.WriteLine($"Successfully open {MAX_CONNECTION_COUNT} SqlConnections");
            Console.ReadLine();
        }
    }
}
