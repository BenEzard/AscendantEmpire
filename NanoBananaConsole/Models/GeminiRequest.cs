using System.Text.Json.Serialization;

namespace NanoBananaConsole.Models;

public sealed class GeminiRequest
{
    [JsonPropertyName("contents")]
    public List<Content> Contents { get; set; } = new();

    [JsonPropertyName("generationConfig")]
    public GenerationConfig? GenerationConfig { get; set; }
}

public sealed class Content
{
    [JsonPropertyName("parts")]
    public List<Part> Parts { get; set; } = new();
}

public sealed class Part
{
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Text { get; set; }

    [JsonPropertyName("inlineData")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public InlineData? InlineData { get; set; }
}

public sealed class InlineData
{
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = null!;

    [JsonPropertyName("data")]
    public string Data { get; set; } = null!;
}

public sealed class GenerationConfig
{
    [JsonPropertyName("responseModalities")]
    public List<string> ResponseModalities { get; set; } = new();

    [JsonPropertyName("imageConfig")]
    public ImageConfig? ImageConfig { get; set; }
}

public sealed class ImageConfig
{
    [JsonPropertyName("aspectRatio")]
    public string? AspectRatio { get; set; }

    [JsonPropertyName("imageSize")]
    public string? ImageSize { get; set; }
}