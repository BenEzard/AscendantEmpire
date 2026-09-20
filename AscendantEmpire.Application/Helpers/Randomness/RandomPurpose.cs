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
/// Values are assigned explicitly and must <b>never</b> be reused or renumbered. The number goes into the seed, so changing one silently changes every roll
/// that purpose has ever made.
/// </para>
/// </remarks>
public enum RandomPurpose
{
    /// <summary>Deaths and injuries taken by a Force that went short of food at the end of its owner's Turn.</summary>
    StarvationCasualties = 1000,
}
