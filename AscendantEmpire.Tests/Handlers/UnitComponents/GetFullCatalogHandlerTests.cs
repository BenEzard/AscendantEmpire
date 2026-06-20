using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;
using AscendantEmpire.BackendContracts.UnitDesigns.GetFullCatalog;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.UnitDesigns;

using Microsoft.Extensions.Logging.Abstractions;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.Handlers.UnitComponents;

/// <summary>
/// Smoke test for the dev god-view: it returns the whole catalog (bodies, components, tags, templates)
/// with no game/player or unlock filtering.
/// </summary>
internal class GetFullCatalogHandlerTests : SqlServerTestBase
{
    [Test, Description("Returns every body, component, tag and template, regardless of unlocks.")]
    public async Task ReturnsEntireCatalogUnfilteredAsync()
    {
        await using (AscendantEmpireContext seed = await DbFactory.CreateDbContextAsync())
        {
            var tag = new Tag { Code = "amphibious", DisplayName = "Amphibious" };

            var body = new UnitComponent
            {
                Code = "test_body",
                DisplayName = "Test Body",
                ComponentType = ComponentType.Body,
                Weight = 100,
                Tags = [new UnitComponentTag { Tag = tag }],
                Body = new UnitComponentBody
                {
                    MovementDomain = DomainType.Land,
                    Slots = [new UnitComponentSlot { DisplayName = "Weapon", IsVisible = true, UIXPosition = 0, UIYPosition = 0 }],
                },
            };
            var weapon = new UnitComponent
            {
                Code = "wpn",
                DisplayName = "Test Weapon",
                ComponentType = ComponentType.Weapon,
                Weight = 10,
                Weapon = new UnitComponentWeapon { Attack = new DamageProfile { Slash = 1000 }, MinimumRange = 1, MaximumRange = 4 },
            };
            seed.UnitComponents.AddRange(body, weapon);
            await seed.SaveChangesAsync();

            seed.UnitTemplates.Add(new UnitTemplate
            {
                Code = "tmpl_global",
                DisplayName = "Global Template",
                MapImagePath = "global.png",
                BodyComponentId = body.Id,
            });
            await seed.SaveChangesAsync();
        }

        // No PlayerUnlockedComponent rows exist — the god-view returns everything anyway.
        var handler = new GetFullCatalogHandler(new NullLogger<GetFullCatalogHandler>(), DbFactory);
        ServiceResult<FullCatalogResult> result =
            await handler.Handle(new GetFullCatalogRequest(), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.StatusCode, Is.EqualTo(ServiceResultStatus.Success));

            FullCatalogResult catalog = result.SuccessValue!;
            Assert.That(catalog.Bodies, Has.Count.EqualTo(1));
            Assert.That(catalog.Bodies.Single().Slots, Has.Count.EqualTo(1), "Body slots are included.");
            Assert.That(catalog.Components, Has.Count.EqualTo(1), "The non-body weapon.");
            Assert.That(catalog.Components.Single().Weapon, Is.Not.Null);
            Assert.That(catalog.Tags.Any(t => t.Code == "amphibious"), Is.True);
            Assert.That(catalog.Templates.Any(t => t.Code == "tmpl_global"), Is.True);
        }
    }
}
