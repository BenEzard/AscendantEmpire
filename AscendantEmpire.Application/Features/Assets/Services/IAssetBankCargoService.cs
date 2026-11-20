using AscendantEmpire.Application.Common;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Application.Features.Assets.Classes;
using AscendantEmpire.Domain.Entities.Assets;

namespace AscendantEmpire.Application.Features.Assets.Services;

/// <summary>
/// Puts an Asset into a Force's cargo, and is the only route by which anything gets there.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why not <see cref="IAssetBankCreditService"/>.</b> That service places a credit across the network a Settlement sits in, levelling it across the banks
/// holding least and clamping each to what that Settlement can store - and it filters cargo out of every route on purpose, because production must never
/// land in a load and a Force standing inside a Settlement must not fill its storehouse. None of its three mechanisms has anything to say about cargo: a
/// unit bank is a network of one, has no Settlement for a capacity to bind to, and takes what it is given whole. Threading a second mode through it would
/// put the exclusion rule and the thing it excludes in one class.
/// </para>
/// <para>
/// <b>Carrying is unlimited today</b>, so nothing here clamps and nothing is reported unplaced. A Force's capacity is meant to be its healthy individuals
/// times the per-individual carry weight of the template it was built from, and neither <c>Asset.Weight</c> nor a compiled carry weight exists yet (RS-41).
/// When they do, the clamp belongs here, which is why this answers with a result rather than returning void.
/// </para>
/// <para>
/// <b>Works on the caller's context and does not save</b>, so a capture provisions the bank and credits it in one unit of work - the same contract
/// <see cref="IAssetBankProvisioningService"/> keeps.
/// </para>
/// <para>See "Force cargo" in <c>docs/design/asset-banks.md</c>.</para>
/// </remarks>
internal interface IAssetBankCargoService
{
    /// <summary>
    /// Stages every credit into the bank, adding a balance row for an Asset it is not carrying yet.
    /// </summary>
    /// <param name="bank">
    /// The cargo bank to load, from <see cref="IAssetBankProvisioningService.ProvisionForUnitAsync"/>. It may still be unsaved, in which case the balances
    /// are joined through its navigation and EF picks up the generated key on the caller's save.
    /// </param>
    /// <param name="credits">
    /// What is arriving. Several credits of one Asset are added together, so a caller taking the same Asset from two sources need not pool them first. A
    /// credit of nothing or less is ignored rather than refused: a yield whose share rounded away to nothing is an ordinary outcome, not a caller error.
    /// </param>
    /// <returns>
    /// <c>InternalServerError</c> when the bank is not a cargo bank. Crediting a Settlement's stockpile through here would bypass its capacity entirely, so
    /// it is refused rather than quietly allowed.
    /// </returns>
    Task<ServiceResultBase> CreditAsync(
        AscendantEmpireContext context,
        AssetBank bank,
        IReadOnlyCollection<CargoCredit> credits,
        CancellationToken cancelToken = default);
}
