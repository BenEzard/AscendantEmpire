using AscendantEmpire.BackendContracts.Assets.UpsertSupplySet;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Enums.Assets;

namespace AscendantEmpire.Application.Features.Assets.Classes;

/// <summary>
/// Shared shape validation for one supply set upsert, used by both the create and update handlers.
/// </summary>
/// <remarks>
/// Whether an Asset code resolves, and whether a set's code is already taken, are checked by each handler against the current rows rather than here.
/// </remarks>
internal static class SupplySetValidation
{
    /// <summary>
    /// Returns every shape problem with the set, or an empty list when it is valid.
    /// </summary>
    public static List<string> Validate(UpsertSupplySetDto set)
    {
        List<string> errors = [];

        if (string.IsNullOrWhiteSpace(set.Code))
        {
            errors.Add("The supply set is missing its Code.");
        }
        else if (set.Code.Length > SupplySet.CODE_MAX_LENGTH)
        {
            errors.Add($"Supply set code '{set.Code}' exceeds {SupplySet.CODE_MAX_LENGTH} characters.");
        }

        if (string.IsNullOrWhiteSpace(set.DisplayName))
        {
            errors.Add("The supply set is missing its DisplayName.");
        }
        else if (set.DisplayName.Length > SupplySet.DISPLAY_NAME_MAX_LENGTH)
        {
            errors.Add($"Supply set DisplayName exceeds {SupplySet.DISPLAY_NAME_MAX_LENGTH} characters.");
        }

        if (!Enum.IsDefined(set.SupplyType))
        {
            errors.Add("The supply set has an undefined SupplyType value.");
        }

        // A set nothing can be drawn or spent from is not a set: a requirement naming it could never be satisfied.
        if (set.Assets.Count == 0)
        {
            errors.Add("A supply set must hold at least one Asset.");

            return errors;
        }

        errors.AddRange(ValidateMembers(set.Assets));

        // Only a Repair set is drawn from, so only it needs a member the draw can land on.
        if (set.SupplyType == SupplyType.Repair && set.Assets.All(asset => asset.SelectionWeight <= 0))
        {
            errors.Add("A Repair supply set is drawn from, so at least one of its Assets must carry a SelectionWeight above 0.");
        }

        return errors;
    }

    /// <summary>
    /// Returns every problem with the set's members — their codes, costs and weights.
    /// </summary>
    private static List<string> ValidateMembers(IReadOnlyCollection<UpsertSupplySetAssetDto> members)
    {
        List<string> errors = [];

        foreach (UpsertSupplySetAssetDto member in members)
        {
            if (string.IsNullOrWhiteSpace(member.AssetCode))
            {
                errors.Add("A supply set member is missing its AssetCode.");

                continue;
            }

            // The divisor wherever a need is turned into an amount of an Asset, so it cannot be zero.
            if (member.AmountPerUnit <= 0)
            {
                errors.Add($"Supply set member '{member.AssetCode}' needs an AmountPerUnit above 0.");
            }

            // Zero is legitimate - it excludes a member from the draw without removing it from the set.
            if (member.SelectionWeight < 0)
            {
                errors.Add($"Supply set member '{member.AssetCode}' cannot have a negative SelectionWeight.");
            }
        }

        string[] duplicateCodes =
        [
            .. members
                .Where(member => !string.IsNullOrWhiteSpace(member.AssetCode))
                .GroupBy(member => member.AssetCode, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .Order()
        ];

        foreach (string duplicateCode in duplicateCodes)
        {
            errors.Add($"Asset '{duplicateCode}' is listed more than once in the supply set.");
        }

        return errors;
    }
}
