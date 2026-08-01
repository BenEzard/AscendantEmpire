using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Entities.Maps.BattleMaps;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Military;
using AscendantEmpire.Domain.Entities.Resources;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Entities.Util;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AscendantEmpire.Application.Data;

public class AscendantEmpireContext : DbContext
{
    #region Battle Schema
    public DbSet<Battle> Battles { get; set; }
    public DbSet<BattleParticipant> BattleParticipants { get; set; }
    #endregion

    #region Battle Map Schema
    /// <summary>
    /// Index of generated BattleMaps (one per campaign tile per game that has been fought over).
    /// </summary>
    public DbSet<BattleMapIndex> BattleMapIndex { get; set; }

    /// <summary>
    /// Tile data for BattleMaps (BattleMapSize × BattleMapSize tiles per battle map).
    /// </summary>
    public DbSet<BattleMap> BattleMap { get; set; }
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

    /// <summary>
    /// Authored player start positions for maps in the gallery.
    /// </summary>
    public DbSet<CampaignMapStartPosition> CampaignMapStartPositions { get; set; }
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

    #region Auth Schema
    /// <summary>
    /// User accounts that people log in with. Each User owns one or more Players.
    /// </summary>
    public DbSet<User> Users { get; set; }

    /// <summary>
    /// Server-side login sessions backing refresh tokens. Revocable, unlike the access token.
    /// </summary>
    public DbSet<RefreshSession> RefreshSessions { get; set; }
    #endregion

    #region Resources Schema
    public DbSet<ResourceMetaCategory> ResourceMetaCategories { get; set; }
    public DbSet<ResourceCategory> ResourceCategories { get; set; }
    public DbSet<ResourceGroup> ResourceGroups { get; set; }
    public DbSet<Resource> Resources { get; set; }
    #endregion

    #region Turn Schema
    public DbSet<TurnQueue> TurnQueues { get; set; }
    public DbSet<TurnQueueParticipant> TurnQueueParticipants { get; set; }
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

    // --- Component-based unit design system (global catalog, unlocks, templates) ---

    /// <summary>
    /// Global reference catalog of buildable unit components (body, weapon, engine, etc.).
    /// </summary>
    public DbSet<UnitComponent> UnitComponents { get; set; }

    /// <summary>
    /// Body-only properties, one-to-one with body-type <see cref="UnitComponent"/> rows.
    /// </summary>
    public DbSet<UnitComponentBody> UnitComponentBodies { get; set; }

    /// <summary>
    /// Weapon-only properties, one-to-one with weapon-type <see cref="UnitComponent"/> rows.
    /// </summary>
    public DbSet<UnitComponentWeapon> UnitComponentWeapons { get; set; }

    /// <summary>
    /// Per-Domain damage multipliers declaring which Domains a weapon can attack.
    /// </summary>
    public DbSet<UnitComponentWeaponDomainModifier> UnitComponentWeaponDomainModifiers { get; set; }

    /// <summary>
    /// Armour-only properties, one-to-one with armour-type <see cref="UnitComponent"/> rows.
    /// </summary>
    public DbSet<UnitComponentArmour> UnitComponentArmours { get; set; }

    /// <summary>
    /// Template Slots exposed by body components.
    /// </summary>
    public DbSet<UnitComponentSlot> UnitComponentSlots { get; set; }

    /// <summary>
    /// Acceptance rules constraining which components may be fitted into a slot.
    /// </summary>
    public DbSet<UnitComponentSlotRule> UnitComponentSlotRules { get; set; }

    /// <summary>
    /// Global, reusable bonus/buff tag catalog.
    /// </summary>
    public DbSet<Tag> Tags { get; set; }

    /// <summary>
    /// Editable metadata for each <see cref="Domain.Enums.UnitDesigns.TagType"/> value (keyed by the enum).
    /// </summary>
    public DbSet<TagTypeInfo> TagTypeInfos { get; set; }

    /// <summary>
    /// Join between components and their tags.
    /// </summary>
    public DbSet<UnitComponentTag> UnitComponentTags { get; set; }

    /// <summary>
    /// Directed "successor supersedes predecessor" links between components (upgrade paths).
    /// </summary>
    public DbSet<ComponentSupersession> ComponentSupersessions { get; set; }

    /// <summary>
    /// Per-player record of which components have been unlocked.
    /// </summary>
    public DbSet<PlayerUnlockedComponent> PlayerUnlockedComponents { get; set; }

    /// <summary>
    /// Per-player unlock/obsolescence state for global (common) unit templates.
    /// </summary>
    public DbSet<PlayerUnlockedTemplate> PlayerUnlockedTemplates { get; set; }

    /// <summary>
    /// Assembled unit templates (global/common and custom player-owned).
    /// </summary>
    public DbSet<UnitTemplate> UnitTemplates { get; set; }

    /// <summary>
    /// Components fitted into a template's body slots.
    /// </summary>
    public DbSet<UnitTemplateComponent> UnitTemplateComponents { get; set; }

    /// <summary>
    /// Directed "successor supersedes predecessor" links between global templates (upgrade paths).
    /// </summary>
    public DbSet<TemplateSupersession> TemplateSupersessions { get; set; }

    /// <summary>
    /// Denormalized snapshots of saved templates, read without the unlock filter (e.g. for enemy designs).
    /// </summary>
    public DbSet<CompiledUnitTemplate> CompiledUnitTemplates { get; set; }

    /// <summary>
    /// Resolved component rows (body + fitted) belonging to a compiled template snapshot.
    /// </summary>
    public DbSet<CompiledUnitTemplateComponent> CompiledUnitTemplateComponents { get; set; }

    /// <summary>
    /// Capability tag rows belonging to a compiled template snapshot.
    /// </summary>
    public DbSet<CompiledUnitTemplateCapability> CompiledUnitTemplateCapabilities { get; set; }
    #endregion


    #region Military Schema
    /// <summary>
    /// Per-player officer rank ladders (one row per player / movement domain / level).
    /// </summary>
    public DbSet<PlayerOfficerRank> PlayerOfficerRanks { get; set; }

    /// <summary>
    /// Per-player organisation structures, each bound to a MilitaryOrganisationalGroup tag.
    /// </summary>
    public DbSet<PlayerOrganisationStructure> PlayerOrganisationStructures { get; set; }

    /// <summary>
    /// The tiers belonging to each player organisation structure.
    /// </summary>
    public DbSet<PlayerOrganisationStructureTier> PlayerOrganisationStructureTiers { get; set; }
    #endregion

    #region System Schema
    /// <summary>
    /// Background process tracking records (exports, imports, etc.).
    /// </summary>
    public DbSet<Processing> Processing { get; set; }
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

        ConfigureResourcesSchema(modelBuilder);

        ConfigureBattleSchema(modelBuilder);

        ConfigureGameSchema(modelBuilder);

        ConfigureAuthSchema(modelBuilder);

        ConfigureMapSchema(modelBuilder);

        ConfigureMapGallerySchema(modelBuilder);

        ConfigureStructureSchema(modelBuilder);

        ConfigureSystemSchema(modelBuilder);

        ConfigureTurnSchema(modelBuilder);

        ConfigureUnitSchema(modelBuilder);

        ConfigureUnitDesignSchema(modelBuilder);

        ConfigureMilitarySchema(modelBuilder);
    }

    private static void ConfigureResourcesSchema(ModelBuilder modelBuilder)
    {
        const string schema = "resources";


        modelBuilder.Entity<ResourceMetaCategory>(entity =>
        {
            entity.ToTable("ResourceMetaCategory", schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(ResourceMetaCategory.MAX_DISPLAY_NAME_LENGTH).IsRequired();

            entity.HasIndex(x => x.DisplayName).IsUnique();
        });

        modelBuilder.Entity<ResourceCategory>(entity =>
        {
            entity.ToTable("ResourceCategory", schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DisplayName).HasMaxLength(ResourceCategory.MAX_DISPLAY_NAME_LENGTH).IsRequired();

            entity.HasIndex(x => new { x.ResourceMetaCategoryId, x.DisplayName }).IsUnique();

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

            entity.Property(x => x.DisplayName).HasMaxLength(ResourceGroup.MAX_DISPLAY_NAME_LENGTH).IsRequired();
            entity.HasIndex(x => new { x.ResourceCategoryId, x.DisplayName }).IsUnique();

            // GroupOrder is per-category
            entity.HasIndex(x => new { x.ResourceCategoryId, x.SortOrder }).IsUnique();

            entity.HasOne(x => x.Category)
                .WithMany(x => x.ResourceGroups)
                .HasForeignKey(x => x.ResourceCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Resource>(entity =>
        {
            // Enforce that exactly one of ResourceGroupId / ResourceCategoryId is non-null.
            entity.ToTable("Resource", schema, t => t.HasCheckConstraint(
                "CK_Resource_GroupOrCategoryXor",
                "([ResourceGroupId] IS NOT NULL AND [ResourceCategoryId] IS NULL) OR ([ResourceGroupId] IS NULL AND [ResourceCategoryId] IS NOT NULL)"));

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Code).HasMaxLength(Resource.MAX_CODE_LENGTH).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(Resource.MAX_DISPLAY_NAME_LENGTH).IsRequired();

            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.DisplayName);

            // Grouped path: resource belongs to a ResourceGroup.
            entity.HasOne(x => x.Group)
                .WithMany(x => x.Resources)
                .HasForeignKey(x => x.ResourceGroupId)
                .OnDelete(DeleteBehavior.NoAction);

            // Standalone path: resource belongs directly to a ResourceCategory.
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Resources)
                .HasForeignKey(x => x.ResourceCategoryId)
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

        // --- BattleMapIndex ---------------------------------------------------------------
        modelBuilder.Entity<BattleMapIndex>(entity =>
        {
            entity.ToTable("BattleMapIndex", schema);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status).HasMaxLength(500).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);

            entity.HasOne(x => x.CampaignMap)
                  .WithMany()
                  .HasForeignKey(x => x.CampaignMapId)
                  // Restrict rather than cascade: don't silently delete battle maps when a campaign tile is removed.
                  .OnDelete(DeleteBehavior.Restrict);

            // Only one BattleMap per campaign tile per game.
            entity.HasIndex(x => x.CampaignMapId)
                  .IsUnique();
        });

        // --- BattleMap -------------------------------------------------------------------
        modelBuilder.Entity<BattleMap>(entity =>
        {
            entity.ToTable("BattleMap", schema);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                  .ValueGeneratedNever();   // manually assigned at generation time

            entity.HasOne(x => x.Index)
                  .WithMany()
                  .HasForeignKey(x => x.BattleMapIndexId)
                  .IsRequired();

            entity.HasOne(x => x.Biome)
                  .WithMany()
                  .HasForeignKey(x => x.BiomeTypeId)
                  .IsRequired();

            // Unique tile within each battle map.
            entity.HasIndex(x => new { x.BattleMapIndexId, x.XCoord, x.YCoord })
                  .IsUnique();

            // Non-unique index for bulk tile lookups by battle map.
            entity.HasIndex(x => x.BattleMapIndexId);

            entity.HasIndex(x => x.BiomeTypeId);
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

    private static void ConfigureAuthSchema(ModelBuilder modelBuilder)
    {
        const string schema = "auth";

        // --- User ----------------------------------------------------------------------
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", schema);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Username).HasMaxLength(User.USERNAME_MAX_LENGTH).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(User.EMAIL_MAX_LENGTH);
            entity.Property(x => x.PasswordHash).HasMaxLength(User.PASSWORD_HASH_MAX_LENGTH).IsRequired();
            entity.Property(x => x.InactiveReasonType).HasDefaultValue(Domain.Enums.Authentication.InactiveReasonType.Active);

            // Usernames are the login key, so they must be unique.
            entity.HasIndex(x => x.Username).IsUnique();

            // A User owns many Players (one per game). Deleting a user is restricted while
            // they still control players — don't silently orphan game state.
            entity.HasMany(x => x.Players)
                .WithOne(p => p.User)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // A user can hold at most one Player slot per game. The filter lets the many
        // unassigned slots (UserId IS NULL) coexist while still enforcing uniqueness
        // for assigned ones.
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasIndex(p => new { p.GameId, p.UserId })
                .IsUnique()
                .HasFilter("[UserId] IS NOT NULL")
                .HasDatabaseName("UX_Player_Game_User");
        });

        // --- RefreshSession ------------------------------------------------------------
        modelBuilder.Entity<RefreshSession>(entity =>
        {
            entity.ToTable("RefreshSessions", schema);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.TokenHash).HasMaxLength(RefreshSession.TOKEN_HASH_MAX_LENGTH).IsRequired();

            // Lookups happen by token hash, so it must be unique and indexed.
            entity.HasIndex(x => x.TokenHash)
                .IsUnique()
                .HasDatabaseName("UX_RefreshSession_TokenHash");

            // Revocation queries hit these columns: by game (delete game / kick all) and by user.
            entity.HasIndex(x => x.GameId).HasDatabaseName("IX_RefreshSession_Game");
            entity.HasIndex(x => x.UserId).HasDatabaseName("IX_RefreshSession_User");

            // Deleting a user takes their sessions with them (unlike Players, sessions are
            // disposable and carry no game state worth preserving).
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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

        // --- CampaignMapStartPosition -----------------------------------------------
        modelBuilder.Entity<CampaignMapStartPosition>(entity =>
        {
            entity.ToTable("CampaignMapStartPosition", schema);

            entity.Property(x => x.DisplayName)
                .HasMaxLength(CampaignMapStartPosition.DISPLAY_NAME_MAX_LENGTH);

            entity.HasOne(x => x.CampaignMapGalleryIndex)
                .WithMany(x => x.StartPositions)
                .HasForeignKey(x => x.CampaignMapGalleryIndexId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => x.CampaignMapGalleryIndexId)
                .HasDatabaseName("IX_CampaignMapStartPosition_Map");

            entity.HasIndex(x => new { x.CampaignMapGalleryIndexId, x.XCoord, x.YCoord })
                .IsUnique()
                .HasDatabaseName("UX_CampaignMapStartPosition_Map_X_Y");
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

    private static void ConfigureSystemSchema(ModelBuilder modelBuilder)
    {
        const string schema = "system";

        modelBuilder.Entity<Processing>(entity =>
        {
            entity.ToTable("BackgroundProcessing", schema);

            entity.HasKey(x => x.ProcessingId);

            entity.Property(x => x.ProcessType)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.Status)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(x => x.StartedAt)
                .IsRequired();

            // No FK constraints — MetadataJson carries process-type-specific context loosely
            entity.Property(x => x.MetadataJson)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.ResultReference)
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.ErrorMessage)
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(x => x.Status)
                .HasDatabaseName("IX_Processing_Status");

            entity.HasIndex(x => x.ProcessType)
                .HasDatabaseName("IX_Processing_ProcessType");
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

        ConfigureUnitComponentCatalog(modelBuilder, schema);
        ConfigureUnitTemplates(modelBuilder, schema);
        ConfigureComponentSupersession(modelBuilder, schema);
        ConfigureTemplateSupersession(modelBuilder, schema);
    }

    /// <summary>
    /// Configures the directed many-to-many "successor supersedes predecessor" links between components.
    /// </summary>
    private static void ConfigureComponentSupersession(ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<ComponentSupersession>(entity =>
        {
            entity.ToTable("ComponentSupersession", schema);

            entity.HasKey(e => new { e.PredecessorComponentId, e.SuccessorComponentId });

            // Both self-referencing FKs must be NoAction: cascading either way would create multiple
            // cascade paths into UnitComponent.
            entity.HasOne(e => e.Predecessor)
                .WithMany(e => e.SupersededBy)
                .HasForeignKey(e => e.PredecessorComponentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ComponentSupersession_UnitComponent_PredecessorComponentId");

            entity.HasOne(e => e.Successor)
                .WithMany(e => e.Supersedes)
                .HasForeignKey(e => e.SuccessorComponentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ComponentSupersession_UnitComponent_SuccessorComponentId");

            // Reverse lookups ("what does this component supersede") hit the successor side.
            entity.HasIndex(e => e.SuccessorComponentId)
                .HasDatabaseName("IX_ComponentSupersession_Successor");
        });
    }

    /// <summary>
    /// Configures the directed many-to-many "successor supersedes predecessor" links between templates.
    /// </summary>
    private static void ConfigureTemplateSupersession(ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<TemplateSupersession>(entity =>
        {
            entity.ToTable("TemplateSupersession", schema);

            entity.HasKey(e => new { e.PredecessorTemplateId, e.SuccessorTemplateId });

            // Both self-referencing FKs must be NoAction: cascading either way would create multiple
            // cascade paths into UnitTemplate.
            entity.HasOne(e => e.Predecessor)
                .WithMany(e => e.SupersededBy)
                .HasForeignKey(e => e.PredecessorTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TemplateSupersession_UnitTemplate_PredecessorTemplateId");

            entity.HasOne(e => e.Successor)
                .WithMany(e => e.Supersedes)
                .HasForeignKey(e => e.SuccessorTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TemplateSupersession_UnitTemplate_SuccessorTemplateId");

            // Reverse lookups ("what does this template supersede") hit the successor side.
            entity.HasIndex(e => e.SuccessorTemplateId)
                .HasDatabaseName("IX_TemplateSupersession_Successor");
        });
    }

    /// <summary>
    /// Configures the global component catalog: components, body extension, slots, slot rules and buff tags.
    /// </summary>
    private static void ConfigureUnitComponentCatalog(ModelBuilder modelBuilder, string schema)
    {
        // --- UnitComponent ---------------------------------------------------------
        modelBuilder.Entity<UnitComponent>(entity =>
        {
            entity.ToTable("UnitComponent", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasMaxLength(UnitComponent.CODE_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.DisplayName)
                .HasMaxLength(UnitComponent.DISPLAY_NAME_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.DesignerImagePath)
                .HasMaxLength(UnitComponent.DESIGNER_IMAGE_PATH_MAX_LENGTH);

            entity.Property(e => e.ComponentType)
                .HasConversion<int>()
                .IsRequired();

            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UX_UnitComponent_Code");

            entity.HasIndex(e => e.ComponentType)
                .HasDatabaseName("IX_UnitComponent_ComponentType");
        });

        // --- UnitComponentBody (1:1 with UnitComponent) ----------------------------
        modelBuilder.Entity<UnitComponentBody>(entity =>
        {
            entity.ToTable("UnitComponentBody", schema);

            // Shares the primary key of the owning UnitComponent.
            entity.HasKey(e => e.UnitComponentId);

            entity.Property(e => e.MovementDomain)
                .HasConversion<int>()
                .IsRequired();

            entity.HasOne(e => e.UnitComponent)
                .WithOne(e => e.Body)
                .HasForeignKey<UnitComponentBody>(e => e.UnitComponentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentBody_UnitComponent_UnitComponentId");

            // Only bodies expose Template Slots, so the slot collection hangs off the body extension.
            entity.HasMany(e => e.Slots)
                .WithOne(e => e.Body)
                .HasForeignKey(e => e.UnitComponentBodyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentSlot_UnitComponentBody_UnitComponentBodyId");
        });

        // --- UnitComponentWeapon (1:1 with UnitComponent) --------------------------
        modelBuilder.Entity<UnitComponentWeapon>(entity =>
        {
            entity.ToTable("UnitComponentWeapon", schema);

            // Shares the primary key of the owning UnitComponent.
            entity.HasKey(e => e.UnitComponentId);

            // The attack DamageProfile is a complex type: its five values live as flat columns on
            // this table. Map explicit column names to avoid the default "Attack_Slash" naming.
            entity.ComplexProperty(e => e.Attack, attack =>
            {
                attack.Property(p => p.Slash).HasColumnName("Slash");
                attack.Property(p => p.Pierce).HasColumnName("Pierce");
                attack.Property(p => p.Crush).HasColumnName("Crush");
                attack.Property(p => p.Burn).HasColumnName("Burn");
                attack.Property(p => p.Poison).HasColumnName("Poison");
            });

            entity.HasOne(e => e.UnitComponent)
                .WithOne(e => e.Weapon)
                .HasForeignKey<UnitComponentWeapon>(e => e.UnitComponentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentWeapon_UnitComponent_UnitComponentId");
        });

        // --- UnitComponentWeaponDomainModifier -------------------------------------
        modelBuilder.Entity<UnitComponentWeaponDomainModifier>(entity =>
        {
            entity.ToTable("UnitComponentWeaponDomainModifier", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Domain)
                .HasConversion<int>()
                .IsRequired();

            entity.HasOne(e => e.Weapon)
                .WithMany(e => e.DomainModifiers)
                .HasForeignKey(e => e.UnitComponentWeaponId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentWeaponDomainModifier_UnitComponentWeapon_UnitComponentWeaponId");

            // A weapon lists each target Domain at most once.
            entity.HasIndex(e => new { e.UnitComponentWeaponId, e.Domain })
                .IsUnique()
                .HasDatabaseName("UX_UnitComponentWeaponDomainModifier_Weapon_Domain");
        });

        // --- UnitComponentArmour (1:1 with UnitComponent) --------------------------
        modelBuilder.Entity<UnitComponentArmour>(entity =>
        {
            entity.ToTable("UnitComponentArmour", schema);

            // Shares the primary key of the owning UnitComponent.
            entity.HasKey(e => e.UnitComponentId);

            // The resistance DamageProfile is a complex type: its five values live as flat columns
            // on this table. A distinct "Resist" prefix keeps the intent clear at the column level.
            entity.ComplexProperty(e => e.Resistance, resistance =>
            {
                resistance.Property(p => p.Slash).HasColumnName("ResistSlash");
                resistance.Property(p => p.Pierce).HasColumnName("ResistPierce");
                resistance.Property(p => p.Crush).HasColumnName("ResistCrush");
                resistance.Property(p => p.Burn).HasColumnName("ResistBurn");
                resistance.Property(p => p.Poison).HasColumnName("ResistPoison");
            });

            entity.HasOne(e => e.UnitComponent)
                .WithOne(e => e.Armour)
                .HasForeignKey<UnitComponentArmour>(e => e.UnitComponentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentArmour_UnitComponent_UnitComponentId");
        });

        // --- UnitComponentSlot -----------------------------------------------------
        modelBuilder.Entity<UnitComponentSlot>(entity =>
        {
            entity.ToTable("UnitComponentSlot", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.DisplayName)
                .HasMaxLength(UnitComponentSlot.DISPLAY_NAME_MAX_LENGTH);

            // Normalized (image-relative) slot offsets. 9,4 gives 0.0001 granularity with ample range
            // for the rare slot that sits well outside the image bounds.
            entity.Property(e => e.UIXPosition)
                .HasPrecision(9, 4);

            entity.Property(e => e.UIYPosition)
                .HasPrecision(9, 4);

            // Slots hang off the body extension (see UnitComponentBody config for the FK); only bodies
            // expose slots.
            entity.HasIndex(e => e.UnitComponentBodyId)
                .HasDatabaseName("IX_UnitComponentSlot_Body");
        });

        // --- UnitComponentSlotRule -------------------------------------------------
        modelBuilder.Entity<UnitComponentSlotRule>(entity =>
        {
            entity.ToTable("UnitComponentSlotRule", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.RuleType)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(e => e.Operator)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(e => e.Value)
                .HasMaxLength(UnitComponentSlotRule.VALUE_MAX_LENGTH)
                .IsRequired();

            entity.HasOne(e => e.Slot)
                .WithMany(e => e.Rules)
                .HasForeignKey(e => e.UnitComponentSlotId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentSlotRule_UnitComponentSlot_UnitComponentSlotId");

            entity.HasIndex(e => e.UnitComponentSlotId)
                .HasDatabaseName("IX_UnitComponentSlotRule_Slot");
        });

        // --- Tag -------------------------------------------------------------------
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tag", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasMaxLength(Tag.CODE_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.DisplayName)
                .HasMaxLength(Tag.DISPLAY_NAME_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.TagType)
                .HasConversion<int>()
                .IsRequired();

            // Nullable — only organisation-group tags carry a domain; stored as the enum's int value.
            entity.Property(e => e.MovementDomain)
                .HasConversion<int>();

            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UX_Tag_Code");

            // The TagType enum column doubles as a FK into the TagType metadata table (which is keyed by
            // the same enum). Restrict deletes so a tag type can't be removed while tags still reference it.
            entity.HasOne<TagTypeInfo>()
                .WithMany()
                .HasForeignKey(e => e.TagType)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Tag_TagType_TagType");
        });

        // --- TagTypeInfo -----------------------------------------------------------
        modelBuilder.Entity<TagTypeInfo>(entity =>
        {
            // Table is literally "TagType"; the entity is TagTypeInfo to avoid the enum name collision.
            entity.ToTable("TagType", schema);

            // Keyed by the enum value itself — never database-generated.
            entity.HasKey(e => e.TagTypeId);

            entity.Property(e => e.TagTypeId)
                .HasConversion<int>()
                .ValueGeneratedNever();

            entity.Property(e => e.DisplayName)
                .HasMaxLength(TagTypeInfo.DISPLAY_NAME_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.Description)
                .HasMaxLength(TagTypeInfo.DESCRIPTION_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.Colour)
                .HasMaxLength(TagTypeInfo.COLOUR_MAX_LENGTH);

            entity.Property(e => e.Icon)
                .HasMaxLength(TagTypeInfo.ICON_MAX_LENGTH);

            // Tag type rows are no longer seeded here via HasData — the database seed tool (EntitySetupTool
            // -> Database -> Seed) now owns the initial TagType catalogue. See TagTypeSeedData /
            // DatabaseSeedExecutor. A migration removes the previously HasData-seeded rows so this stays the
            // single source of truth.
        });

        // --- UnitComponentTag (join) -----------------------------------------------
        modelBuilder.Entity<UnitComponentTag>(entity =>
        {
            entity.ToTable("UnitComponentTag", schema);

            entity.HasKey(e => new { e.UnitComponentId, e.TagId });

            entity.HasOne(e => e.UnitComponent)
                .WithMany(e => e.Tags)
                .HasForeignKey(e => e.UnitComponentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentTag_UnitComponent_UnitComponentId");

            entity.HasOne(e => e.Tag)
                .WithMany(e => e.ComponentTags)
                .HasForeignKey(e => e.TagId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentTag_Tag_TagId");

            entity.HasIndex(e => e.TagId)
                .HasDatabaseName("IX_UnitComponentTag_Tag");
        });

        // --- PlayerUnlockedComponent -----------------------------------------------
        modelBuilder.Entity<PlayerUnlockedComponent>(entity =>
        {
            entity.ToTable("PlayerUnlockedComponent", schema);

            entity.HasKey(e => new { e.PlayerId, e.UnitComponentId });

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnlockedComponent_Player_PlayerId");

            entity.HasOne(e => e.UnitComponent)
                .WithMany()
                .HasForeignKey(e => e.UnitComponentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnlockedComponent_UnitComponent_UnitComponentId");

            entity.HasIndex(e => e.UnitComponentId)
                .HasDatabaseName("IX_PlayerUnlockedComponent_UnitComponent");
        });
    }

    /// <summary>
    /// Configures assembled unit templates, their fitted components, and per-player unlock state.
    /// </summary>
    private static void ConfigureUnitTemplates(ModelBuilder modelBuilder, string schema)
    {
        // --- UnitTemplate ----------------------------------------------------------
        modelBuilder.Entity<UnitTemplate>(entity =>
        {
            entity.ToTable("UnitTemplate", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasMaxLength(UnitTemplate.CODE_MAX_LENGTH);

            entity.Property(e => e.DisplayName)
                .HasMaxLength(UnitTemplate.DISPLAY_NAME_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.MapImagePath)
                .HasMaxLength(UnitTemplate.MAP_IMAGE_PATH_MAX_LENGTH)
                .IsRequired();

            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitTemplate_GameIndex_GameId");

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitTemplate_Player_PlayerId");

            entity.HasOne(e => e.BodyComponent)
                .WithMany()
                .HasForeignKey(e => e.BodyComponentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitTemplate_UnitComponent_BodyComponentId");

            // Codes only need to be unique among the global/common templates that have one.
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL")
                .HasDatabaseName("UX_UnitTemplate_Code");

            entity.HasIndex(e => e.BodyComponentId)
                .HasDatabaseName("IX_UnitTemplate_BodyComponent");
        });

        // --- UnitTemplateComponent -------------------------------------------------
        modelBuilder.Entity<UnitTemplateComponent>(entity =>
        {
            entity.ToTable("UnitTemplateComponent", schema);

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.UnitTemplate)
                .WithMany(e => e.Components)
                .HasForeignKey(e => e.UnitTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitTemplateComponent_UnitTemplate_UnitTemplateId");

            entity.HasOne(e => e.UnitComponentSlot)
                .WithMany()
                .HasForeignKey(e => e.UnitComponentSlotId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitTemplateComponent_UnitComponentSlot_UnitComponentSlotId");

            entity.HasOne(e => e.UnitComponent)
                .WithMany()
                .HasForeignKey(e => e.UnitComponentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitTemplateComponent_UnitComponent_UnitComponentId");

            // One component per slot of the template's body.
            entity.HasIndex(e => new { e.UnitTemplateId, e.UnitComponentSlotId })
                .IsUnique()
                .HasDatabaseName("UX_UnitTemplateComponent_Template_Slot");

            entity.HasIndex(e => e.UnitComponentSlotId)
                .HasDatabaseName("IX_UnitTemplateComponent_Slot");

            entity.HasIndex(e => e.UnitComponentId)
                .HasDatabaseName("IX_UnitTemplateComponent_Component");
        });

        // --- PlayerUnlockedTemplate ------------------------------------------------
        modelBuilder.Entity<PlayerUnlockedTemplate>(entity =>
        {
            entity.ToTable("PlayerUnlockedTemplate", schema);

            entity.HasKey(e => new { e.PlayerId, e.UnitTemplateId });

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnlockedTemplate_Player_PlayerId");

            entity.HasOne(e => e.UnitTemplate)
                .WithMany(e => e.PlayerStates)
                .HasForeignKey(e => e.UnitTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnlockedTemplate_UnitTemplate_UnitTemplateId");

            entity.HasIndex(e => e.UnitTemplateId)
                .HasDatabaseName("IX_PlayerUnlockedTemplate_UnitTemplate");
        });

        // --- CompiledUnitTemplate (1:1 snapshot of a saved UnitTemplate) -----------
        modelBuilder.Entity<CompiledUnitTemplate>(entity =>
        {
            entity.ToTable("CompiledUnitTemplate", schema);

            // Shares the primary key of the owning UnitTemplate.
            entity.HasKey(e => e.UnitTemplateId);

            entity.Property(e => e.DisplayName)
                .HasMaxLength(UnitTemplate.DISPLAY_NAME_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.MapImagePath)
                .HasMaxLength(UnitTemplate.MAP_IMAGE_PATH_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.MovementDomain)
                .HasConversion<int>()
                .IsRequired();

            // Two DamageProfile complex types share this table, so give each its own column prefix.
            entity.ComplexProperty(e => e.Attack, attack =>
            {
                attack.Property(p => p.Slash).HasColumnName("AttackSlash");
                attack.Property(p => p.Pierce).HasColumnName("AttackPierce");
                attack.Property(p => p.Crush).HasColumnName("AttackCrush");
                attack.Property(p => p.Burn).HasColumnName("AttackBurn");
                attack.Property(p => p.Poison).HasColumnName("AttackPoison");
            });

            entity.ComplexProperty(e => e.Resistance, resistance =>
            {
                resistance.Property(p => p.Slash).HasColumnName("ResistSlash");
                resistance.Property(p => p.Pierce).HasColumnName("ResistPierce");
                resistance.Property(p => p.Crush).HasColumnName("ResistCrush");
                resistance.Property(p => p.Burn).HasColumnName("ResistBurn");
                resistance.Property(p => p.Poison).HasColumnName("ResistPoison");
            });

            entity.HasOne(e => e.UnitTemplate)
                .WithOne()
                .HasForeignKey<CompiledUnitTemplate>(e => e.UnitTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CompiledUnitTemplate_UnitTemplate_UnitTemplateId");
        });

        // --- CompiledUnitTemplateComponent -----------------------------------------
        modelBuilder.Entity<CompiledUnitTemplateComponent>(entity =>
        {
            entity.ToTable("CompiledUnitTemplateComponent", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code)
                .HasMaxLength(UnitComponent.CODE_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.DisplayName)
                .HasMaxLength(UnitComponent.DISPLAY_NAME_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.ComponentType)
                .HasConversion<int>()
                .IsRequired();

            entity.HasOne(e => e.CompiledUnitTemplate)
                .WithMany(e => e.Components)
                .HasForeignKey(e => e.CompiledUnitTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CompiledUnitTemplateComponent_CompiledUnitTemplate_CompiledUnitTemplateId");

            entity.HasIndex(e => e.CompiledUnitTemplateId)
                .HasDatabaseName("IX_CompiledUnitTemplateComponent_Template");
        });

        // --- CompiledUnitTemplateCapability ----------------------------------------
        modelBuilder.Entity<CompiledUnitTemplateCapability>(entity =>
        {
            entity.ToTable("CompiledUnitTemplateCapability", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.TagCode)
                .HasMaxLength(Tag.CODE_MAX_LENGTH)
                .IsRequired();

            entity.Property(e => e.DisplayName)
                .HasMaxLength(Tag.DISPLAY_NAME_MAX_LENGTH)
                .IsRequired();

            entity.HasOne(e => e.CompiledUnitTemplate)
                .WithMany(e => e.Capabilities)
                .HasForeignKey(e => e.CompiledUnitTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CompiledUnitTemplateCapability_CompiledUnitTemplate_CompiledUnitTemplateId");

            // A capability appears at most once per compiled template.
            entity.HasIndex(e => new { e.CompiledUnitTemplateId, e.TagCode })
                .IsUnique()
                .HasDatabaseName("UX_CompiledUnitTemplateCapability_Template_Tag");
        });
    }


    /// <summary>
    /// Configures the per-player military organisation tables: officer rank ladders and organisation
    /// structures (with their tiers).
    /// </summary>
    private static void ConfigureMilitarySchema(ModelBuilder modelBuilder)
    {
        const string schema = "military";

        // --- PlayerOfficerRank -----------------------------------------------------
        modelBuilder.Entity<PlayerOfficerRank>(entity =>
        {
            entity.ToTable("PlayerOfficerRank", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.MovementDomain)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(e => e.Title)
                .HasMaxLength(PlayerOfficerRank.TITLE_MAX_LENGTH)
                .IsRequired();

            // One row per player / domain / level — the natural key the save handler upserts against.
            entity.HasIndex(e => new { e.PlayerId, e.MovementDomain, e.Level })
                .IsUnique()
                .HasDatabaseName("UX_PlayerOfficerRank_Player_Domain_Level");

            // NoAction (not cascade): Player already sits at the head of several cascade paths, so cascading
            // a player delete through here would create multiple cascade paths into Player.
            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerOfficerRank_Player_PlayerId");
        });

        // --- PlayerOrganisationStructure -------------------------------------------
        modelBuilder.Entity<PlayerOrganisationStructure>(entity =>
        {
            entity.ToTable("PlayerOrganisationStructure", schema);

            entity.HasKey(e => e.Id);

            // At most one structure per player per tag.
            entity.HasIndex(e => new { e.PlayerId, e.TagId })
                .IsUnique()
                .HasDatabaseName("UX_PlayerOrganisationStructure_Player_Tag");

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerOrganisationStructure_Player_PlayerId");

            // Restrict: a MilitaryOrganisationalGroup tag can't be deleted while a structure still points at it.
            entity.HasOne(e => e.Tag)
                .WithMany()
                .HasForeignKey(e => e.TagId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_PlayerOrganisationStructure_Tag_TagId");

            entity.HasMany(e => e.Tiers)
                .WithOne(t => t.Structure)
                .HasForeignKey(t => t.StructureId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PlayerOrganisationStructureTier_Structure_StructureId");
        });

        // --- PlayerOrganisationStructureTier ---------------------------------------
        modelBuilder.Entity<PlayerOrganisationStructureTier>(entity =>
        {
            entity.ToTable("PlayerOrganisationStructureTier", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .HasMaxLength(PlayerOrganisationStructureTier.NAME_MAX_LENGTH)
                .IsRequired();

            // One row per structure / tier index.
            entity.HasIndex(e => new { e.StructureId, e.TierIndex })
                .IsUnique()
                .HasDatabaseName("UX_PlayerOrganisationStructureTier_Structure_TierIndex");
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
