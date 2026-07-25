using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetCompiledUnitTemplate;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.UnitComponents;

/// <summary>
/// Integration tests for compiling a template into its denormalized snapshot, and reading that snapshot
/// back without the unlock filter (the case that makes enemy-design analysis possible).
/// </summary>
internal class CompileUnitTemplateHandlerTests : SqlServerTestBase
{
    private int _templateId;
    private int _equipmentComponentId;

    /// <summary>
    /// Seeds a global template: a body (with crush + an "amphibious" tag and three slots) fitted with a
    /// weapon, an armour and an equipment carrying a "settler" capability tag. No player unlocks are
    /// seeded — the compiled read must work without them.
    /// </summary>
    private async Task SeedAsync()
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();

        var amphibious = new Tag { Code = "amphibious", DisplayName = "Amphibious", TagType = TagType.Ability };
        var settler = new Tag { Code = "settler", DisplayName = "Settler", TagType = TagType.Combat };

        var body = new UnitComponent
        {
            Code = "test_body",
            DisplayName = "Test Body",
            ComponentType = ComponentType.Body,
            Weight = 100,
            Detection = 1000,
            Visibility = 2000,
            Tags = [new UnitComponentTag { Tag = amphibious }],
            Body = new UnitComponentBody
            {
                MovementDomain = DomainType.Land,
                // Raw fixed-point (Scale 1,000): 3,000 = 3.0 campaign, 6,000 = 6.0 battlefield movement points.
                // The compile step unscales these into the snapshot's decimals (asserted as 3m below).
                CampaignMovementPoints = 3000,
                BattlefieldMovementPoints = 6000,
            },
            // A body may carry its own inherent weapon (here a crush attack), which the compiler folds in.
            Weapon = new UnitComponentWeapon
            {
                Attack = new DamageProfile { Crush = 200 },
                MinimumRange = 0,
                MaximumRange = 1,
            },
            // Only bodies expose slots, so they live on the body extension.
        };
        body.Body!.Slots =
        [
            new UnitComponentSlot { DisplayName = "Weapon", IsVisible = true, UIXPosition = 0, UIYPosition = 0 },
            new UnitComponentSlot { DisplayName = "Armour", IsVisible = true, UIXPosition = 1, UIYPosition = 0 },
            new UnitComponentSlot { DisplayName = "Utility", IsVisible = true, UIXPosition = 2, UIYPosition = 0 },
        ];

        var weapon = new UnitComponent
        {
            Code = "wpn",
            DisplayName = "Test Weapon",
            ComponentType = ComponentType.Weapon,
            Weight = 10,
            Detection = 500,
            Visibility = 700,
            Weapon = new UnitComponentWeapon
            {
                Attack = new DamageProfile { Slash = 1000, Pierce = 500, Crush = 50 },
                MinimumRange = 1,
                MaximumRange = 4,
            },
        };

        var armour = new UnitComponent
        {
            Code = "arm",
            DisplayName = "Test Armour",
            ComponentType = ComponentType.Armour,
            Weight = 30,
            Armour = new UnitComponentArmour { Resistance = new DamageProfile { Slash = 100, Pierce = 250 } },
        };

        var equipment = new UnitComponent
        {
            Code = "camping_goods",
            DisplayName = "Camping Goods",
            ComponentType = ComponentType.Equipment,
            Weight = 5,
            Tags = [new UnitComponentTag { Tag = settler }],
        };

        context.UnitComponents.AddRange(body, weapon, armour, equipment);
        await context.SaveChangesAsync();

        List<UnitComponentSlot> slots = [.. body.Body!.Slots.OrderBy(s => s.UIXPosition)];

        var template = new UnitTemplate
        {
            Code = "tmpl_global",
            DisplayName = "Global Template",
            MapImagePath = "global.png",
            BodyComponentId = body.Id,
            Components =
            [
                new UnitTemplateComponent { UnitComponentSlotId = slots[0].Id, UnitComponentId = weapon.Id },
                new UnitTemplateComponent { UnitComponentSlotId = slots[1].Id, UnitComponentId = armour.Id },
                new UnitTemplateComponent { UnitComponentSlotId = slots[2].Id, UnitComponentId = equipment.Id },
            ],
        };
        context.UnitTemplates.Add(template);
        await context.SaveChangesAsync();

        _templateId = template.Id;
        _equipmentComponentId = equipment.Id;
    }

    private async Task<ServiceResult<int>> CompileAsync(int compiledOnTurn)
    {
        await using AscendantEmpireContext context = await DbFactory.CreateDbContextAsync();
        var handler = new CompileUnitTemplateHandler(new NullLogger<CompileUnitTemplateHandler>(), context);
        return await handler.Handle(new CompileUnitTemplateRequest(_templateId, compiledOnTurn), CancellationToken.None);
    }

    [Test, Description("Compiling aggregates body + fitted stats into typed columns, and captures resolved components and capabilities.")]
    public async Task CompileWritesAggregatedSnapshotAsync()
    {
        await SeedAsync();

        ServiceResult<int> result = await CompileAsync(compiledOnTurn: 5);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        CompiledUnitTemplate snapshot = await verify.CompiledUnitTemplates
            .Include(c => c.Components)
            .Include(c => c.Capabilities)
            .SingleAsync(c => c.UnitTemplateId == _templateId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));

            // Aggregates: weights/detection/visibility summed across body + 3 fitted.
            Assert.That(snapshot.TotalWeight, Is.EqualTo(145), "100 + 10 + 30 + 5.");
            Assert.That(snapshot.TotalDetection, Is.EqualTo(1500), "1000 + 500.");
            Assert.That(snapshot.TotalVisibility, Is.EqualTo(2700), "2000 + 700.");

            // Attack = fitted weapon attack, with the body's own inherent weapon folded into Crush.
            Assert.That(snapshot.Attack.Slash, Is.EqualTo(1000));
            Assert.That(snapshot.Attack.Crush, Is.EqualTo(250), "Fitted weapon crush 50 + body's inherent weapon crush 200.");
            Assert.That(snapshot.Resistance.Pierce, Is.EqualTo(250), "From the fitted armour.");

            // Body-derived movement. Movement is copied through as the raw fixed-point long (3,000 = 3.0).
            Assert.That(snapshot.MovementDomain, Is.EqualTo(DomainType.Land));
            Assert.That(snapshot.CampaignMovementPoints, Is.EqualTo(3000));
            Assert.That(snapshot.CompiledOnTurn, Is.EqualTo(5));

            // Components: body (null slot) + 3 fitted.
            Assert.That(snapshot.Components, Has.Count.EqualTo(4));
            Assert.That(snapshot.Components.Count(c => c.UnitComponentSlotId == null), Is.EqualTo(1), "The body sits in no slot.");

            // Capabilities: the union of the body's and equipment's tags.
            Assert.That(snapshot.Capabilities.Select(c => c.TagCode), Is.EquivalentTo(new[] { "amphibious", "settler" }));
        }
    }

    [Test, Description("The compiled read returns the snapshot with NO unlock filter — even components no player has unlocked.")]
    public async Task CompiledReadIgnoresUnlocksAsync()
    {
        await SeedAsync();
        await CompileAsync(compiledOnTurn: 1);

        // No PlayerUnlockedComponent rows were ever seeded, yet the compiled read still returns everything.
        var handler = new GetCompiledUnitTemplateHandler(new NullLogger<GetCompiledUnitTemplateHandler>(), DbFactory);
        ServiceResult<CompiledUnitTemplateView> result =
            await handler.Handle(new GetCompiledUnitTemplateRequest(_templateId), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));

            CompiledUnitTemplateView view = result.SuccessValue!;
            Assert.That(view.Components.Any(c => c.UnitComponentId == _equipmentComponentId), Is.True,
                "A component no player unlocked still appears in the compiled (enemy-viewable) snapshot.");
            Assert.That(view.Capabilities.Any(c => c.TagCode == "settler"), Is.True);
        }
    }

    [Test, Description("Recompiling upserts in place — no duplicate snapshot rows or children.")]
    public async Task RecompileReplacesSnapshotWithoutDuplicatesAsync()
    {
        await SeedAsync();

        await CompileAsync(compiledOnTurn: 1);
        await CompileAsync(compiledOnTurn: 2);

        await using AscendantEmpireContext verify = await DbFactory.CreateDbContextAsync();
        int snapshotCount = await verify.CompiledUnitTemplates.CountAsync(c => c.UnitTemplateId == _templateId);
        CompiledUnitTemplate snapshot = await verify.CompiledUnitTemplates
            .Include(c => c.Components)
            .Include(c => c.Capabilities)
            .SingleAsync(c => c.UnitTemplateId == _templateId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(snapshotCount, Is.EqualTo(1), "Still one snapshot row.");
            Assert.That(snapshot.Components, Has.Count.EqualTo(4), "Children replaced, not appended.");
            Assert.That(snapshot.Capabilities, Has.Count.EqualTo(2));
            Assert.That(snapshot.CompiledOnTurn, Is.EqualTo(2), "Reflects the latest compile.");
        }
    }
}
