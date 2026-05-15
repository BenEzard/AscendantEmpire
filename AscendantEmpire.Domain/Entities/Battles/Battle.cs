using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Enums.Maps;

using System.ComponentModel.DataAnnotations;

namespace AscendantEmpire.Domain.Entities.Battles;

public class Battle
{
    public const int BATTLE_NAME_LENGTH = 250;

    public int Id { get; set; }
    public int GameId { get; set; }
    public int CampaignMapTileId { get; set; }

    [MaxLength(BATTLE_NAME_LENGTH)]
    public string? GlobalBattleName { get; set; }

    public int CommencedAtRoundNumber { get; set; }
    public TileOwnershipStatus OriginalTileStatus { get; set; }

    /// <summary>
    /// The original Tile Owner (if Status is Claimed, Owned or Occupied)
    /// </summary>
    public int? OriginalTileOwnerId { get; set; }

    public int? ConcludedAtRoundNumber { get; set; }   
    public int? VictorId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public GameIndex Game { get; set; } = null!;
    public List<BattleParticipant> BattleParticipants { get; set; } = [];
}