using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Turns.Services;
using AscendantEmpire.Application.Features.UnitComponents.Handlers;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using static AscendantEmpire.Application.Common.ServiceResultBase;

namespace AscendantEmpire.Tests.UnitDesigns.Shared;

/// <summary>
/// Builds the unit-design write handlers with their collaborators stubbed, for the fixtures that drive
/// them directly rather than through the API.
/// </summary>
internal static class UnitDesignHandlers
{
    /// <summary>
    /// Builds a <see cref="BulkUpsertUnitComponentsHandler"/> over a fresh context from the fixture's factory.
    /// </summary>
    public static async Task<BulkUpsertUnitComponentsHandler> CreateBulkUpsertHandlerAsync(
        this IDbContextFactory<AscendantEmpireContext> dbFactory)
    {
        AscendantEmpireContext context = await dbFactory.CreateDbContextAsync();

        // These tests are about the component graph, not the snapshots downstream of it — SaveUnitTemplateCompilationTests
        // covers those for real. The stub still has to answer the compile request rather than being left bare, though:
        // AffectedTemplateRecompiler sends one CompileUnitTemplateRequest per template built on an edited component and
        // reads the result, so an unconfigured substitute fails every test that seeds a fitted template on the recompile
        // instead of on what it set out to assert.
        IMediator mediator = Substitute.For<IMediator>();
        mediator.Send(Arg.Any<CompileUnitTemplateRequest>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<int>.Succeed(0));

        return new BulkUpsertUnitComponentsHandler(
            new NullLogger<BulkUpsertUnitComponentsHandler>(),
            context,
            mediator,
            Substitute.For<ICampaignTurnService>());
    }
}
