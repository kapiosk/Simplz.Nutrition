namespace Simplz.Nutrition.Options;

public record OllamaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";

    public string EmbeddingModel { get; set; } = "all-minilm";

    public string ChatModel { get; set; } = "llama3";

    public int RequestTimeoutSeconds { get; set; } = 60;
}
