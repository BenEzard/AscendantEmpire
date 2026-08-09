using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Entities.UnitDesigns;

using Microsoft.EntityFrameworkCore;

namespace AscendantEmpire.Tests.UnitDesigns.Shared;

/// <summary>
/// Database seeding shared by the unit-design write tests.
/// </summary>
/// <remarks>
/// Deliberately does not create the catalogue components themselves. Each fixture creates those through
/// the handler it is testing — the bulk importer in one case, the single-component create endpoint in the
/// other — and that difference is the point of the fixture, not incidental duplication.
/// </remarks>
internal static class UnitDesignSeed
{
    /// <summary>
    /// Saves a template that fits the test weapon into the test body's first slot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the arrangement every slot-reconciliation test turns on: with a template fitted, a slot
    /// delete has to take its <c>UnitTemplateComponent</c> with it, or it hits
    /// <c>FK_UnitTemplateComponent_UnitComponentSlot_UnitComponentSlotId</c>. Without one, the reconciler's
    /// interesting paths are never exercised.
    /// </para>
    /// <para>
    /// Both <see cref="UnitDesignPayloads.BODY_CODE"/> and <see cref="UnitDesignPayloads.WEAPON_CODE"/> must
    /// already exist in the database — see the remarks on the class.
    /// </para>
    /// </remarks>
    /// <param name="templateName">The saved template's display name, which the write handlers report back when a fitting is lost.</param>
    /// <returns>The body's slot ids in ascending order, which is the order the feature treats as positional.</returns>
    public static async Task<List<int>> FitWeaponIntoFirstSlotAsync(
        this IDbContextFactory<AscendantEmpireContext> dbFactory,
        string templateName)
    {
        await using AscendantEmpireContext seedContext = await dbFactory.CreateDbContextAsync();

        UnitComponent body = await seedContext.UnitComponents
            .Include(component => component.Body!).ThenInclude(bodyExtension => bodyExtension.Slots)
            .SingleAsync(component => component.Code == UnitDesignPayloads.BODY_CODE);

        int weaponId = await seedContext.UnitComponents
            .Where(component => component.Code == UnitDesignPayloads.WEAPON_CODE)
            .Select(component => component.Id)
            .SingleAsync();

        // Slot order is by id everywhere in this feature, so the "first" slot is the lowest id.
        List<int> slotIds = [.. body.Body!.Slots.Select(slot => slot.Id).Order()];

        seedContext.UnitTemplates.Add(new UnitTemplate
        {
            DisplayName = templateName,
            MapImagePath = "test.png",
            BodyComponentId = body.Id,
            Components =
            [
                new UnitTemplateComponent { UnitComponentSlotId = slotIds[0], UnitComponentId = weaponId },
            ],
        });

        await seedContext.SaveChangesAsync();

        return slotIds;
    }
}
