using AscendantEmpire.Application.Features.Intelligence.Classes;
using AscendantEmpire.BackendContracts.CampaignMaps;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerKnownForces;
using AscendantEmpire.BackendContracts.Intelligence.GetPlayerSightingReports;
using AscendantEmpire.Domain.Enums;
using AscendantEmpire.Domain.Enums.Intelligence;
using AscendantEmpire.Domain.Enums.Maps;

namespace AscendantEmpire.Tests.Features.Intelligence;

/// <summary>
/// Direct tests of <see cref="SightingDisclosure"/>, the redaction layer every sighting passes through on its way to a Player.
/// </summary>
/// <remarks>
/// <para>
/// No database. The design calls this layer security-relevant — sightings store the truth, so a slip here hands a Player the identity of a Force they only glimpsed — and asks for it to be tested
/// directly rather than through a handler.
/// </para>
/// <para>
/// Every level below Detail is exercised here even though <c>SightingFidelityRules.ForForce</c> is still a stub that only ever returns Detail, so the rule is proven before anything relies on it.
/// </para>
/// </remarks>
[TestFixture]
internal class SightingDisclosureTests
{
    private static readonly SightingSubject _TRUTH = new() { UnitInstanceId = 99, OwnerPlayerId = 1, UnitTemplateId = 3, HealthyTroops = 45 };

    [Test, Description("Each fidelity level discloses exactly what it earned and nothing of the level above, so a glimpse never reveals who or how strong.")]
    [TestCase(SightingFidelity.Movement, false, false, false, Description = "Movement is only that something passed: not what, not whose, not how strong.")]
    [TestCase(SightingFidelity.Type, true, false, false, Description = "Type adds what kind of Force it was.")]
    [TestCase(SightingFidelity.Owner, true, true, false, Description = "Owner adds whose it was and which Force it was.")]
    [TestCase(SightingFidelity.Detail, true, true, true, Description = "Detail adds its strength.")]
    public void ShouldDiscloseOnlyWhatTheFidelityEarned(SightingFidelity fidelity, bool expectsTemplate, bool expectsOwnerAndIdentity, bool expectsTroops)
    {
        SightingSubject disclosed = SightingDisclosure.DiscloseSubject(fidelity, _TRUTH);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(disclosed.UnitTemplateId, expectsTemplate ? Is.EqualTo(_TRUTH.UnitTemplateId) : Is.Null);
            Assert.That(disclosed.OwnerPlayerId, expectsOwnerAndIdentity ? Is.EqualTo(_TRUTH.OwnerPlayerId) : Is.Null);
            Assert.That(disclosed.UnitInstanceId, expectsOwnerAndIdentity ? Is.EqualTo(_TRUTH.UnitInstanceId) : Is.Null);
            Assert.That(disclosed.HealthyTroops, expectsTroops ? Is.EqualTo(_TRUTH.HealthyTroops) : Is.Null);
        }
    }

    [Test, Description("A fidelity that is not a defined level discloses nothing, because a redaction rule that fails open leaks on exactly the data nobody expected.")]
    [TestCase((SightingFidelity)0, Description = "An unset value.")]
    [TestCase((SightingFidelity)99, Description = "A value above Detail, which would otherwise pass every ordinal comparison.")]
    public void ShouldDiscloseNothingAboutTheSubjectWhenTheFidelityIsNotADefinedLevel(SightingFidelity fidelity)
    {
        SightingSubject disclosed = SightingDisclosure.DiscloseSubject(fidelity, _TRUTH);

        Assert.That(disclosed, Is.EqualTo(new SightingSubject()));
    }

    [Test, Description("A report is stripped of its subject's identity at low fidelity but keeps where, when and which way, which is what a glimpse genuinely saw.")]
    public void ShouldStripAReportDownToItsFidelityButKeepWhatWasSeen()
    {
        var stored = new StoredSightingReport
        {
            ReportId = 7,
            ReportType = PlayerReportType.ForceSighted,
            RaisedDuringPlayerTurnId = 40,
            RoundNumber = 2,
            Fidelity = SightingFidelity.Movement,
            Path = [new Coord(4, 1), new Coord(5, 1)],
            ExitDirection = MoveDirection.East,
            StillVisible = false,
            AppearedInView = true,
            DestroyedInView = false,
            FirstTurnSequence = 1,
            LastTurnSequence = 2,
            FirstWitnessId = 10,
            FirstWitnessRecordType = RecordType.Structure,
            FirstWitnessOwnerPlayerId = 2,
            Subject = _TRUTH
        };

        SightingReport report = SightingDisclosure.Disclose(stored);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.UnitTemplateId, Is.Null);
            Assert.That(report.OwnerPlayerId, Is.Null);
            Assert.That(report.UnitInstanceId, Is.Null);
            Assert.That(report.HealthyTroops, Is.Null);
            Assert.That(report.Path.Select(coord => (coord.X, coord.Y)), Is.EqualTo(new[] { (4, 1), (5, 1) }));
            Assert.That(report.ExitDirection, Is.EqualTo(MoveDirection.East));
            Assert.That(report.AppearedInView, Is.True);
            Assert.That(report.RoundNumber, Is.EqualTo(2));
            Assert.That(report.FirstWitnessId, Is.EqualTo(10), "The witness is the reader's own Observer, so there is nothing about it to withhold.");
        }
    }

    [Test, Description("A known Force goes through the same rule as a report, or the map layer would leak what the feed withholds.")]
    public void ShouldStripAKnownForceDownToItsFidelity()
    {
        var stored = new StoredKnownForce
        {
            Status = KnownForceStatus.Sighted,
            Fidelity = SightingFidelity.Type,
            LastSeenLocation = new Coord(5, 1),
            LastSeenPlayerTurnId = 40,
            LastSeenRoundNumber = 2,
            LastSeenTurnSequence = 4,
            ExitDirection = MoveDirection.West,
            StillVisible = false,
            Subject = _TRUTH
        };

        KnownForce knownForce = SightingDisclosure.Disclose(stored);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(knownForce.UnitTemplateId, Is.EqualTo(_TRUTH.UnitTemplateId));
            Assert.That(knownForce.OwnerPlayerId, Is.Null);
            Assert.That(knownForce.UnitInstanceId, Is.Null);
            Assert.That(knownForce.HealthyTroops, Is.Null);
            Assert.That((knownForce.LastSeenLocation.X, knownForce.LastSeenLocation.Y), Is.EqualTo((5, 1)));
            Assert.That(knownForce.LastSeenRoundNumber, Is.EqualTo(2));
        }
    }

    private static readonly SightingSubject _STRUCTURE_TRUTH = new() { StructureInstanceId = 60, StructureId = 7, OwnerPlayerId = 1, PreviousOwnerPlayerId = 2, CompletionPercent = 80 };

    [Test, Description("A Structure's kind, owners, identity and progress each wait for the level that earns them, so a glimpse of a village reveals neither whose it is nor who held it before.")]
    [TestCase(SightingFidelity.Movement, false, false, false, Description = "Movement is only that something happened there: not what, not whose, not how far built.")]
    [TestCase(SightingFidelity.Type, true, false, false, Description = "Type adds what kind of Structure it was.")]
    [TestCase(SightingFidelity.Owner, true, true, false, Description = "Owner adds whose it was, who held it before, and which one it was.")]
    [TestCase(SightingFidelity.Detail, true, true, true, Description = "Detail adds how far built it was.")]
    public void ShouldDiscloseOnlyWhatTheFidelityEarnedOfAStructure(SightingFidelity fidelity, bool expectsKind, bool expectsOwnersAndIdentity, bool expectsCompletion)
    {
        SightingSubject disclosed = SightingDisclosure.DiscloseSubject(fidelity, _STRUCTURE_TRUTH);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(disclosed.StructureId, expectsKind ? Is.EqualTo(_STRUCTURE_TRUTH.StructureId) : Is.Null);
            Assert.That(disclosed.OwnerPlayerId, expectsOwnersAndIdentity ? Is.EqualTo(_STRUCTURE_TRUTH.OwnerPlayerId) : Is.Null);
            Assert.That(disclosed.PreviousOwnerPlayerId, expectsOwnersAndIdentity ? Is.EqualTo(_STRUCTURE_TRUTH.PreviousOwnerPlayerId) : Is.Null);
            Assert.That(disclosed.StructureInstanceId, expectsOwnersAndIdentity ? Is.EqualTo(_STRUCTURE_TRUTH.StructureInstanceId) : Is.Null);
            Assert.That(disclosed.CompletionPercent, expectsCompletion ? Is.EqualTo(_STRUCTURE_TRUTH.CompletionPercent) : Is.Null);
        }
    }

    [Test, Description("A Structure report is stripped of whose and which at low fidelity, but keeps which events were seen, since seeing a village razed is what the glimpse genuinely saw.")]
    public void ShouldStripAStructureReportButKeepTheEventsSeen()
    {
        var stored = new StoredSightingReport
        {
            ReportId = 8,
            ReportType = PlayerReportType.StructureSighted,
            RaisedDuringPlayerTurnId = 40,
            RoundNumber = 2,
            Fidelity = SightingFidelity.Movement,
            Path = [new Coord(6, 3)],
            ExitDirection = null,
            StillVisible = false,
            AppearedInView = true,
            DestroyedInView = true,
            CompletedInView = false,
            CapturedInView = true,
            FirstTurnSequence = 2,
            LastTurnSequence = 9,
            FirstWitnessId = 10,
            FirstWitnessRecordType = RecordType.Unit,
            FirstWitnessOwnerPlayerId = 2,
            Subject = _STRUCTURE_TRUTH
        };

        SightingReport report = SightingDisclosure.Disclose(stored);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.StructureId, Is.Null);
            Assert.That(report.OwnerPlayerId, Is.Null);
            Assert.That(report.PreviousOwnerPlayerId, Is.Null);
            Assert.That(report.StructureInstanceId, Is.Null);
            Assert.That(report.CompletionPercent, Is.Null);
            Assert.That(report.AppearedInView, Is.True);
            Assert.That(report.DestroyedInView, Is.True);
            Assert.That(report.CapturedInView, Is.True);
            Assert.That(report.CompletedInView, Is.False);
        }
    }

    private static readonly SightingSubject _COMBAT_TRUTH = new() { BattleId = 70, GroundHeldByPlayerId = 1, TroopsLost = 120 };

    [Test, Description("An engagement's identity and ground-holder wait for Owner and its losses for Detail, so a distant watcher learns only that fighting happened there.")]
    [TestCase(SightingFidelity.Movement, false, false, Description = "Movement is only that fighting happened there.")]
    [TestCase(SightingFidelity.Type, false, false, Description = "Type adds nothing at all: an engagement has no kind to make out.")]
    [TestCase(SightingFidelity.Owner, true, false, Description = "Owner adds which engagement it was and who was left holding the ground.")]
    [TestCase(SightingFidelity.Detail, true, true, Description = "Detail adds what it cost.")]
    public void ShouldDiscloseOnlyWhatTheFidelityEarnedOfAnEngagement(SightingFidelity fidelity, bool expectsIdentityAndGroundHolder, bool expectsLosses)
    {
        SightingSubject disclosed = SightingDisclosure.DiscloseSubject(fidelity, _COMBAT_TRUTH);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(disclosed.BattleId, expectsIdentityAndGroundHolder ? Is.EqualTo(_COMBAT_TRUTH.BattleId) : Is.Null);
            Assert.That(disclosed.GroundHeldByPlayerId, expectsIdentityAndGroundHolder ? Is.EqualTo(_COMBAT_TRUTH.GroundHeldByPlayerId) : Is.Null);
            Assert.That(disclosed.TroopsLost, expectsLosses ? Is.EqualTo(_COMBAT_TRUTH.TroopsLost) : Is.Null);
        }
    }

    [Test, Description("A fidelity that is not a defined level discloses nothing of an engagement either, so the fail-closed guard covers the combat fields as well.")]
    [TestCase((SightingFidelity)0, Description = "An unset value.")]
    [TestCase((SightingFidelity)99, Description = "A value above Detail, which would otherwise pass every ordinal comparison.")]
    public void ShouldDiscloseNothingOfAnEngagementWhenTheFidelityIsNotADefinedLevel(SightingFidelity fidelity)
    {
        SightingSubject disclosed = SightingDisclosure.DiscloseSubject(fidelity, _COMBAT_TRUTH);

        Assert.That(disclosed, Is.EqualTo(new SightingSubject()));
    }

    [Test, Description("A combat report is stripped of which engagement and whose ground at low fidelity, but keeps every event seen, since watching a battle break out is a real observation.")]
    public void ShouldStripACombatReportButKeepTheEventsSeen()
    {
        var stored = new StoredSightingReport
        {
            ReportId = 9,
            ReportType = PlayerReportType.CombatSighted,
            RaisedDuringPlayerTurnId = 40,
            RoundNumber = 2,
            Fidelity = SightingFidelity.Movement,
            Path = [new Coord(4, 1)],
            ExitDirection = null,
            StillVisible = false,
            AppearedInView = true,
            DestroyedInView = true,
            SideJoinedInView = true,
            SideLeftInView = true,
            FirstTurnSequence = 3,
            LastTurnSequence = 9,
            FirstWitnessId = 10,
            FirstWitnessRecordType = RecordType.Unit,
            FirstWitnessOwnerPlayerId = 2,
            Subject = _COMBAT_TRUTH
        };

        SightingReport report = SightingDisclosure.Disclose(stored);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(report.BattleId, Is.Null);
            Assert.That(report.GroundHeldByPlayerId, Is.Null);
            Assert.That(report.TroopsLost, Is.Null);
            Assert.That(report.AppearedInView, Is.True, "The engagement was seen commencing.");
            Assert.That(report.DestroyedInView, Is.True, "The engagement was seen concluding.");
            Assert.That(report.SideJoinedInView, Is.True);
            Assert.That(report.SideLeftInView, Is.True);
        }
    }

    [Test, Description("A known Structure goes through the same rule as a report, or the map layer would leak what the feed withholds.")]
    public void ShouldStripAKnownStructureDownToItsFidelity()
    {
        var stored = new StoredKnownStructure
        {
            Status = KnownStructureStatus.Razed,
            Fidelity = SightingFidelity.Type,
            Location = new Coord(6, 3),
            LastSeenPlayerTurnId = 40,
            LastSeenRoundNumber = 2,
            LastSeenTurnSequence = 9,
            StillVisible = false,
            Subject = _STRUCTURE_TRUTH
        };

        BackendContracts.Intelligence.GetPlayerKnownStructures.KnownStructure knownStructure = SightingDisclosure.Disclose(stored);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(knownStructure.StructureId, Is.EqualTo(_STRUCTURE_TRUTH.StructureId));
            Assert.That(knownStructure.OwnerPlayerId, Is.Null);
            Assert.That(knownStructure.StructureInstanceId, Is.Null);
            Assert.That(knownStructure.CompletionPercent, Is.Null);
            Assert.That(knownStructure.Status, Is.EqualTo(KnownStructureStatus.Razed), "That it was razed is what was seen, at any level.");
            Assert.That((knownStructure.Location.X, knownStructure.Location.Y), Is.EqualTo((6, 3)));
        }
    }
}
