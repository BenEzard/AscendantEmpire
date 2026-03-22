using System.Text.Json.Serialization;

namespace NanoBananaConsole.Models;

public sealed class GeminiResponse
{
    [JsonPropertyName("candidates")]
    public List<Candidate>? Candidates { get; set; }

    [JsonPropertyName("promptFeedback")]
    public PromptFeedback? PromptFeedback { get; set; }

    [JsonPropertyName("usageMetadata")]
    public UsageMetadata? UsageMetadata { get; set; }
}

public sealed class Candidate
{
    [JsonPropertyName("content")]
    public ResponseContent? Content { get; set; }

    [JsonPropertyName("finishReason")]
    public string? FinishReason { get; set; }
}

public sealed class ResponseContent
{
    [JsonPropertyName("parts")]
    public List<ResponsePart>? Parts { get; set; }
}

public sealed class ResponsePart
{
    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("inlineData")]
    public InlineData? InlineData { get; set; }
}

public sealed class PromptFeedback
{
    [JsonPropertyName("blockReason")]
    public string? BlockReason { get; set; }
}

public sealed class UsageMetadata
{
    [JsonPropertyName("promptTokenCount")]
    public int? PromptTokenCount { get; set; }

    [JsonPropertyName("candidatesTokenCount")]
    public int? CandidatesTokenCount { get; set; }

    [JsonPropertyName("totalTokenCount")]
    public int? TotalTokenCount { get; set; }
}

public sealed class ImageGenerationResult
{
    public bool Success { get; set; }
    public string? Model { get; set; }
    public string? OutputFilePath { get; set; }
    public string? MimeType { get; set; }
    public string? TextResponse { get; set; }
    public string? ErrorMessage { get; set; }
}