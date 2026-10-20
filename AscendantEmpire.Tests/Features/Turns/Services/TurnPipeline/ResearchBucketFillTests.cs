using AscendantEmpire.Application.Features.Turns.Services.TurnPipeline.Campaign.EndTurn;
using AscendantEmpire.Domain.Entities.Technologies;

namespace AscendantEmpire.Tests.Features.Turns.Services.TurnPipeline;

/// <summary>
/// Tests for <see cref="ResearchBucketFill"/> — how a pool of directed points is spread over one Technology's three buckets.
/// </summary>
/// <remarks>
/// No database. The rule is pure arithmetic over a <see cref="PlayerTechnology"/> that is never saved, so the whole table of cases is pinned here and
/// <see cref="ApplyResearchTests"/> is left to test the orchestration around it.
/// </remarks>
[TestFixture]
internal class ResearchBucketFillTests
{
    [Test, Description("Directed is filled before anything reaches the passive buckets, because directed points stand in for experience and diffusion only once the bucket they belong to is complete.")]
    public void ShouldFillDirectedBeforeTheOthers()
    {
        PlayerTechnology playerTechnology = Technology(directedRequired: 100, experienceRequired: 100, diffusionRequired: 100);

        int spent = ResearchBucketFill.Spend(playerTechnology, 100);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(spent, Is.EqualTo(100));
            Assert.That(playerTechnology.DirectedPoints, Is.EqualTo(100));
            Assert.That(playerTechnology.ExperiencePoints, Is.Zero);
            Assert.That(playerTechnology.DiffusionPoints, Is.Zero);
        }
    }

    [Test, Description("What directed cannot take spills into experience and then diffusion of the same Technology: the design has directed research completing a technology on its own.")]
    public void ShouldSpillIntoExperienceThenDiffusion()
    {
        PlayerTechnology playerTechnology = Technology(directedRequired: 100, experienceRequired: 50, diffusionRequired: 50);

        int spent = ResearchBucketFill.Spend(playerTechnology, 175);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(spent, Is.EqualTo(175));
            Assert.That(playerTechnology.DirectedPoints, Is.EqualTo(100));
            Assert.That(playerTechnology.ExperiencePoints, Is.EqualTo(50), "Filled before diffusion saw anything.");
            Assert.That(playerTechnology.DiffusionPoints, Is.EqualTo(25));
        }
    }

    [Test, Description("A pool big enough to fill all three completes the Technology from directed research alone, with no experience or diffusion ever having accrued.")]
    public void ShouldCompleteEveryBucketFromOnePool()
    {
        PlayerTechnology playerTechnology = Technology(directedRequired: 100, experienceRequired: 50, diffusionRequired: 50);

        int spent = ResearchBucketFill.Spend(playerTechnology, 200);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(spent, Is.EqualTo(200));
            Assert.That(ResearchBucketFill.IsComplete(playerTechnology), Is.True);
        }
    }

    [Test, Description("Points beyond what every bucket needs are left unspent rather than pushed past a requirement, because the overflow calculation works from what remains and a negative remainder breaks it.")]
    public void ShouldLeaveWhatNoBucketCanTake()
    {
        PlayerTechnology playerTechnology = Technology(directedRequired: 100, experienceRequired: 50, diffusionRequired: 50);

        int spent = ResearchBucketFill.Spend(playerTechnology, 500);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(spent, Is.EqualTo(200), "The 300 that had nowhere to go is the caller's to lose.");
            Assert.That(playerTechnology.DirectedPoints, Is.EqualTo(100));
            Assert.That(playerTechnology.ExperiencePoints, Is.EqualTo(50));
            Assert.That(playerTechnology.DiffusionPoints, Is.EqualTo(50));
        }
    }

    [Test, Description("A bucket with no requirement takes nothing: an unused bucket has no requirement at all rather than a requirement of zero, so points must skip it rather than pile up where nothing will ever read them.")]
    public void ShouldPutNothingIntoAnUnusedBucket()
    {
        PlayerTechnology playerTechnology = Technology(directedRequired: 100, experienceRequired: null, diffusionRequired: 50);

        int spent = ResearchBucketFill.Spend(playerTechnology, 200);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(spent, Is.EqualTo(150));
            Assert.That(playerTechnology.ExperiencePoints, Is.Zero);
            Assert.That(playerTechnology.DiffusionPoints, Is.EqualTo(50), "Diffusion was reached even though the bucket before it was skipped.");
        }
    }

    [Test, Description("A bucket already at its requirement takes nothing rather than a negative amount, which is what stops a part-researched Technology losing points it had already banked.")]
    public void ShouldTakeNothingIntoAnAlreadyFullBucket()
    {
        PlayerTechnology playerTechnology = Technology(directedRequired: 100, experienceRequired: 100, diffusionRequired: null);
        playerTechnology.DirectedPoints = 100;

        int spent = ResearchBucketFill.Spend(playerTechnology, 40);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(spent, Is.EqualTo(40));
            Assert.That(playerTechnology.DirectedPoints, Is.EqualTo(100), "Untouched.");
            Assert.That(playerTechnology.ExperiencePoints, Is.EqualTo(40));
        }
    }

    [Test, Description("A pool smaller than the first bucket's headroom all lands there, leaving the Technology part-researched rather than spreading thinly across three.")]
    public void ShouldPutASmallPoolEntirelyIntoTheFirstBucket()
    {
        PlayerTechnology playerTechnology = Technology(directedRequired: 1_000, experienceRequired: 250, diffusionRequired: 250);
        playerTechnology.DirectedPoints = 400;

        int spent = ResearchBucketFill.Spend(playerTechnology, 120);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(spent, Is.EqualTo(120));
            Assert.That(playerTechnology.DirectedPoints, Is.EqualTo(520));
            Assert.That(ResearchBucketFill.IsComplete(playerTechnology), Is.False);
        }
    }

    [Test, Description("An empty pool changes nothing, so a Player who generated no points this Round is not quietly moved along.")]
    public void ShouldSpendNothingFromAnEmptyPool()
    {
        PlayerTechnology playerTechnology = Technology(directedRequired: 100, experienceRequired: 100, diffusionRequired: 100);

        int spent = ResearchBucketFill.Spend(playerTechnology, 0);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(spent, Is.Zero);
            Assert.That(playerTechnology.DirectedPoints, Is.Zero);
        }
    }

    [Test, Description("A Technology using only the directed bucket is complete once that bucket is full, because the two it does not use are satisfied by definition rather than by being zero.")]
    public void ShouldReadAsCompleteWhenTheOnlyUsedBucketIsFull()
    {
        PlayerTechnology playerTechnology = Technology(directedRequired: 100, experienceRequired: null, diffusionRequired: null);

        ResearchBucketFill.Spend(playerTechnology, 100);

        Assert.That(ResearchBucketFill.IsComplete(playerTechnology), Is.True);
    }

    [Test, Description("A Technology is not complete while any bucket it uses is short, however full the others are.")]
    public void ShouldNotReadAsCompleteWhileABucketIsShort()
    {
        PlayerTechnology playerTechnology = Technology(directedRequired: 100, experienceRequired: 100, diffusionRequired: null);

        ResearchBucketFill.Spend(playerTechnology, 150);

        Assert.That(ResearchBucketFill.IsComplete(playerTechnology), Is.False);
    }

    // ── arrangement ───────────────────────────────────────────────────────────

    private static PlayerTechnology Technology(int? directedRequired, int? experienceRequired, int? diffusionRequired) =>
        new()
        {
            PlayerId = 1,
            TechnologyId = 1,
            GameId = 1,
            DirectedRequired = directedRequired,
            ExperienceRequired = experienceRequired,
            DiffusionRequired = diffusionRequired
        };
}
