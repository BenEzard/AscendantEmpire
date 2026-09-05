using System.Text.Json;

using AscendantEmpire.Application.Helpers.ScaledNumerics;

using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;

namespace AscendantEmpire.Application.Features.ForceOrders.Movement;

/// <summary>
/// Central place for converting loaded Unit and compiled design data into movement models.
/// Keeping this mapping here prevents handlers and planners from needing to know how modifier
/// JSON is shaped or how common bonuses are inferred from that JSON.
/// </summary>
internal sealed class MovementProfileFactory : IMovementProfileFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MovementProfile Create(UnitInstance force, DomainType domainType)
    {
        ArgumentNullException.ThrowIfNull(force);
        ArgumentNullException.ThrowIfNull(force.UnitTemplate);
        ArgumentNullException.ThrowIfNull(force.UnitTemplate.Compiled);

        var compiled = force.UnitTemplate.Compiled;

        // The compiled template snapshot does not carry movement-modifier JSON yet (the legacy
        // CompiledDesignStats.ModifiersJson has no template-model equivalent), so template Forces move
        // un-modified for now. The parsing below is kept wired so the JSON slots straight back in when it
        // is compiled onto the snapshot — see the military-organisation implementation notes.
        string? modifiersJson = null;

        IReadOnlyList<MovementModifier> modifiers = ParseModifiers(modifiersJson);
        IReadOnlySet<MoveModifier> bonuses = DeriveBonuses(modifiers);

        return new MovementProfile
        {
            DomainType = domainType,
            // Raw fixed-point (Scale 1,000; 1,000 = 1.0) converted to the decimal MP the movement engine uses.
            BaseMovementPoints = compiled.CampaignMovementPoints / (decimal)ScaledAmount.Scale,
            Bonuses = bonuses,
            Modifiers = modifiers
        };
    }

    public MovementState CreateState(UnitInstance force)
    {
        ArgumentNullException.ThrowIfNull(force);

        return new MovementState
        {
            // The Force stores its remaining MP as raw fixed-point (Scale 1,000), the same convention as the compiled template's
            // allowance above. Converted here, at the one boundary between storage and the movement engine, so the engine's models
            // stay in whole/fractional decimal MP throughout.
            CampaignMovementRemaining = force.CampaignMovementRemaining / (decimal)ScaledAmount.Scale
        };
    }

    private static IReadOnlyList<MovementModifier> ParseModifiers(string? modifiersJson)
    {
        if (string.IsNullOrWhiteSpace(modifiersJson))
        {
            return [];
        }

        try
        {
            // While the modifier system is evolving, support both a single JSON object and a JSON array.
            // This keeps persisted data flexible without spreading parsing concerns into the rest of the application.
            if (modifiersJson.TrimStart().StartsWith('['))
            {
                return JsonSerializer.Deserialize<List<MovementModifier>>(modifiersJson, JsonOptions) ?? [];
            }

            MovementModifier? single = JsonSerializer.Deserialize<MovementModifier>(modifiersJson, JsonOptions);
            return single is null ? [] : [single];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("The compiled design's modifier payload is not valid movement modifier JSON.", ex);
        }
    }

    private static IReadOnlySet<MoveModifier> DeriveBonuses(IReadOnlyList<MovementModifier> modifiers)
    {
        HashSet<MoveModifier> bonuses = [];

        foreach (MovementModifier modifier in modifiers)
        {
            // This derives a quick lookup set for the most common movement rules. The full modifier
            // payload is still preserved on the profile so domain strategies can evaluate more
            // detailed or conditional effects later.
            if (modifier.Effect.Equals("CapTileMovementCost", StringComparison.OrdinalIgnoreCase)
                && modifier.Conditions.Any(c =>
                    c.Type.Equals("Biome", StringComparison.OrdinalIgnoreCase)
                    && c.Value.Equals("Jungle", StringComparison.OrdinalIgnoreCase)))
            {
                bonuses.Add(MoveModifier.JungleCrasher);
            }

            if (modifier.Effect.Equals("ReduceRiverCrossingCost", StringComparison.OrdinalIgnoreCase))
            {
                bonuses.Add(MoveModifier.RiverRunner);
            }

            if (modifier.Effect.Equals("IgnoreGeographyPenalty", StringComparison.OrdinalIgnoreCase)
                && modifier.Conditions.Any(c =>
                    c.Type.Equals("Geography", StringComparison.OrdinalIgnoreCase)
                    && c.Value.Equals("Hills", StringComparison.OrdinalIgnoreCase)))
            {
                bonuses.Add(MoveModifier.IgnoreHillsPenalty);
            }
        }

        return bonuses;
    }
}
