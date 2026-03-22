using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using NanoBananaConsole.Models;
using NanoBananaConsole.Services;

namespace NanoBananaConsole;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        ServiceProvider services = BuildServices();
        ILogger logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Program");

        try
        {
            IConfiguration config = services.GetRequiredService<IConfiguration>();
            AppSettings settings = config.GetSection("NanoBanana").Get<AppSettings>() ?? new AppSettings();

            // Environment variable overrides config file.
            settings.ApiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")?.Trim() ?? settings.ApiKey?.Trim();

            if (string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                Console.WriteLine("Missing API key.");
                Console.WriteLine("Set GEMINI_API_KEY environment variable or NanoBanana:ApiKey in appsettings.json");
                return 1;
            }

            if (args.Length == 0 || IsHelp(args[0]))
            {
                ShowHelp(settings);
                return 0;
            }

            INanoBananaService service = services.GetRequiredService<INanoBananaService>();

            string command = args[0].Trim().ToLowerInvariant();

            return command switch
            {
                "text2image" => await RunTextToImageAsync(args, settings, service),
                "edit" => await RunEditAsync(args, settings, service),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception");
            Console.WriteLine(ex.ToString());
            return 1;
        }
    }

    private static ServiceProvider BuildServices()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        ServiceCollection services = new();

        services.AddSingleton(configuration);

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.TimestampFormat = "HH:mm:ss ";
            });
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddHttpClient<INanoBananaService, NanoBananaService>();
        return services.BuildServiceProvider();
    }

    private static bool IsHelp(string value) =>
        value.Equals("help", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
        value.Equals("/?", StringComparison.OrdinalIgnoreCase);

    private static void ShowHelp(AppSettings settings)
    {
        Console.WriteLine("Nano Banana Console");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  text2image <prompt> [aspectRatio] [imageSize] [outputFile]");
        Console.WriteLine("  edit <inputImagePath> <prompt> [aspectRatio] [imageSize] [outputFile]");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  NanoBananaConsole text2image \"A cute 3D cartoon oak log, plain white background, no text\" 1:1 2K oak-log.png");
        Console.WriteLine("  NanoBananaConsole edit living-room.png \"Replace the blue sofa with a brown leather chesterfield sofa\" 16:9 2K edited-room.png");
        Console.WriteLine();
        Console.WriteLine("Current defaults:");
        Console.WriteLine($"  Model: {settings.Model}");
        Console.WriteLine($"  AspectRatio: {settings.DefaultAspectRatio}");
        Console.WriteLine($"  ImageSize: {settings.DefaultImageSize}");
        Console.WriteLine($"  OutputDirectory: {settings.OutputDirectory}");
        Console.WriteLine();
        Console.WriteLine("Environment variable:");
        Console.WriteLine("  GEMINI_API_KEY");
    }

    private static int UnknownCommand(string command)
    {
        Console.WriteLine($"Unknown command: {command}");
        Console.WriteLine("Use --help for usage.");
        return 1;
    }

    private static async Task<int> RunTextToImageAsync(
        string[] args,
        AppSettings settings,
        INanoBananaService service)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: text2image <prompt> [aspectRatio] [imageSize] [outputFile]");
            return 1;
        }

        string prompt = args[1];
        string aspectRatio = args.Length >= 3 ? args[2] : settings.DefaultAspectRatio;
        string imageSize = args.Length >= 4 ? args[3] : settings.DefaultImageSize;
        string? outputFile = args.Length >= 5 ? args[4] : null;

        ImageGenerationResult result = await service.GenerateImageAsync(
            prompt: prompt,
            aspectRatio: aspectRatio,
            imageSize: imageSize,
            outputFilePath: outputFile,
            cancellationToken: CancellationToken.None);

        PrintResult(result);
        return result.Success ? 0 : 1;
    }

    private static async Task<int> RunEditAsync(
        string[] args,
        AppSettings settings,
        INanoBananaService service)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("Usage: edit <inputImagePath> <prompt> [aspectRatio] [imageSize] [outputFile]");
            return 1;
        }

        string inputImagePath = args[1];
        string prompt = args[2];
        string aspectRatio = args.Length >= 4 ? args[3] : settings.DefaultAspectRatio;
        string imageSize = args.Length >= 5 ? args[4] : settings.DefaultImageSize;
        string? outputFile = args.Length >= 6 ? args[5] : null;

        ImageGenerationResult result = await service.EditImageAsync(
            inputImagePath: inputImagePath,
            prompt: prompt,
            aspectRatio: aspectRatio,
            imageSize: imageSize,
            outputFilePath: outputFile,
            cancellationToken: CancellationToken.None);

        PrintResult(result);
        return result.Success ? 0 : 1;
    }

    private static void PrintResult(ImageGenerationResult result)
    {
        Console.WriteLine();
        Console.WriteLine($"Success: {result.Success}");

        if (!string.IsNullOrWhiteSpace(result.Model))
            Console.WriteLine($"Model: {result.Model}");

        if (!string.IsNullOrWhiteSpace(result.OutputFilePath))
            Console.WriteLine($"Saved file: {result.OutputFilePath}");

        if (!string.IsNullOrWhiteSpace(result.MimeType))
            Console.WriteLine($"MimeType: {result.MimeType}");

        if (!string.IsNullOrWhiteSpace(result.TextResponse))
        {
            Console.WriteLine();
            Console.WriteLine("Text response:");
            Console.WriteLine(result.TextResponse);
        }

        if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
        {
            Console.WriteLine();
            Console.WriteLine("Error:");
            Console.WriteLine(result.ErrorMessage);
        }
    }
}