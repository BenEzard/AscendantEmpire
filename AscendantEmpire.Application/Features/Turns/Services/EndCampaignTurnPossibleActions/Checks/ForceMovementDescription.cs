using System.Globalization;

using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Turns.Services.EndCampaignTurnPossibleActions.Checks;

/// <summary>
/// Words a Force with movement left as the notifications panel shows it: <c>Settler (0.5/1)</c>.
/// </summary>
/// <remarks>
/// Shared by the campaign and battle movement checks so both read the same. Formatted invariantly, because the description is display text the
/// client shows as it arrives, and a server culture with a comma decimal separator would put one in front of every player.
/// </remarks>
internal static class ForceMovementDescription
{
    public static string Describe(string templateName, long rawMovementRemaining, long rawMovementPoints)
    {
        return $"{templateName} ({Format(rawMovementRemaining)}/{Format(rawMovementPoints)})";
    }

    private static string Format(long rawMovement)
    {
        return ScaledAmount.FromRaw(rawMovement).ToDecimal().ToString("0.##", CultureInfo.InvariantCulture);
    }
}
