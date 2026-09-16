using AscendantEmpire.Application.Helpers.ScaledNumerics;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Resources;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Services;

/// <summary>
/// Reads and adds to the amount taken of one resource on a <see cref="CampaignMapDepletion"/> row.
/// </summary>
/// <remarks>
/// The one place a <see cref="SustenanceResource"/> is mapped to its <c>*Taken</c> column, so gathering (which writes it) and the tile toolbar read (which shows what is left)
/// cannot disagree about which column holds which resource.
/// </remarks>
internal static class DepletionTaken
{
    /// <summary>
    /// How much of <paramref name="resource"/> has been taken from the tile.
    /// </summary>
    /// <param name="depletion">The tile's depletion row, or <see langword="null"/> when the tile has never been fed on - in which case nothing has been taken.</param>
    /// <param name="resource">The resource to read.</param>
    public static ScaledAmount GetTaken(CampaignMapDepletion? depletion, SustenanceResource resource)
    {
        if (depletion is null)
        {
            return ScaledAmount.Zero;
        }

        long takenRaw = resource switch
        {
            SustenanceResource.Forage => depletion.ForageTaken,
            SustenanceResource.Hunt => depletion.HuntTaken,
            SustenanceResource.Fish => depletion.FishTaken,
            SustenanceResource.Fodder => depletion.FodderTaken,
            _ => throw new ArgumentOutOfRangeException(nameof(resource), resource, "Unknown sustenance resource.")
        };

        return ScaledAmount.FromRaw(takenRaw);
    }

    /// <summary>
    /// Adds <paramref name="amount"/> to what has been taken of <paramref name="resource"/> from the tile.
    /// </summary>
    /// <param name="depletion">The tile's depletion row.</param>
    /// <param name="resource">The resource taken.</param>
    /// <param name="amount">The amount taken. Must not be negative: taking only ever adds, and nothing gives food back to a tile this way.</param>
    public static void AddTaken(CampaignMapDepletion depletion, SustenanceResource resource, ScaledAmount amount)
    {
        if (amount < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "An amount taken cannot be negative.");
        }

        // checked: an overflow here would wrap to a negative Taken, which the check constraint would only catch at save time and far from the cause.
        checked
        {
            switch (resource)
            {
                case SustenanceResource.Forage:
                {
                    depletion.ForageTaken += amount.RawValue;
                    break;
                }
                case SustenanceResource.Hunt:
                {
                    depletion.HuntTaken += amount.RawValue;
                    break;
                }
                case SustenanceResource.Fish:
                {
                    depletion.FishTaken += amount.RawValue;
                    break;
                }
                case SustenanceResource.Fodder:
                {
                    depletion.FodderTaken += amount.RawValue;
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(resource), resource, "Unknown sustenance resource.");
                }
            }
        }
    }

    /// <summary>
    /// Replaces what has been taken of <paramref name="resource"/> from the tile with <paramref name="amount"/>.
    /// </summary>
    /// <remarks>
    /// For regeneration, which works out a whole new <c>Taken</c> rather than adding to it. Feeding adds through <see cref="AddTaken"/>.
    /// </remarks>
    /// <param name="depletion">The tile's depletion row.</param>
    /// <param name="resource">The resource to set.</param>
    /// <param name="amount">The new amount taken. Must not be negative, matching the table's check constraint.</param>
    public static void SetTaken(CampaignMapDepletion depletion, SustenanceResource resource, ScaledAmount amount)
    {
        if (amount < ScaledAmount.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "An amount taken cannot be negative.");
        }

        switch (resource)
        {
            case SustenanceResource.Forage:
            {
                depletion.ForageTaken = amount.RawValue;
                break;
            }
            case SustenanceResource.Hunt:
            {
                depletion.HuntTaken = amount.RawValue;
                break;
            }
            case SustenanceResource.Fish:
            {
                depletion.FishTaken = amount.RawValue;
                break;
            }
            case SustenanceResource.Fodder:
            {
                depletion.FodderTaken = amount.RawValue;
                break;
            }
            default:
            {
                throw new ArgumentOutOfRangeException(nameof(resource), resource, "Unknown sustenance resource.");
            }
        }
    }
}
