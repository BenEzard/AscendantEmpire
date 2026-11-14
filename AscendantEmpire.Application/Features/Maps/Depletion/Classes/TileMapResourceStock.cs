using AscendantEmpire.Application.Helpers.ScaledNumerics;

namespace AscendantEmpire.Application.Features.Maps.Depletion.Classes;

/// <summary>
/// One Asset a MapResource instance standing on a tile holds, as the caller read it out of the database.
/// </summary>
/// <remarks>
/// The caller assembles these so <see cref="Services.TileFoodSources"/> can stay pure: the instance's quantity has already been multiplied by the type's
/// yield rate, and the Player's own switches have already decided whether the yield is on.
/// </remarks>
/// <param name="Available">
/// How much of the Asset the instance holds, or <see langword="null"/> when it holds its amount in child rows instead - a Distribution or a stratified
/// resource, which is skipped rather than read as a patch stripped bare.
/// </param>
/// <param name="IsYieldEnabled">Whether this Player can take this yield at all, the type's default as the Player's switches leave it.</param>
internal sealed record TileMapResourceStock(int MapResourceInstanceId, int AssetId, ScaledAmount? Available, bool IsYieldEnabled);
