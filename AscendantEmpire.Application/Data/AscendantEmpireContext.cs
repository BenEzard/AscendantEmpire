using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Entities.Util;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AscendantEmpire.Application.Data;

public class AscendantEmpireContext : DbContext
{
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

    /// <summary>
    /// The Roster/queue of Player Turns within a Game Round.
    /// </summary>
    public DbSet<TurnRoster> TurnRosters { get; set; }

    public DbSet<PlayerKnownStructure> PlayerStructures { get; set; }
    public DbSet<StructureInstance> StructureInstances { get; set; }
    #endregion

    #region Units

    public DbSet<TroopInstance> TroopInstances { get; set; }

    #endregion

    #region Unit Designs

    public DbSet<CompiledDesignStats> DesignStats { get; set; }

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

        ConfigureGameSchema(modelBuilder);

        ConfigureMapSchema(modelBuilder);

        ConfigureMapGallerySchema(modelBuilder);

        ConfigureStructureSchema(modelBuilder);

        ConfigureUnitSchema(modelBuilder);

        ConfigureUnitDesignSchema(modelBuilder);
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

        // --- TurnRoster ----------------------------------------------------------------
        modelBuilder.Entity<TurnRoster>(entity =>
        {
            entity.ToTable("TurnRoster", schema);

            // Campaign roster uniqueness (BattleId IS NULL)
            entity.HasIndex(e => new { e.GameId, e.PlayerId, e.RoundNumber })
                .HasDatabaseName("UX_TurnRoster_Campaign_Game_Player_Round")
                .IsUnique()
                .HasFilter("[BattleId] IS NULL");

            // Battle roster uniqueness (BattleId IS NOT NULL)
            entity.HasIndex(e => new { e.GameId, e.BattleId, e.PlayerId, e.RoundNumber })
                .HasDatabaseName("UX_TurnRoster_Battle_Game_Battle_Player_Round")
                .IsUnique()
                .HasFilter("[BattleId] IS NOT NULL");
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
        });
    }

    private static void ConfigureUnitDesignSchema(ModelBuilder modelBuilder)
    {
        const string schema = "unitdesigns";

        // --- TroopInstances --------------------------------------------------------
        modelBuilder.Entity<CompiledDesignStats>(entity =>
        {
            entity.ToTable("CompiledDesignStats", schema);

            entity.HasKey(e => e.UnitDesignId);

            modelBuilder.Entity<CompiledDesignStats>(entity =>
            {
                entity.HasKey(x => x.UnitDesignId);
            });

            modelBuilder.Entity<TroopInstance>(entity =>
            {
                entity.HasOne(x => x.Stats)
                    .WithMany()
                    .HasForeignKey(x => x.UnitDesignId)
                    .OnDelete(DeleteBehavior.NoAction);
            });
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
