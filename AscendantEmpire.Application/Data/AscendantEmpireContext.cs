using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Entities.Util;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using System.Text.Json;

namespace AscendantEmpire.Application.Data;

public class AscendantEmpireContext : DbContext
{
    #region Assets Schema
    public DbSet<ResourceMetaCategory> ResourceMetaCategories { get; set; }
    public DbSet<ResourceCategory> ResourceCategories { get; set; }
    public DbSet<ResourceGroup> ResourceGroups { get; set; }
    public DbSet<Resource> Resources { get; set; }
    public DbSet<FactionResourceState> FactionResourceStates { get; set; }
    public DbSet<FactionResourcePriority> FactionResourcePriorities { get; set; }
    public DbSet<ResourceBank> ResourceBanks { get; set; }
    public DbSet<ResourceBankBalance> ResourceBankBalances { get; set; }
    public DbSet<ResourceBankSettlementMembership> ResourceBankSettlementMemberships { get; set; }
    public DbSet<WorldResourceSnapshot> WorldResourceSnapshots { get; set; }
    #endregion

    #region Battle Schema
    public DbSet<Battle> Battles { get; set; }
    public DbSet<BattleParticipant> BattleParticipants { get; set; }
    #endregion

    #region Map Schema
    /// <summary>
    /// Default set of Biomes on Campaign Maps.
    /// </summary>
    public DbSet<CampaignBiome> CampaignBiomes { get; set; }

    /// <summary>
    /// Default set of Geographies on Campaign Maps.
    /// </summary>
    public DbSet<CampaignGeography> CampaignGeographies { get; set; }

    /// <summary>
    /// Gallery collection of all available Campaign Maps which can be chosen for new games.
    /// </summary>
    public DbSet<CampaignMapGallery> CampaignMapGallery { get; set; }

    /// <summary>
    /// Index of available Campaign Maps in the Gallery.
    /// </summary>
    public DbSet<CampaignMapGalleryIndex> CampaignMapGalleryIndex { get; set; }
    #endregion

    #region Game Schema
    /// <summary>
    /// Index of current games.
    /// </summary>
    public DbSet<GameIndex> GameIndex { get; set; }

    /// <summary>
    /// List of Players in specific games.
    /// </summary>
    public DbSet<Player> Players { get; set; }

    /// <summary>
    /// Master copy of Campaign Maps for specific games.
    /// </summary>
    public DbSet<CampaignMap> CampaignMaps { get; set; }

    /// <summary>
    /// Player-specific copy of Campaign Maps for specific games.
    /// </summary>
    public DbSet<PlayerCampaignMap> PlayerCampaignMaps { get; set; }

    public DbSet<PlayerKnownStructure> PlayerStructures { get; set; }
    public DbSet<StructureInstance> StructureInstances { get; set; }
    #endregion

    #region Units

    public DbSet<TroopInstance> TroopInstances { get; set; }
    public DbSet<TroopOrder> TroopOrders { get; set; }
    public DbSet<TroopOrderMovementStep> TroopOrderMovementSteps { get; set; }
    public DbSet<TroopOrderParticipant> TroopOrderParticipants { get; set; }

    #endregion

    #region Unit Designs
    public DbSet<UnitDesign> UnitDesigns { get; set; }
    public DbSet<CompiledDesignStats> UnitDesignStats { get; set; }
    public DbSet<PlayerUnitDesignState> PlayerUnitDesignStates { get; set; }
    #endregion

    #region Turn Schema
    public DbSet<TurnQueue> TurnQueues { get; set; }
    public DbSet<TurnQueueParticipant> TurnQueueParticipants { get; set; }
    #endregion

    #region Util Schema
    public DbSet<Number> Numbers { get; set; }
    #endregion

    public AscendantEmpireContext(DbContextOptions<AscendantEmpireContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        SetDecimalPrecision(modelBuilder, 18, 3);

        ConfigureAssetsSchema(modelBuilder);

        ConfigureBattleSchema(modelBuilder);

        ConfigureGameSchema(modelBuilder);

        ConfigureMapSchema(modelBuilder);

        ConfigureMapGallerySchema(modelBuilder);

        ConfigureStructureSchema(modelBuilder);

        ConfigureTurnSchema(modelBuilder);

        ConfigureUnitSchema(modelBuilder);

        ConfigureUnitDesignSchema(modelBuilder);
    }

    private static void ConfigureAssetsSchema(ModelBuilder modelBuilder)
    {
        const string schema = "assets";

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.General);

        var dictionaryConverter = new ValueConverter<Dictionary<string, string>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => string.IsNullOrWhiteSpace(v)
                ? new Dictionary<string, string>()
                : JsonSerializer.Deserialize<Dictionary<string, string>>(v, jsonOptions) ?? new Dictionary<string, string>());

        var dictionaryComparer = new ValueComparer<Dictionary<string, string>>(
            (a, b) => JsonSerializer.Serialize(a ?? new Dictionary<string, string>(), jsonOptions) ==
                      JsonSerializer.Serialize(b ?? new Dictionary<string, string>(), jsonOptions),
            v => JsonSerializer.Serialize(v ?? new Dictionary<string, string>(), jsonOptions).GetHashCode(),
            v => v == null ? new Dictionary<string, string>() : v.ToDictionary(k => k.Key, k => k.Value));

        modelBuilder.Entity<ResourceMetaCategory>(entity =>
        {
            entity.ToTable("ResourceMetaCategory", schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();

            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<ResourceCategory>(entity =>
        {
            entity.ToTable("ResourceCategory", schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();

            entity.HasIndex(x => new { x.ResourceMetaCategoryId, x.Name }).IsUnique();

            entity.HasOne(x => x.MetaCategory)
                .WithMany(x => x.Categories)
                .HasForeignKey(x => x.ResourceMetaCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // NEW: 1 -> many ResourceGroups
            entity.HasMany(x => x.ResourceGroups)
                .WithOne(x => x.Category)
                .HasForeignKey(x => x.ResourceCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ResourceGroup>(entity =>
        {
            entity.ToTable("ResourceGroup", schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => new { x.ResourceCategoryId, x.Name }).IsUnique();

            // GroupOrder is per-category
            entity.HasIndex(x => new { x.ResourceCategoryId, x.GroupOrder }).IsUnique();

            entity.HasOne(x => x.Category)
                .WithMany(x => x.ResourceGroups)
                .HasForeignKey(x => x.ResourceCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Resource>(entity =>
        {
            entity.ToTable("Resource", schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Code).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();

            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.Name);

            entity.Property(x => x.Properties)
                .HasConversion(dictionaryConverter)
                .Metadata.SetValueComparer(dictionaryComparer);

            entity.HasOne(x => x.Group)
                .WithMany(x => x.Resources)
                .HasForeignKey(x => x.ResourceGroupId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<FactionResourceState>(entity =>
        {
            entity.ToTable("FactionResourceState", schema);
            entity.HasKey(x => new { x.FactionId, x.ResourceId });

            entity.HasIndex(x => new { x.FactionId, x.AvailabilityState });
            entity.HasIndex(x => new { x.FactionId, x.IsCurrentlyLuxury });

            entity.HasOne(x => x.Resource)
                .WithMany(x => x.FactionStates)
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<FactionResourcePriority>(entity =>
        {
            entity.ToTable("FactionResourcePriority", schema);
            entity.HasKey(x => new { x.FactionId, x.ResourceCategoryId, x.ResourceId });

            entity.HasIndex(x => new { x.FactionId, x.ResourceCategoryId, x.PriorityOrder }).IsUnique();

            entity.HasOne(x => x.Category)
                .WithMany(x => x.FactionPriorities)
                .HasForeignKey(x => x.ResourceCategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Resource)
                .WithMany(x => x.FactionPriorities)
                .HasForeignKey(x => x.ResourceId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ResourceBank>(entity =>
        {
            entity.ToTable("ResourceBank", schema);
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.FactionId, x.Kind });
            entity.HasIndex(x => x.NetworkKey);
        });

        modelBuilder.Entity<ResourceBankBalance>(entity =>
        {
            entity.ToTable("ResourceBankBalance", schema);
            entity.HasKey(x => new { x.ResourceBankId, x.ResourceDefinitionId });

            entity.Property(x => x.Quantity).IsRequired();

            entity.HasOne(x => x.Bank)
                .WithMany(x => x.Balances)
                .HasForeignKey(x => x.ResourceBankId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Resource)
                .WithMany(x => x.BankBalances)
                .HasForeignKey(x => x.ResourceDefinitionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<ResourceBankSettlementMembership>(entity =>
        {
            entity.ToTable("ResourceBankSettlementMembership", schema);
            entity.HasKey(x => x.SettlementId);

            entity.HasIndex(x => x.ResourceBankId);

            entity.HasOne(x => x.Bank)
                .WithMany(x => x.SettlementMemberships)
                .HasForeignKey(x => x.ResourceBankId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WorldResourceSnapshot>(entity =>
        {
            entity.ToTable("WorldResourceSnapshot", schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.RelativeValue).HasPrecision(18, 6);

            entity.HasIndex(x => new { x.TurnNumber, x.ResourceDefinitionId }).IsUnique();

            entity.HasOne(x => x.Resource)
                .WithMany(x => x.WorldSnapshots)
                .HasForeignKey(x => x.ResourceDefinitionId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureBattleSchema(ModelBuilder modelBuilder)
    {
        const string schema = "battle";

        modelBuilder.Entity<Battle>(entity =>
        {
            entity.ToTable("Battle", schema);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.GlobalBattleName)
                .HasMaxLength(Battle.BATTLE_NAME_LENGTH);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(x => x.OriginalTileOwnerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(x => x.VictorId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(x => x.BattleParticipants)
                .WithOne(x => x.Battle)
                .HasForeignKey(x => x.BattleId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.GameId, x.CampaignMapTileId })
                .HasDatabaseName("UX_Battle_Game_Tile_Open")
                .IsUnique()
                .HasFilter("[ConcludedAtRoundNumber] IS NULL");

            entity.HasIndex(x => x.GameId);
            entity.HasIndex(x => new { x.GameId, x.CampaignMapTileId });
        });

        modelBuilder.Entity<BattleParticipant>(entity =>
        {
            entity.ToTable("BattleParticipant", schema);

            entity.HasKey(x => new { x.BattleId, x.PlayerId });

            entity.Property(x => x.PlayerBattleName)
                .HasMaxLength(BattleParticipant.BATTLE_NAME_LENGTH);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne(x => x.Battle)
                .WithMany(x => x.BattleParticipants)
                .HasForeignKey(x => x.BattleId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.BattleId, x.IsStillInBattle });
            entity.HasIndex(x => new { x.PlayerId, x.IsStillInBattle });
        });
    }

    private static void ConfigureGameSchema(ModelBuilder modelBuilder)
    {
        const string schema = "game";

        // --- CampaignMap ---------------------------------------------------------------
        modelBuilder.Entity<CampaignMap>(entity =>
        {
            entity.ToTable("CampaignMap", schema);

            entity.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.NoAction);

            // Lookup tables: no cascade paths
            entity.HasOne(x => x.Biome)
                .WithMany()
                .HasForeignKey(x => x.BiomeTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Geography)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapGeographyTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Optional relationships (because FK is nullable)
            entity.HasOne(x => x.FirstVisitor)
                .WithMany()
                .HasForeignKey(x => x.FirstSeenByPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.LastVisitor)
                .WithMany()
                .HasForeignKey(x => x.LastVisitedByPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.GameId, x.ControlledByPlayerId })
                .HasDatabaseName("IX_CampaignMap_Game_ControlledByPlayer");

            // Optional but often useful for “tile identity” safety:
            entity.HasIndex(x => new { x.GameId, x.XCoord, x.YCoord })
                .IsUnique()
                .HasDatabaseName("UX_CampaignMap_Game_X_Y");
        });

        // --- GameIndex -----------------------------------------------------------------
        modelBuilder.Entity<GameIndex>(entity =>
        {
            entity.ToTable("GameIndex", schema);

            entity.HasMany(g => g.Players)
                .WithOne(p => p.Game)
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // --- Player --------------------------------------------------------------------
        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("Player", schema);
           
            entity.HasOne(p => p.Game)
                .WithMany(g => g.Players)
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // --- PlayerCampaignMap ---------------------------------------------------------
        modelBuilder.Entity<PlayerCampaignMap>(entity =>
        {
            entity.ToTable("PlayerCampaignMap", schema);

            entity.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            // (Deleting a map deletes its per-player rows)
            entity.HasOne(x => x.CampaignMap)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Biome)
                .WithMany()
                .HasForeignKey(x => x.BiomeTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Geography)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapGeographyTypeId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // --- PlayerKnownStructure ------------------------------------------------------
        modelBuilder.Entity<PlayerKnownStructure>(entity =>
        {
            entity.ToTable("PlayerKnownStructures", schema);

            // PlayerKnownStructures -> GameIndex (many Structures per game)
            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownStructures_GameIndex_GameId");

            // PlayerKnownStructures -> Player (many Structures per player)
            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownStructures_Player_PlayerId");

            entity.HasOne(e => e.StructureInstance)
                .WithMany()
                .HasForeignKey(e => e.StructureInstanceId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownStructures_StructureInstances_StructureInstanceId");

            entity.HasIndex(e => new { e.GameId, e.PlayerId, e.StructureInstanceId })
                .IsUnique()
                .HasDatabaseName("UX_PlayerKnownStructures_Game_Player_Instance");

            entity.HasIndex(e => new { e.GameId, e.PlayerId, e.CampaignMapId })
                .HasDatabaseName("IX_PlayerKnownStructures_Game_Player_Tile");

            entity.HasIndex(e => new { e.GameId, e.PlayerId, e.LastSeenOnTurnId })
                .HasDatabaseName("IX_PlayerKnownStructures_Game_Player_LastSeen");
        });

        // --- StructureInstance ---------------------------------------------------------
        modelBuilder.Entity<StructureInstance>(entity =>
        {
            entity.ToTable("StructureInstances", schema);

            entity.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Structure)
                .WithMany()
                .HasForeignKey(x => x.StructureId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.CampaignMap)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction);

            // Only one ACTIVE instance per tile per slot
            entity.HasIndex(x => new { x.CampaignMapTileId, x.SlotType })
                  .IsUnique()
                  .HasFilter("[RemovedOnTurnId] IS NULL AND [BattleMapTileId] IS NULL"); // SQL Server filtered unique index

            // Fast join from tile -> structures (especially after filtering tiles by control)
            entity.HasIndex(x => new { x.GameId, x.CampaignMapTileId })
                .HasDatabaseName("IX_CampaignStructureInstances_Game_Tile");

            // Optional: if you sometimes query "all active structures in a game"
            entity.HasIndex(x => new { x.GameId, x.RemovedOnTurnId })
                .HasDatabaseName("IX_CampaignStructureInstances_Game_RemovedOn");

            entity.HasOne(x => x.OwnedBy)
                .WithMany()
                .HasForeignKey(x => x.OwnedByPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            // NEW: query index (owner + active + game)
            entity.HasIndex(x => new { x.GameId, x.OwnedByPlayerId, x.RemovedOnTurnId })
                .HasDatabaseName("IX_CampaignStructureInstances_Game_Owner_RemovedOn");

            // Optional: if you frequently filter by battle/campaign
            entity.HasIndex(x => new { x.GameId, x.OwnedByPlayerId, x.BattleMapTileId })
                .HasDatabaseName("IX_CampaignStructureInstances_Game_Owner_BattleMap");

            entity.HasIndex(x => new { x.BattleMapTileId })
                .IsUnique()
                .HasDatabaseName("UX_CampaignStructureInstances_Active_BattleTile")
                .HasFilter("[RemovedOnTurnId] IS NULL AND [BattleMapTileId] IS NOT NULL");
        });
    }

    private static void ConfigureMapSchema(ModelBuilder modelBuilder)
    {
        const string schema = "map";

        // --- CampaignBiomes ------------------------------------------------------------
        modelBuilder.Entity<CampaignBiome>(entity =>
        {
            entity.ToTable("CampaignBiomes", schema);
        });

        // --- CampaignGeographies ------------------------------------------------------
        modelBuilder.Entity<CampaignGeography>(entity =>
        {
            entity.ToTable("CampaignGeography", schema);
        });
    }

    private static void ConfigureMapGallerySchema(ModelBuilder modelBuilder)
    {
        const string schema = "mapgallery";

        // --- CampaignMapGallery ------------------------------------------------------
        modelBuilder.Entity<CampaignMapGallery>(entity =>
        {
            entity.ToTable("CampaignMapGallery", schema);

            entity.HasOne(x => x.Biome)
                .WithMany()
                .HasForeignKey(x => x.BiomeTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Geography)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapGeographyTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Index)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapGalleryIndexId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // --- CampaignMapGalleryIndex ---------------------------------------------------
        modelBuilder.Entity<CampaignMapGalleryIndex>(entity =>
        {
            entity.ToTable("CampaignMapGalleryIndex", schema);
        });
    }

    private static void ConfigureStructureSchema(ModelBuilder modelBuilder)
    {
        const string schema = "structure";

        // --- Structure -----------------------------------------------------------------
        modelBuilder.Entity<Structure>(entity =>
        {
            entity.ToTable("Structures", schema);
        });
    }

    private static void ConfigureTurnSchema(ModelBuilder modelBuilder)
    {
        const string schema = "turns";

        modelBuilder.Entity<TurnQueue>(entity =>
        {
            entity.ToTable("TurnQueue", schema);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Battle)
                .WithMany()
                .HasForeignKey(x => x.BattleId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(x => x.TurnQueueParticipants)
                .WithOne(x => x.TurnQueue)
                .HasForeignKey(x => x.TurnQueueId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => x.GameId);
            entity.HasIndex(x => new { x.GameId, x.QueueType, x.IsCompleted });

            entity.HasIndex(x => x.BattleId)
                .HasDatabaseName("UX_TurnQueue_Battle")
                .IsUnique()
                .HasFilter("[BattleId] IS NOT NULL");

            // Optional: enforce one active campaign lane per game.
            entity.HasIndex(x => new { x.GameId, x.QueueType })
                .HasDatabaseName("UX_TurnQueue_Campaign_Game_Active")
                .IsUnique()
                .HasFilter("[QueueType] = 0 AND [BattleId] IS NULL AND [IsCompleted] = 0");
        });

        modelBuilder.Entity<TurnQueueParticipant>(entity =>
        {
            entity.ToTable("TurnQueueParticipant", schema);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            entity.HasOne(x => x.TurnQueue)
                .WithMany(x => x.TurnQueueParticipants)
                .HasForeignKey(x => x.TurnQueueId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => new { x.TurnQueueId, x.PlayerId }).IsUnique();
            entity.HasIndex(x => new { x.TurnQueueId, x.TurnOrder }).IsUnique();
            entity.HasIndex(x => new { x.TurnQueueId, x.Status });
            entity.HasIndex(x => new { x.TurnQueueId, x.IsActiveInLane, x.TurnOrder });
            entity.HasIndex(x => x.PlayerId);
        });
    }

    private static void ConfigureUnitSchema(ModelBuilder modelBuilder)
    {
        const string schema = "units";

        // --- TroopInstances --------------------------------------------------------
        modelBuilder.Entity<TroopInstance>(entity =>
        {
            entity.ToTable("TroopInstances", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.BattleMapTileId).IsRequired(false);

            // PlayerTroopSummary -> GameIndex (many summaries per game)
            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TroopInstances_GameIndex_GameId");

            // PlayerTroopSummary -> Player (many summaries per player)
            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TroopInstances_Player_PlayerId");

            entity.HasOne(e => e.CampaignMap)
                .WithMany()
                .HasForeignKey(e => e.CampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TroopInstances_CampaignMap_CampaignMapTileId");

            entity.HasOne(e => e.UnitDesign)
                 .WithMany()
                 .HasForeignKey(e => e.UnitDesignId)
                 .OnDelete(DeleteBehavior.NoAction)
                 .HasConstraintName("FK_TroopInstances_UnitDesigns_UnitDesignId");
        });

        modelBuilder.Entity<TroopOrder>(entity =>
        {
            entity.ToTable("TroopOrders", "troops");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.OrderType)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(e => e.OrderStatus)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(e => e.WorkCompleted)
                .HasDefaultValue(0m);

            entity.Property(e => e.FailureReason)
                .HasMaxLength(500);

            entity.Property(t => t.CancellationReason)
                .HasMaxLength(500);

            entity.HasOne(e => e.TroopInstance)
                .WithMany(t => t.Orders)
                .HasForeignKey(e => e.TroopInstanceId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasMany(e => e.MovementSteps)
                .WithOne(e => e.TroopOrder)
                .HasForeignKey(e => e.TroopOrderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.TroopInstanceId);
            entity.HasIndex(e => new { e.GameId, e.TroopInstanceId, e.OrderStatus });
            entity.HasIndex(e => new { e.GameId, e.OrderStatus });
            entity.HasIndex(e => new { e.GameId, e.OrderType, e.OrderStatus });
            entity.HasIndex(e => e.TargetCampaignMapTileId);

            entity.HasIndex(e => e.StructureInstanceId);
            entity.HasIndex(e => e.ResourceDefinitionId);

            entity.HasIndex(e => e.TradeSourceStructureInstanceId);
            entity.HasIndex(e => e.TradeTargetStructureInstanceId);

            entity.HasIndex(e => e.DefendTargetTroopInstanceId);
            entity.HasIndex(e => e.DefendTargetStructureInstanceId);
            entity.HasIndex(x => new { x.TroopInstanceId, x.OrderStatus });
        });

        modelBuilder.Entity<TroopOrderMovementStep>(entity =>
        {
            entity.ToTable("TroopOrderMovementSteps", "troops");

            entity.HasKey(e => e.Id);

            entity.Property(e => e.IsCompleted)
                .HasDefaultValue(false);

            entity.HasOne(e => e.TroopOrder)
                .WithMany(e => e.MovementSteps)
                .HasForeignKey(e => e.TroopOrderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.TroopOrderId);

            entity.HasIndex(e => new { e.TroopOrderId, e.StepIndex })
                .IsUnique();

            entity.HasIndex(e => new { e.CampaignMapTileId, e.IsCompleted });
        });

        modelBuilder.Entity<TroopOrderParticipant>(entity =>
        {
            entity.ToTable("TroopOrderParticipants", "troops");

            entity.Property(x => x.Role)
                .HasConversion<int>()
                .IsRequired();

            entity.HasOne(e => e.TroopOrder)
                .WithMany()
                .HasForeignKey(e => e.TroopOrderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => x.TroopOrderId);

            entity.HasIndex(x => x.TroopInstanceId);

            entity.HasIndex(x => new { x.TroopOrderId, x.TroopInstanceId })
                .IsUnique();

            entity.HasOne(x => x.TroopInstance)
                .WithMany()
                .HasForeignKey(x => x.TroopInstanceId)
                .OnDelete(DeleteBehavior.NoAction);
        });
    }

    private static void ConfigureUnitDesignSchema(ModelBuilder modelBuilder)
    {
        const string schema = "unitdesigns";

        modelBuilder.Entity<UnitDesign>(entity =>
        {
            entity.ToTable("UnitDesigns", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.DesignName)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.MapImagePath)
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(e => e.IsTemplate)
                .IsRequired();

            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitDesigns_GameIndex_GameId");

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitDesigns_Player_PlayerId");

            entity.HasOne(e => e.CompiledStats)
                .WithOne(e => e.UnitDesign)
                .HasForeignKey<CompiledDesignStats>(e => e.UnitDesignId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CompiledDesignStats_UnitDesigns_UnitDesignId");

            entity.HasMany(e => e.PlayerStates)
                .WithOne(e => e.UnitDesign)
                .HasForeignKey(e => e.UnitDesignId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnitDesignStates_UnitDesigns_UnitDesignId");
        });

        modelBuilder.Entity<CompiledDesignStats>(entity =>
        {
            entity.ToTable("CompiledDesignStats", schema);

            entity.HasKey(e => e.UnitDesignId);

            entity.Property(e => e.SightRange)
                .IsRequired();
        });

        modelBuilder.Entity<PlayerUnitDesignState>(entity =>
        {
            entity.ToTable("PlayerUnitDesignStates", schema);

            entity.HasKey(e => new { e.PlayerId, e.UnitDesignId });

            entity.Property(e => e.IsUnlocked)
                .IsRequired();

            entity.Property(e => e.IsObsolete)
                .IsRequired();

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnitDesignStates_Player_PlayerId");
        });
    }


    /// <summary>
    /// Sets the precision and scale for all decimal properties in the model.
    /// </summary>
    private static void SetDecimalPrecision(ModelBuilder modelBuilder, int precision, int scale)
    {
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                {
                    property.SetPrecision(precision);
                    property.SetScale(scale);
                }
            }
        }
    }
}
