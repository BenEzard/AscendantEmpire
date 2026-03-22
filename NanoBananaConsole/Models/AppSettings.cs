namespace NanoBananaConsole.Models;

public sealed class AppSettings
{
    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
    public string Model { get; set; } = "gemini-3.1-flash-image-preview";
    public string DefaultAspectRatio { get; set; } = "1:1";
    public string DefaultImageSize { get; set; } = "1024";
    public string OutputDirectory { get; set; } = "output";
}