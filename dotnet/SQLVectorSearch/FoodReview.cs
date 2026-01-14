using System.Text.Json.Serialization;

namespace SQLVectorSearch;

public class FoodReview
{
    [JsonPropertyName("Id")]
    public int Id { get; set; }
    
    [JsonPropertyName("ProductId")]
    public string ProductId { get; set; } = string.Empty;
    
    [JsonPropertyName("Score")]
    public int Score { get; set; }
    
    [JsonPropertyName("Summary")]
    public string Summary { get; set; } = string.Empty;
    
    [JsonPropertyName("ReviewText")]
    public string ReviewText { get; set; } = string.Empty;
    
    [JsonPropertyName("CombinedText")]
    public string CombinedText { get; set; } = string.Empty;
    
    [JsonPropertyName("Distance")]
    public double Distance { get; set; }
}
