using System;
using Microsoft.Data.SqlClient;

namespace connectionleak
{
    class Program
    {
        static int MAX_CONNECTION_COUNT = 300;

        static void Main(string[] args)
        {
            string connectionString = $"Data Source=sql-fxtest; Initial Catalog = master; pwd={args[1]};user Id={args[0]}";
            for(int i = 0; i < MAX_CONNECTION_COUNT; i++)
            using(SqlConnection connection = new SqlConnection(connectionString)) {
                connection.Open();
            }
            Console.WriteLine($"Successfully open {MAX_CONNECTION_COUNT} SqlConnections");

        }
    }
}
