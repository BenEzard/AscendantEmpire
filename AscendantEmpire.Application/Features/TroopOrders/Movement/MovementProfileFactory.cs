using System.Text.Json;

using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Modifiers;

namespace AscendantEmpire.Application.Features.TroopOrders.Movement;

/// <summary>
/// Central place for converting loaded troop and compiled design data into movement models.
/// Keeping this mapping here prevents handlers and planners from needing to know how modifier
/// JSON is shaped or how common bonuses are inferred from that JSON.
/// </summary>
internal sealed class MovementProfileFactory : IMovementProfileFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MovementProfile Create(TroopInstance troop, DomainType domainType)
    {
        ArgumentNullException.ThrowIfNull(troop);
        ArgumentNullException.ThrowIfNull(troop.UnitTemplate);
        ArgumentNullException.ThrowIfNull(troop.UnitTemplate.Compiled);

        var compiled = troop.UnitTemplate.Compiled;

        // The compiled template snapshot does not carry movement-modifier JSON yet (the legacy
        // CompiledDesignStats.ModifiersJson has no template-model equivalent), so template troops move
        // un-modified for now. The parsing below is kept wired so the JSON slots straight back in when it
        // is compiled onto the snapshot — see the military-organisation implementation notes.
        string? modifiersJson = null;

        IReadOnlyList<MovementModifier> modifiers = ParseModifiers(modifiersJson);
        IReadOnlySet<MoveModifier> bonuses = DeriveBonuses(modifiers);

        return new MovementProfile
        {
            DomainType = domainType,
            // Raw fixed-point (Scale 1,000; 1,000 = 1.0) converted to the decimal MP the movement engine uses.
            BaseMovementPoints = compiled.CampaignMovementPoints / 1000m,
            Bonuses = bonuses,
            Modifiers = modifiers
        };
    }

    public MovementState CreateState(TroopInstance troop)
    {
        ArgumentNullException.ThrowIfNull(troop);

        return new MovementState
        {
            MovementPointsRemaining = troop.MovementPointsRemaining
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
