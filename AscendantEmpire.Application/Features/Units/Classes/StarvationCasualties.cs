namespace AscendantEmpire.Application.Features.Units.Classes;

/// <summary>
/// What one Turn of going short of food costs a Force.
/// </summary>
/// <remarks>
/// The two are a split of one total rather than two independent figures: the total is decided by how far short of food the Force went, and only the division
/// between dying and being wounded is left to chance.
/// </remarks>
/// <param name="Killed">Troops who die.</param>
/// <param name="Wounded">Troops who are wounded but survive.</param>
internal readonly record struct StarvationCasualties(int Killed, int Wounded)
{
    /// <summary>
    /// A Force that got away with it.
    /// </summary>
    public static StarvationCasualties None => new(0, 0);

    /// <summary>
    /// Every troop the shortfall accounted for, dead or wounded.
    /// </summary>
    public int Total => Killed + Wounded;
}
