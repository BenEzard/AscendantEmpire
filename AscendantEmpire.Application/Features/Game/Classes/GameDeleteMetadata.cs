using System.Text.Json;

namespace AscendantEmpire.Application.Features.Game.Classes;

/// <summary>
/// The <c>MetadataJson</c> bag carried by a <c>DeleteGame</c> background job — which Game the job is to purge.
/// </summary>
/// <remarks>
/// A <c>Processing</c> row has no foreign key to anything, so the target has to travel as loose JSON. Reading and writing it lives here rather than being
/// spelled out at each end, because the initiating handler and the executor must agree on the property name exactly and nothing else would catch a
/// mismatch until the job failed at run time.
/// </remarks>
internal static class GameDeleteMetadata
{
    /// <summary>The JSON property the Game's id is stored under.</summary>
    private const string _GAME_ID_PROPERTY = "gameId";

    /// <summary>
    /// Builds the <c>MetadataJson</c> value for a delete job targeting <paramref name="gameId"/>.
    /// </summary>
    public static string Write(int gameId)
    {
        return JsonSerializer.Serialize(new Dictionary<string, int> { [_GAME_ID_PROPERTY] = gameId });
    }

    /// <summary>
    /// Reads the target Game's id back out of a <c>MetadataJson</c> value.
    /// </summary>
    /// <returns>The Game id, or null when the metadata is missing, unparseable, or does not carry one.</returns>
    /// <remarks>
    /// Returns null rather than throwing so callers can decide what a malformed row means. The executor treats it as a job failure; the duplicate check
    /// in the initiating handler simply skips the row, because a job it cannot read the target of is not evidence that this game is already being deleted.
    /// </remarks>
    public static int? TryRead(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(metadataJson);

            if (document.RootElement.TryGetProperty(_GAME_ID_PROPERTY, out JsonElement gameIdElement) == false)
            {
                return null;
            }

            return gameIdElement.TryGetInt32(out int gameId) ? gameId : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
