namespace AscendantEmpire.Application.Helpers.Randomness;

/// <summary>
/// What a derived random stream is being drawn for.
/// </summary>
/// <remarks>
/// <para>
/// Every roll in a Game derives its own seed from that Game's <c>GenerationSeed</c> rather than drawing from one shared sequence, and the purpose is part of
/// that derivation. Two purposes therefore never influence one another: adding a roll to one leaves every other stream producing exactly the values it
/// produced before, which is what lets a new mechanic be built without disturbing the Games already in progress.
/// </para>
/// <para>
/// Values are assigned explicitly and must <b>NEVER</b> be reused or renumbered. The number goes into the seed, so changing one silently changes every roll
/// that purpose has ever made.
/// </para>
/// </remarks>
public enum RandomPurpose
{
    /// <summary>Deaths and injuries taken by a Force that went short of food at the end of its owner's Turn.</summary>
    StarvationCasualties = 1000,

    /// <summary>The quantity a single-quantity MapResource instance is created with, where the map editor did not set one itself.</summary>
    MapResourceInitialQuantity = 2000,

    /// <summary>Which of its MapResourceType's Distribution scenarios an instance is created from, weighted by its tile's Biome.</summary>
    MapResourceDistributionScenario = 2001,

    /// <summary>How many spreads a MapResource instance is created able to make.</summary>
    MapResourceSpreadLimit = 2002,

    /// <summary>How far off the quantity shown to Players can be for a MapResource instance.</summary>
    MapResourceEstimationFuzzyFactor = 2003,

    /// <summary>Whether each layer of a stratified MapResource instance is empty, what it holds, and how well it can be judged.</summary>
    MapResourceLayerContents = 2004,
}
