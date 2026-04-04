using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using NanoBananaConsole.Models;

namespace NanoBananaConsole.Services;

public interface INanoBananaService
{
    Task<ImageGenerationResult> GenerateImageAsync(
        string prompt,
        string? aspectRatio,
        string? imageSize,
        string? outputFilePath,
        CancellationToken cancellationToken);

    Task<ImageGenerationResult> EditImageAsync(
        string inputImagePath,
        string prompt,
        string? aspectRatio,
        string? imageSize,
        string? outputFilePath,
        CancellationToken cancellationToken);
}

public sealed class NanoBananaService : INanoBananaService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<NanoBananaService> _logger;
    private readonly AppSettings _settings;

    public NanoBananaService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<NanoBananaService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _settings = configuration.GetSection("NanoBanana").Get<AppSettings>() ?? new AppSettings();

        string? envApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (!string.IsNullOrWhiteSpace(envApiKey))
            _settings.ApiKey = envApiKey.Trim();
    }

    public async Task<ImageGenerationResult> GenerateImageAsync(
        string prompt,
        string? aspectRatio,
        string? imageSize,
        string? outputFilePath,
        CancellationToken cancellationToken)
    {
        GeminiRequest request = BuildTextToImageRequest(prompt, aspectRatio, imageSize);
        return await SendAsync(request, outputFilePath, cancellationToken);
    }

    public async Task<ImageGenerationResult> EditImageAsync(
        string inputImagePath,
        string prompt,
        string? aspectRatio,
        string? imageSize,
        string? outputFilePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(inputImagePath))
            throw new ArgumentException("Input image path is required.", nameof(inputImagePath));

        if (!File.Exists(inputImagePath))
            throw new FileNotFoundException("Input image file not found.", inputImagePath);

        GeminiRequest request = await BuildEditImageRequestAsync(
            inputImagePath,
            prompt,
            aspectRatio,
            imageSize,
            cancellationToken);

        return await SendAsync(request, outputFilePath, cancellationToken);
    }

    private GeminiRequest BuildTextToImageRequest(
        string prompt,
        string? aspectRatio,
        string? imageSize)
    {
        return new GeminiRequest
        {
            Contents =
            {
                new Content
                {
                    Parts =
                    {
                        new Part { Text = prompt }
                    }
                }
            },
            GenerationConfig = BuildGenerationConfig(aspectRatio, imageSize)
        };
    }

    private async Task<GeminiRequest> BuildEditImageRequestAsync(
        string inputImagePath,
        string prompt,
        string? aspectRatio,
        string? imageSize,
        CancellationToken cancellationToken)
    {
        byte[] bytes = await File.ReadAllBytesAsync(inputImagePath, cancellationToken);
        string mimeType = GetMimeTypeFromPath(inputImagePath);
        string base64 = Convert.ToBase64String(bytes);

        return new GeminiRequest
        {
            Contents =
            {
                new Content
                {
                    Parts =
                    {
                        new Part
                        {
                            InlineData = new InlineData
                            {
                                MimeType = mimeType,
                                Data = base64
                            }
                        },
                        new Part
                        {
                            Text = prompt
                        }
                    }
                }
            },
            GenerationConfig = BuildGenerationConfig(aspectRatio, imageSize)
        };
    }

    private GenerationConfig BuildGenerationConfig(string? aspectRatio, string? imageSize)
    {
        return new GenerationConfig
        {
            ResponseModalities = ["IMAGE"],
            ImageConfig = new ImageConfig
            {
                AspectRatio = string.IsNullOrWhiteSpace(aspectRatio) ? _settings.DefaultAspectRatio : aspectRatio,
                ImageSize = string.IsNullOrWhiteSpace(imageSize) ? _settings.DefaultImageSize : imageSize
            }
        };
    }

    private async Task<ImageGenerationResult> SendAsync(
        GeminiRequest request,
        string? outputFilePath,
        CancellationToken cancellationToken)
    {
        string endpoint = BuildEndpoint();

        string json = JsonSerializer.Serialize(request, JsonOptions);

        using HttpRequestMessage httpRequest = new(HttpMethod.Post, endpoint);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Sending request to model {Model}", _settings.Model);

        using HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini API returned {StatusCode}: {Body}", (int)response.StatusCode, responseContent);

            return new ImageGenerationResult
            {
                Success = false,
                Model = _settings.Model,
                ErrorMessage = $"HTTP {(int)response.StatusCode}: {responseContent}"
            };
        }

        GeminiResponse? geminiResponse = JsonSerializer.Deserialize<GeminiResponse>(responseContent, JsonOptions);
        if (geminiResponse is null)
        {
            return new ImageGenerationResult
            {
                Success = false,
                Model = _settings.Model,
                ErrorMessage = "Failed to parse Gemini response."
            };
        }

        if (!string.IsNullOrWhiteSpace(geminiResponse.PromptFeedback?.BlockReason))
        {
            return new ImageGenerationResult
            {
                Success = false,
                Model = _settings.Model,
                ErrorMessage = $"Blocked: {geminiResponse.PromptFeedback.BlockReason}"
            };
        }

        ResponsePart? imagePart = geminiResponse.Candidates?
            .SelectMany(c => c.Content?.Parts ?? Enumerable.Empty<ResponsePart>())
            .FirstOrDefault(p => p.InlineData is not null);

        string textResponse = string.Join(
            Environment.NewLine,
            geminiResponse.Candidates?
                .SelectMany(c => c.Content?.Parts ?? Enumerable.Empty<ResponsePart>())
                .Where(p => !string.IsNullOrWhiteSpace(p.Text))
                .Select(p => p.Text!.Trim()) ?? Enumerable.Empty<string>());

        if (imagePart?.InlineData?.Data is null)
        {
            return new ImageGenerationResult
            {
                Success = false,
                Model = _settings.Model,
                TextResponse = string.IsNullOrWhiteSpace(textResponse) ? null : textResponse,
                ErrorMessage = "No image was returned by the model."
            };
        }

        byte[] imageBytes = Convert.FromBase64String(imagePart.InlineData.Data);
        string mimeType = imagePart.InlineData.MimeType;
        string finalPath = ResolveOutputPath(outputFilePath, mimeType);

        string? directory = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(finalPath, imageBytes, cancellationToken);

        _logger.LogInformation("Saved image to {Path}", finalPath);

        return new ImageGenerationResult
        {
            Success = true,
            Model = _settings.Model,
            OutputFilePath = finalPath,
            MimeType = mimeType,
            TextResponse = string.IsNullOrWhiteSpace(textResponse) ? null : textResponse
        };
    }

    private string BuildEndpoint()
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new InvalidOperationException("API key is missing.");

        string baseUrl = _settings.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/models/{_settings.Model}:generateContent?key={_settings.ApiKey}";
    }

    private string ResolveOutputPath(string? outputFilePath, string mimeType)
    {
        if (!string.IsNullOrWhiteSpace(outputFilePath))
            return outputFilePath;

        string extension = mimeType switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".bin"
        };

        string directory = string.IsNullOrWhiteSpace(_settings.OutputDirectory)
            ? "output"
            : _settings.OutputDirectory;

        string fileName = $"nano-banana-{DateTime.Now:yyyyMMdd-HHmmss}{extension}";
        return Path.Combine(directory, fileName);
    }

    private static string GetMimeTypeFromPath(string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".png" => "image/png",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => throw new NotSupportedException($"Unsupported input file extension: {extension}")
        };
    }
}