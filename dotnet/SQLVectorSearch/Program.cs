using System.Text.Json;
using Microsoft.Data.SqlClient;
using SQLVectorSearch;

const string CONNECTION_STRING = "Server=localhost;Database=demo;UID=sa;PWD=z+/3GyAx6zUerW0r;TrustServerCertificate=yes;";
const string TABLE_NAME = "FoodReviewEmbeddings_Local";

Console.WriteLine("=============================================================");
Console.WriteLine("SQL Server Vector Search Demo - Interactive");
Console.WriteLine("=============================================================\n");

// Load pre-generated search embeddings
Console.WriteLine("Loading pre-generated search embeddings...");
var jsonPath = Path.Combine(AppContext.BaseDirectory, "search_embeddings.json");
var jsonText = await File.ReadAllTextAsync(jsonPath);
var searchEmbeddings = JsonSerializer.Deserialize<List<SearchEmbedding>>(jsonText);

if (searchEmbeddings == null || searchEmbeddings.Count == 0)
{
    Console.WriteLine("❌ No search embeddings found!");
    return;
}

Console.WriteLine($"✓ Loaded {searchEmbeddings.Count} search terms with embeddings\n");

// Connect to SQL Server
using var connection = new SqlConnection(CONNECTION_STRING);
await connection.OpenAsync();
Console.WriteLine("✓ Connected to SQL Server\n");

// Interactive search loop
while (true)
{
    Console.WriteLine("=============================================================");
    Console.Write("Enter search term (or 'q' to quit): ");
    
    var input = Console.ReadLine()?.Trim();
    
    if (string.IsNullOrEmpty(input) || input.ToLower() == "q")
    {
        Console.WriteLine("\n✓ Goodbye!");
        break;
    }
    
    // Look up the search term in pre-generated embeddings (case-insensitive)
    var searchItem = searchEmbeddings.FirstOrDefault(s => 
        s.Term.Trim().Equals(input, StringComparison.OrdinalIgnoreCase));
    
    if (searchItem == null)
    {
        Console.WriteLine($"\n❌ Search term '{input}' not found in pre-generated embeddings.");
        Console.WriteLine("\nAvailable terms:");
        foreach (var term in searchEmbeddings)
        {
            Console.WriteLine($"  - {term.Term}");
        }
        Console.WriteLine();
        continue;
    }
    
    Console.WriteLine($"\n{new string('=', 60)}");
    Console.WriteLine($"Searching for: '{searchItem.Term}'");
    Console.WriteLine($"{new string('=', 60)}\n");
    
    // Convert embedding to JSON array string for SQL Server
    var embeddingJson = JsonSerializer.Serialize(searchItem.Embedding);
    
    // Query using VECTOR_DISTANCE function
    var query = $@"
        SELECT TOP 5
            Id,
            ProductId,
            Score,
            Summary,
            ReviewText,
            CombinedText,
            VECTOR_DISTANCE('cosine', Embedding, CAST(@embedding AS VECTOR(384))) AS Distance
        FROM {TABLE_NAME}
        ORDER BY Distance ASC";
    
    using var command = new SqlCommand(query, connection);
    command.Parameters.AddWithValue("@embedding", embeddingJson);
    
    using var reader = await command.ExecuteReaderAsync();
    
    var results = new List<FoodReview>();
    while (await reader.ReadAsync())
    {
        results.Add(new FoodReview
        {
            Id = reader.GetInt32(0),
            ProductId = reader.GetString(1),
            Score = reader.GetInt32(2),
            Summary = reader.GetString(3),
            ReviewText = reader.GetString(4),
            CombinedText = reader.GetString(5),
            Distance = reader.GetDouble(6)
        });
    }
    
    // Display results
    if (results.Count > 0)
    {
        Console.WriteLine($"\nTop {results.Count} similar reviews:\n");
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            Console.WriteLine($"{i + 1}. ID: {result.Id} | Score: {result.Score}/5 | Distance: {result.Distance:F4}");
            Console.WriteLine($"   Product: {result.ProductId}");
            Console.WriteLine($"   Summary: {result.Summary}");
            Console.WriteLine($"   Text: {(result.ReviewText.Length > 100 ? result.ReviewText.Substring(0, 100) + "..." : result.ReviewText)}");
            Console.WriteLine();
        }
    }
    else
    {
        Console.WriteLine("No results found.\n");
    }
    
    Console.WriteLine(new string('=', 60));
    Console.WriteLine("\nPress Enter to search again...");
    Console.ReadLine();
    Console.WriteLine();
}
