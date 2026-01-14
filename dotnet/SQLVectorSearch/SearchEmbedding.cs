using System.Text.Json.Serialization;

namespace SQLVectorSearch;

public class SearchEmbedding
{
    [JsonPropertyName("term")]
    public string Term { get; set; } = string.Empty;
    
    [JsonPropertyName("embedding")]
    public List<float> Embedding { get; set; } = new();
}
