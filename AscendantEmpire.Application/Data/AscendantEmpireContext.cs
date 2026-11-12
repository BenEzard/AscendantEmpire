using AscendantEmpire.Domain.Entities.Battles;
using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Identity;
using AscendantEmpire.Domain.Entities.Maps.BattleMaps;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Maps.MapResources;
using AscendantEmpire.Domain.Entities.Military;
using AscendantEmpire.Domain.Entities.Assets;
using AscendantEmpire.Domain.Entities.Settlements;
using AscendantEmpire.Domain.Entities.Structures;
using AscendantEmpire.Domain.Entities.Technologies;
using AscendantEmpire.Domain.Entities.BackgroundProcessing;
using AscendantEmpire.Domain.Entities.Turns;
using AscendantEmpire.Domain.Entities.UnitDesigns;
using AscendantEmpire.Domain.Entities.Units;
using AscendantEmpire.Domain.Entities.Util;
using AscendantEmpire.Domain.Enums.Assets;
using AscendantEmpire.Domain.Enums.Battle;
using AscendantEmpire.Domain.Enums.Forces;
using AscendantEmpire.Domain.Enums.MapResources;
using AscendantEmpire.Domain.Enums.Maps;
using AscendantEmpire.Domain.Enums.Structures;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AscendantEmpire.Application.Data;

public class AscendantEmpireContext : DbContext
{
    #region Battle Schema
    public DbSet<Battle> Battles { get; set; }
    public DbSet<BattleParticipant> BattleParticipants { get; set; }

    /// <summary>
    /// The raw combat feed: one append-only row per engagement commencing, gaining or losing a side, or concluding during a Turn. Written only through <c>IBattleLifecycleWriter</c>.
    /// </summary>
    public DbSet<BattleHistory> BattleHistory { get; set; }
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

    /// <summary>
    /// MapResources the map editor has placed on gallery tiles, at most two per tile. Copied into <see cref="MapResourceInstances"/> at game setup.
    /// </summary>
    public DbSet<CampaignMapGalleryResource> CampaignMapGalleryResources { get; set; }
    #endregion

    #region Map Resources (map schema)
    /// <summary>
    /// Groups of like MapResourceTypes (Food, Metals, Wood...) for the map editor.
    /// </summary>
    public DbSet<MapResourceCategory> MapResourceCategories { get; set; }

    /// <summary>
    /// The kinds of MapResource that can be on a tile, e.g. Berry, Deer, Coal Seam. See <c>docs/design/campaign-map/map-resources.md</c>.
    /// </summary>
    public DbSet<MapResourceType> MapResourceTypes { get; set; }

    /// <summary>
    /// The Assets one unit of a MapResourceType gives when extracted.
    /// </summary>
    public DbSet<MapResourceTypeYield> MapResourceTypeYields { get; set; }

    /// <summary>
    /// Where each MapResourceType can exist (per Biome and optional Geography), and its values there.
    /// </summary>
    public DbSet<MapResourceHabitat> MapResourceHabitats { get; set; }

    /// <summary>
    /// The sets of starting amounts a Distribution MapResource is created from.
    /// </summary>
    public DbSet<MapResourceDistributionScenario> MapResourceDistributionScenarios { get; set; }

    /// <summary>
    /// The starting amount of each Asset in a Distribution scenario.
    /// </summary>
    public DbSet<MapResourceDistributionScenarioAmount> MapResourceDistributionScenarioAmounts { get; set; }

    /// <summary>
    /// How likely each Distribution scenario is to be picked in each Biome.
    /// </summary>
    public DbSet<MapResourceDistributionScenarioBiomeWeight> MapResourceDistributionScenarioBiomeWeights { get; set; }

    /// <summary>
    /// The layer templates of stratified MapResourceTypes.
    /// </summary>
    public DbSet<MapResourceTypeLayer> MapResourceTypeLayers { get; set; }
    #endregion

    #region Game Schema
    /// <summary>
    /// Index of current games.
    /// </summary>
    public DbSet<GameIndex> GameIndex { get; set; }

    /// <summary>
    /// The rule settings chosen for each game. One row per game, created with the GameIndex row.
    /// </summary>
    public DbSet<GameConfiguration> GameConfigurations { get; set; }

    /// <summary>
    /// List of Players in specific games.
    /// </summary>
    public DbSet<Player> Players { get; set; }

    /// <summary>
    /// Master copy of Campaign Maps for specific games.
    /// </summary>
    public DbSet<CampaignMap> CampaignMaps { get; set; }

    /// <summary>
    /// Every Player's political claims on campaign map tiles, one row per (Player, tile). Sparse: rows exist only where a claim does.
    /// </summary>
    public DbSet<CampaignMapClaim> CampaignMapClaims { get; set; }

    /// <summary>
    /// Player-specific copy of Campaign Maps for specific games.
    /// </summary>
    public DbSet<PlayerCampaignMap> PlayerCampaignMaps { get; set; }

    /// <summary>
    /// Each battle participant's knowledge of the battle map they fought on: one row per (Player, battle map tile). Written in bulk by <c>IPlayerBattleMapService</c>.
    /// </summary>
    public DbSet<PlayerBattleMap> PlayerBattleMaps { get; set; }

    /// <summary>
    /// The modifiers each Player holds to their Forage, Hunt, Fish and Fodder values, one row per source, resource and biome. Summed by <c>IPlayerSustenanceValueReader</c>.
    /// </summary>
    public DbSet<PlayerSustenanceModifier> PlayerSustenanceModifiers { get; set; }

    /// <summary>
    /// The Player-neutral Forage, Hunt, Fish and Fodder taken from each campaign map tile that has been fed on, and the last Round it was fed on. Written by the end-of-turn
    /// <c>Gathering</c> process.
    /// </summary>
    public DbSet<CampaignMapDepletion> CampaignMapDepletions { get; set; }

    /// <summary>
    /// The MapResources on each game's campaign map tiles, Player-neutral. Created at game setup from <see cref="CampaignMapGalleryResources"/>, and by spread.
    /// </summary>
    public DbSet<MapResourceInstance> MapResourceInstances { get; set; }

    /// <summary>
    /// Each Distribution instance's own amount of each of its Assets.
    /// </summary>
    public DbSet<MapResourceInstanceAmount> MapResourceInstanceAmounts { get; set; }

    /// <summary>
    /// Each stratified instance's layers.
    /// </summary>
    public DbSet<MapResourceInstanceLayer> MapResourceInstanceLayers { get; set; }

    /// <summary>
    /// When each Player first saw, and researched, each MapResourceType.
    /// </summary>
    public DbSet<PlayerMapResourceTypeKnowledge> PlayerMapResourceTypeKnowledge { get; set; }

    /// <summary>
    /// The MapResource instances each Player has discovered.
    /// </summary>
    public DbSet<PlayerKnownMapResource> PlayerKnownMapResources { get; set; }

    /// <summary>
    /// The modifiers each Player holds to their MapResourceType values, one row per source, type, target, Biome and Geography.
    /// </summary>
    public DbSet<PlayerMapResourceModifier> PlayerMapResourceModifiers { get; set; }

    /// <summary>
    /// The per-Player switches turning discovery, a yielded Asset or a layer survey on or off. An off always wins.
    /// </summary>
    public DbSet<PlayerMapResourceSwitch> PlayerMapResourceSwitches { get; set; }

    /// <summary>
    /// Each Player's last observed owner, progress and state for every Structure they have watched change.
    /// </summary>
    public DbSet<PlayerKnownStructure> PlayerKnownStructures { get; set; }

    public DbSet<StructureInstance> StructureInstances { get; set; }

    /// <summary>
    /// The global catalogue of kinds of Structure — what each one requires, sees and looks like. No <c>GameId</c>: one catalogue serves every Game.
    /// </summary>
    public DbSet<Structure> Structures { get; set; }

    /// <summary>
    /// The Settlements of every Game, each sitting on the <see cref="StructureInstance"/> that stands on its tile.
    /// </summary>
    public DbSet<Settlement> Settlements { get; set; }

    /// <summary>
    /// The raw structure feed: one append-only row per Structure founded, built, captured or razed during a Turn. Lives in the <c>structure</c> schema; written only through
    /// <c>IStructureLifecycleWriter</c>.
    /// </summary>
    public DbSet<StructureHistory> StructureHistory { get; set; }

    /// <summary>
    /// The intelligence feed: one row per thing a Player learned while they were not looking. Raised by the end-of-turn sightings step.
    /// </summary>
    public DbSet<PlayerReport> PlayerReports { get; set; }

    /// <summary>
    /// The ordered tiles each <see cref="PlayerReport"/> saw its subject take.
    /// </summary>
    public DbSet<PlayerReportPathTile> PlayerReportPathTiles { get; set; }

    /// <summary>
    /// Each Player's last observed position and state for every Force they have watched.
    /// </summary>
    public DbSet<PlayerKnownForce> PlayerKnownForces { get; set; }
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

    /// <summary>
    /// Capabilities granted to users. A user holds a permission when a row exists for the pair.
    /// </summary>
    public DbSet<UserPermission> UserPermissions { get; set; }
    #endregion

    #region Assets Schema
    public DbSet<AssetMetaCategory> AssetMetaCategories { get; set; }
    public DbSet<AssetCategory> AssetCategories { get; set; }
    public DbSet<AssetGroup> AssetGroups { get; set; }
    public DbSet<Asset> Assets { get; set; }

    /// <summary>
    /// How each Asset is kept, as against what it is. What a per-settlement capacity cap binds against.
    /// </summary>
    public DbSet<StorageClass> StorageClasses { get; set; }

    /// <summary>
    /// Groups of Assets that each satisfy one ongoing need — the foods a man will eat, the fuels an engine will burn.
    /// </summary>
    public DbSet<SupplySet> SupplySets { get; set; }

    /// <summary>
    /// The Assets in each SupplySet, with what one unit of the need costs in each and how heavily it is weighted in a draw.
    /// </summary>
    public DbSet<SupplySetAsset> SupplySetAssets { get; set; }

    /// <summary>
    /// Which AssetMetaCategories each Player has unlocked, and so which icons their Assets toolbar group shows.
    /// </summary>
    public DbSet<PlayerUnlockedAssetMetaCategory> PlayerUnlockedAssetMetaCategories { get; set; }

    /// <summary>
    /// Which Assets each Player has unlocked. The categories and groups between are derived from these rather than unlocked in their own right.
    /// </summary>
    public DbSet<PlayerUnlockedAsset> PlayerUnlockedAssets { get; set; }

    /// <summary>
    /// One Settlement's store of Assets, owned by a Player. Which stores can reach each other is the NetworkKey, not the row.
    /// </summary>
    public DbSet<AssetBank> AssetBanks { get; set; }

    /// <summary>
    /// How much of each Asset each bank holds.
    /// </summary>
    public DbSet<AssetBankBalance> AssetBankBalances { get; set; }

    /// <summary>
    /// Each Player's empire-wide strategic reserve intent, one row per Asset they have stated one for.
    /// </summary>
    public DbSet<PlayerStrategicReservePolicy> PlayerStrategicReservePolicies { get; set; }
    #endregion

    #region Technology Schema
    public DbSet<Technology> Technologies { get; set; }
    public DbSet<ExperienceType> ExperienceTypes { get; set; }

    /// <summary>
    /// The Assets and quantities each Technology costs to unlock. No rows for a Technology means it is free.
    /// </summary>
    public DbSet<TechnologyAcquisitionCost> TechnologyAcquisitionCosts { get; set; }

    /// <summary>
    /// The prerequisite edges between Technologies. Mandatory edges must all be met; optional edges are alternatives.
    /// </summary>
    public DbSet<TechnologyPrerequisite> TechnologyPrerequisites { get; set; }

    /// <summary>
    /// What each Technology unlocks or retires when discovered - the source of the per-player unlock rows elsewhere.
    /// </summary>
    public DbSet<TechnologyGrant> TechnologyGrants { get; set; }

    /// <summary>
    /// Which kinds of experience feed which Technologies. A mapping only: the points per action are decided by the action.
    /// </summary>
    public DbSet<TechnologyExperienceType> TechnologyExperienceTypes { get; set; }

    /// <summary>
    /// Each Player's independent progress toward each Technology. A row exists from setup, so presence does not mean unlocked.
    /// </summary>
    public DbSet<PlayerTechnology> PlayerTechnologies { get; set; }

    /// <summary>
    /// Each Player's research project selection and carried-over points. One row per Player.
    /// </summary>
    public DbSet<PlayerResearchState> PlayerResearchStates { get; set; }

    /// <summary>
    /// Experience earned this turn and not yet distributed. Empty between turns.
    /// </summary>
    public DbSet<PendingExperienceAccrual> PendingExperienceAccruals { get; set; }

    /// <summary>
    /// Experience that had no Technology to go into. Never spent; kept only for comparing Players.
    /// </summary>
    public DbSet<PlayerUnusedExperience> PlayerUnusedExperience { get; set; }

    /// <summary>
    /// Modifiers to how many research points a Player generates per turn, by bucket and by granting source.
    /// </summary>
    public DbSet<PlayerResearchModifier> PlayerResearchModifiers { get; set; }

    /// <summary>
    /// Per-round summary of each Player's research points by bucket. Append-only.
    /// </summary>
    public DbSet<ResearchHistory> ResearchHistories { get; set; }
    #endregion

    #region Turn Schema
    public DbSet<TurnQueue> TurnQueues { get; set; }
    public DbSet<TurnQueueParticipant> TurnQueueParticipants { get; set; }

    /// <summary>
    /// One row per Player Turn taken in any lane, carrying the sequence counter the raw activity feeds draw from.
    /// </summary>
    public DbSet<PlayerTurn> PlayerTurns { get; set; }
    #endregion

    #region Units

    public DbSet<UnitInstance> UnitInstances { get; set; }
    public DbSet<Command> Commands { get; set; }
    public DbSet<ForceOrder> ForceOrders { get; set; }
    public DbSet<ForceOrderMovementStep> ForceOrderMovementSteps { get; set; }
    public DbSet<ForceOrderParticipant> ForceOrderParticipants { get; set; }

    /// <summary>
    /// The raw movement feed: one append-only row per Force created, moved or destroyed during a Turn. Written only through <c>IForceLifecycleWriter</c>.
    /// </summary>
    public DbSet<MovementHistory> MovementHistory { get; set; }

    #endregion

    #region Unit Designs
    public DbSet<UnitDesign> UnitDesigns { get; set; }
    public DbSet<CompiledDesignStats> UnitDesignStats { get; set; }
    public DbSet<PlayerUnitDesignState> PlayerUnitDesignStates { get; set; }

    // --- Component-based unit design system (global catalogue, unlocks, templates) ---

    /// <summary>
    /// Global reference catalogue of buildable unit components (body, weapon, engine, etc.).
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
    /// The kinds of ammunition a weapon accepts, each with what one attack consumes of it.
    /// </summary>
    public DbSet<UnitComponentWeaponAmmunition> UnitComponentWeaponAmmunitions { get; set; }

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
    /// Global, reusable bonus/buff tag catalogue.
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
    /// What each component consumes, as a quantity of one SupplySet.
    /// </summary>
    public DbSet<UnitComponentSupplyRequirement> UnitComponentSupplyRequirements { get; set; }

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

        ConfigureAssetsSchema(modelBuilder);

        ConfigureBattleSchema(modelBuilder);

        ConfigureGameSchema(modelBuilder);

        ConfigureAuthSchema(modelBuilder);

        ConfigureMapSchema(modelBuilder);

        ConfigureMapGallerySchema(modelBuilder);

        ConfigureSettlementSchema(modelBuilder);

        ConfigureStructureSchema(modelBuilder);

        ConfigureSystemSchema(modelBuilder);

        ConfigureTurnSchema(modelBuilder);

        ConfigureUnitSchema(modelBuilder);

        ConfigureUnitDesignSchema(modelBuilder);

        ConfigureMilitarySchema(modelBuilder);

        ConfigureTechnologySchema(modelBuilder);
    }

    private static void ConfigureAssetsSchema(ModelBuilder modelBuilder)
    {
        const string schema = "assets";


        // Not a level of the hierarchy below - a sibling catalogue the Assets point at, placed first because they do. Both columns are uniquely indexed:
        // Code because it is the upsert's match key, DisplayName because a capacity refusal names the class that bound and two classes reading alike
        // would make that message unexplainable.
        modelBuilder.Entity<StorageClass>(entity =>
        {
            entity.ToTable("StorageClass", schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Code).HasMaxLength(StorageClass.MAX_CODE_LENGTH).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(StorageClass.MAX_DISPLAY_NAME_LENGTH).IsRequired();

            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_StorageClass_Code");
            entity.HasIndex(x => x.DisplayName).IsUnique().HasDatabaseName("UX_StorageClass_DisplayName");
        });

        // Code is the match key every asset upsert works from, so it is length-capped and uniquely indexed at all four levels. Without the unique index
        // nothing stops two rows sharing a code, and the upsert would then silently update whichever the dictionary happened to keep.
        modelBuilder.Entity<AssetMetaCategory>(entity =>
        {
            entity.ToTable("AssetMetaCategory", schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(AssetMetaCategory.MAX_CODE_LENGTH).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(AssetMetaCategory.MAX_DISPLAY_NAME_LENGTH).IsRequired();
            entity.Property(x => x.IconPath).HasMaxLength(AssetMetaCategory.MAX_ICON_PATH_LENGTH);

            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.DisplayName).IsUnique();
        });

        modelBuilder.Entity<AssetCategory>(entity =>
        {
            entity.ToTable("AssetCategory", schema);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(AssetCategory.MAX_CODE_LENGTH).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(AssetCategory.MAX_DISPLAY_NAME_LENGTH).IsRequired();

            // Sentinel stated for the same reason as Asset.StorageScope below: 0 matches no member, so it can only mean "unset" and the store default is
            // always the right answer on insert.
            entity.Property(x => x.SubjectScope)
                .HasDefaultValue(AssetSubjectScope.Own)
                .HasSentinel((AssetSubjectScope)0);

            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.AssetMetaCategoryId, x.DisplayName }).IsUnique();

            entity.HasOne(x => x.MetaCategory)
                .WithMany(x => x.Categories)
                .HasForeignKey(x => x.AssetMetaCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // NEW: 1 -> many AssetGroups
            entity.HasMany(x => x.AssetGroups)
                .WithOne(x => x.Category)
                .HasForeignKey(x => x.AssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AssetGroup>(entity =>
        {
            entity.ToTable("AssetGroup", schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Code).HasMaxLength(AssetGroup.MAX_CODE_LENGTH).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(AssetGroup.MAX_DISPLAY_NAME_LENGTH).IsRequired();

            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => new { x.AssetCategoryId, x.DisplayName }).IsUnique();

            // GroupOrder is per-category
            entity.HasIndex(x => new { x.AssetCategoryId, x.SortOrder }).IsUnique();

            entity.HasOne(x => x.Category)
                .WithMany(x => x.AssetGroups)
                .HasForeignKey(x => x.AssetCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            // Enforce that exactly one of AssetGroupId / AssetCategoryId is non-null.
            entity.ToTable("Asset", schema, t => t.HasCheckConstraint(
                "CK_Asset_GroupOrCategoryXor",
                "([AssetGroupId] IS NOT NULL AND [AssetCategoryId] IS NULL) OR ([AssetGroupId] IS NULL AND [AssetCategoryId] IS NOT NULL)"));

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Code).HasMaxLength(Asset.MAX_CODE_LENGTH).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(Asset.MAX_DISPLAY_NAME_LENGTH).IsRequired();

            // Defaulted in the database as well as on the entity, following SubjectScope above, so the column lands on existing rows as SettlementBank
            // rather than as a zero that matches no member of the enum. The sentinel says that a zero means "unset", which EF cannot infer and warns about.
            entity.Property(x => x.StorageScope)
                .HasDefaultValue(AssetStorageScope.SettlementBank)
                .HasSentinel((AssetStorageScope)0);

            entity.HasIndex(x => x.Code).IsUnique();
            entity.HasIndex(x => x.DisplayName);

            // Grouped path: asset belongs to an AssetGroup.
            entity.HasOne(x => x.Group)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.AssetGroupId)
                .OnDelete(DeleteBehavior.NoAction);

            // Standalone path: asset belongs directly to an AssetCategory.
            entity.HasOne(x => x.Category)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.AssetCategoryId)
                .OnDelete(DeleteBehavior.NoAction);

            // NoAction, following every other row that names a catalogue type, so a class an Asset still names cannot be deleted.
            entity.HasOne(x => x.StorageClass)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.StorageClassId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // --- Per-player unlock state -------------------------------------------------
        // These live here with the catalogue they gate rather than in the game schema, following PlayerUnlockedComponent, which sits with the unit-design
        // catalogue. AssetCategory and AssetGroup deliberately have no equivalent: their visibility is derived from the Assets beneath them.
        modelBuilder.Entity<PlayerUnlockedAssetMetaCategory>(entity =>
        {
            entity.ToTable("PlayerUnlockedAssetMetaCategory", schema);

            entity.HasKey(e => new { e.PlayerId, e.AssetMetaCategoryId });

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnlockedAssetMetaCategory_Player_PlayerId");

            entity.HasOne(e => e.MetaCategory)
                .WithMany()
                .HasForeignKey(e => e.AssetMetaCategoryId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnlockedAssetMetaCategory_AssetMetaCategory_AssetMetaCategoryId");

            entity.HasIndex(e => e.AssetMetaCategoryId)
                .HasDatabaseName("IX_PlayerUnlockedAssetMetaCategory_AssetMetaCategory");
        });

        modelBuilder.Entity<PlayerUnlockedAsset>(entity =>
        {
            entity.ToTable("PlayerUnlockedAsset", schema);

            entity.HasKey(e => new { e.PlayerId, e.AssetId });

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnlockedAsset_Player_PlayerId");

            entity.HasOne(e => e.Asset)
                .WithMany()
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnlockedAsset_Asset_AssetId");

            entity.HasIndex(e => e.AssetId)
                .HasDatabaseName("IX_PlayerUnlockedAsset_Asset");
        });

        // --- Asset banks -------------------------------------------------------------
        // One store per settlement, networked by a shared NetworkKey. These sit in the assets schema with the catalogue they hold quantities of, rather than
        // in the settlements schema, because what they hold is a quantity of an Asset and ownership is per Player.
        modelBuilder.Entity<AssetBank>(entity =>
        {
            entity.ToTable("AssetBank", schema);

            entity.HasKey(x => x.Id);

            // A real foreign key rather than a bare PlayerId index: a bank with no owning Player is meaningless, and nothing else would stop one existing.
            // NoAction, like every other Player reference, so a cascade cannot ripple out from deleting a Player.
            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_AssetBank_Player_PlayerId");

            // No navigation back from Settlement: the assets schema reaches into settlements, not the other way about.
            entity.HasOne(x => x.Settlement)
                .WithOne()
                .HasForeignKey<AssetBank>(x => x.SettlementId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_AssetBank_Settlements_SettlementId");

            // Unique, which is what makes "a settlement has exactly one asset bank at any time" a schema rule rather than one a service has to check.
            entity.HasIndex(x => x.SettlementId)
                .IsUnique()
                .HasDatabaseName("UX_AssetBank_Settlement");

            // PlayerId is this index's left-most column, so it needs no index of its own. Every network read filters on both.
            entity.HasIndex(x => new { x.PlayerId, x.NetworkKey })
                .HasDatabaseName("IX_AssetBank_Player_NetworkKey");
        });

        modelBuilder.Entity<AssetBankBalance>(entity =>
        {
            entity.ToTable("AssetBankBalance", schema);

            // One row per Asset per bank, so a drawdown has a single row to debit rather than a set to reconcile.
            entity.HasKey(x => new { x.AssetBankId, x.AssetId });

            entity.Property(x => x.Quantity).IsRequired();

            // Defaulted so that every row the refactor's earlier steps already wrote reads as holding no reserve, rather than needing a backfill.
            entity.Property(x => x.ReservedQuantity).IsRequired().HasDefaultValue(0L);

            entity.HasOne(x => x.Bank)
                .WithMany(x => x.Balances)
                .HasForeignKey(x => x.AssetBankId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(x => x.Asset)
                .WithMany(x => x.BankBalances)
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<PlayerStrategicReservePolicy>(entity =>
        {
            entity.ToTable("PlayerStrategicReservePolicy", schema, table => table.HasCheckConstraint(
                "CK_PlayerStrategicReservePolicy_DiversionPercent",
                $"[DiversionPercent] >= 0 AND [DiversionPercent] <= {PlayerStrategicReservePolicy.MAX_DIVERSION_PERCENT}"));

            // One intent per Player per Asset. The policy is empire-wide, so there is deliberately no Settlement in this key.
            entity.HasKey(x => new { x.PlayerId, x.AssetId });

            entity.Property(x => x.TargetQuantity).IsRequired();
            entity.Property(x => x.DiversionPercent).IsRequired();

            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerStrategicReservePolicy_Player_PlayerId");

            entity.HasOne(x => x.Asset)
                .WithMany()
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerStrategicReservePolicy_Asset_AssetId");

            entity.HasIndex(x => x.AssetId)
                .HasDatabaseName("IX_PlayerStrategicReservePolicy_Asset");
        });

        ConfigureSupplySets(modelBuilder, schema);
    }

    /// <summary>
    /// Configures the supply sets and their members.
    /// </summary>
    /// <remarks>
    /// A grouping of Assets rather than a unit design concern, so it belongs beside the catalogue it draws from. What consumes a set lives in
    /// <c>unitdesigns</c> — see <c>UnitComponentSupplyRequirement</c> in <see cref="ConfigureUnitComponentCatalogue"/>.
    /// </remarks>
    private static void ConfigureSupplySets(ModelBuilder modelBuilder, string schema)
    {
        modelBuilder.Entity<SupplySet>(entity =>
        {
            entity.ToTable("SupplySet", schema);
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Code).HasMaxLength(SupplySet.CODE_MAX_LENGTH).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(SupplySet.DISPLAY_NAME_MAX_LENGTH).IsRequired();
            entity.Property(x => x.SupplyType).HasConversion<int>().IsRequired();

            // Code is the key seed data and every requirement match on, so nothing may share one.
            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_SupplySet_Code");
        });

        modelBuilder.Entity<SupplySetAsset>(entity =>
        {
            entity.ToTable("SupplySetAsset", schema);

            // An Asset belongs to a set at most once, which the key enforces rather than the upsert alone.
            entity.HasKey(x => new { x.SupplySetId, x.AssetId });

            entity.Property(x => x.AmountPerUnit).IsRequired();

            // No store default: EF's sentinel for an int is 0, so a column default would make an author's explicit "never draw this" indistinguishable
            // from an unset value on insert and write 1 instead. The entity's own initialiser carries the default.
            entity.Property(x => x.SelectionWeight).IsRequired();

            entity.HasOne(x => x.SupplySet)
                .WithMany(x => x.Assets)
                .HasForeignKey(x => x.SupplySetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_SupplySetAsset_SupplySet_SupplySetId");

            entity.HasOne(x => x.Asset)
                .WithMany()
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_SupplySetAsset_Asset_AssetId");

            entity.HasIndex(x => x.AssetId)
                .HasDatabaseName("IX_SupplySetAsset_Asset");
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

        // --- BattleHistory ---------------------------------------------------------------
        // The raw combat feed of docs/design/intelligence-and-sightings.md, the third sibling of units.MovementHistory and structure.StructureHistory. Append-only, so it carries no RowVersion.
        modelBuilder.Entity<BattleHistory>(entity =>
        {
            // Each rule is its own constraint so that a violation names the column it is about rather than pointing at one catch-all. They are enforced by the database because the fold reads the
            // nullable columns per event type without re-checking, and a malformed row would quietly produce a false sentence instead of failing.
            entity.ToTable(
                "BattleHistory",
                schema,
                table =>
                {
                    // Only a join or a departure is about one side; every other row is about the engagement as a whole.
                    table.HasCheckConstraint(
                        "CK_BattleHistory_ParticipantOnlyOnParticipantEvents",
                        $"([EventType] IN ({(int)BattleHistoryEventType.ParticipantJoined}, {(int)BattleHistoryEventType.ParticipantLeft}) AND [ParticipantPlayerId] IS NOT NULL) OR ([EventType] NOT IN ({(int)BattleHistoryEventType.ParticipantJoined}, {(int)BattleHistoryEventType.ParticipantLeft}) AND [ParticipantPlayerId] IS NULL)");

                    // Nullable rather than defaulted, because BattleExitReason.NotApplicable is a real member on zero, so the column has no spare value to mean "not set".
                    table.HasCheckConstraint(
                        "CK_BattleHistory_ExitReasonOnlyWhenParticipantLeft",
                        $"([EventType] = {(int)BattleHistoryEventType.ParticipantLeft} AND [ExitReason] IS NOT NULL) OR ([EventType] <> {(int)BattleHistoryEventType.ParticipantLeft} AND [ExitReason] IS NULL)");

                    // Losses are per side, on the row for that side leaving, never a battle-wide total: a coalition only learns the losses it watched happen.
                    table.HasCheckConstraint(
                        "CK_BattleHistory_TroopsLostOnlyWhenParticipantLeft",
                        $"([EventType] = {(int)BattleHistoryEventType.ParticipantLeft} AND [TroopsLost] IS NOT NULL) OR ([EventType] <> {(int)BattleHistoryEventType.ParticipantLeft} AND [TroopsLost] IS NULL)");

                    // One-sided rather than an "if and only if": a Battle can end with nobody left holding the ground, so a Concluded row is allowed a null.
                    table.HasCheckConstraint(
                        "CK_BattleHistory_GroundHeldOnlyWhenConcluded",
                        $"[EventType] = {(int)BattleHistoryEventType.Concluded} OR [GroundHeldByPlayerId] IS NULL");

                    table.HasCheckConstraint("CK_BattleHistory_TroopsLostNotNegative", "[TroopsLost] IS NULL OR [TroopsLost] >= 0");
                });

            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .HasConversion<int>()
                .IsRequired();

            entity.Property(e => e.ExitReason)
                .HasConversion<int>();

            // No navigations on either side, the same as the other two feeds: nothing reads the feed through an entity graph.
            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_BattleHistory_GameIndex_GameId");

            // A real foreign key, unlike MovementHistory.UnitInstanceId: concluding a Battle keeps its row, and the one hard delete - the game purge - removes this feed before the Battles it names.
            entity.HasOne<Battle>()
                .WithMany()
                .HasForeignKey(e => e.BattleId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_BattleHistory_Battle_BattleId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.ParticipantPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_BattleHistory_Player_ParticipantPlayerId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.GroundHeldByPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_BattleHistory_Player_GroundHeldByPlayerId");

            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.CampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_BattleHistory_CampaignMap_CampaignMapTileId");

            entity.HasOne<PlayerTurn>()
                .WithMany()
                .HasForeignKey(e => e.PlayerTurnId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_BattleHistory_PlayerTurn_PlayerTurnId");

            // Every row of one Turn, in order - the shape the fold reads the table in.
            entity.HasIndex(e => new { e.PlayerTurnId, e.TurnSequence })
                .HasDatabaseName("IX_BattleHistory_Turn_Sequence");

            // One engagement's events through a Turn, in order.
            entity.HasIndex(e => new { e.BattleId, e.PlayerTurnId, e.TurnSequence })
                .HasDatabaseName("IX_BattleHistory_Battle_Turn_Sequence");
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
            // A rebelling tile's percentage falls until it runs out and is then set back to NULL, so 0 and anything outside the percentage range could only come from a defect.
            // The roll it starts from and the step it falls by are deliberately not pinned here - both are values to be tuned, and varying either is a change to the per-round
            // pass rather than to the schema.
            entity.ToTable("CampaignMap", schema, table => table.HasCheckConstraint(
                "CK_CampaignMap_RebellionPercentRemaining",
                "[RebellionPercentRemaining] IS NULL OR [RebellionPercentRemaining] BETWEEN 1 AND 100"));

            // Tile ids are assigned by the application during game setup (see GameSetupService),
            // never by the database. A tile's id is GameIndex.CampaignMapOffsetId + X + Y * MapWidth,
            // and that only holds if we choose the value ourselves: an IDENTITY column numbers rows in
            // whatever order the query plan emits them, which is not the coordinate order we rely on.
            entity.Property(x => x.CampaignMapId).ValueGeneratedNever();

            // River edges default to None in the database as well as in C#, because game setup copies tiles with a raw INSERT ... SELECT. The constraints are
            // named so they never arrive as server-generated DF__ names (see TM-6_RenameStaleDefaultConstraints).
            entity.Property(x => x.SouthRiver).HasDefaultValue(RiverWidth.None, "DF_CampaignMap_SouthRiver");
            entity.Property(x => x.EastRiver).HasDefaultValue(RiverWidth.None, "DF_CampaignMap_EastRiver");

            // Defaulted in the database for the same reason the river edges are: game setup copies tiles with a raw INSERT ... SELECT that does not name this column. A tile
            // arrives claimed by nobody, and the per-round pass sets the flag when it writes the first claim row.
            entity.Property(x => x.HasClaims).HasDefaultValue(false, "DF_CampaignMap_HasClaims");

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

            // The ownership layers. Each is a nullable Player reference rather than a state enum, and the navigation-less ones are configured here because nothing reads them
            // through an entity graph - see the entity's remarks.
            entity.HasOne(x => x.OwnedBy)
                .WithMany()
                .HasForeignKey(x => x.OwnedByPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(x => x.OccupiedByPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMap_Player_OccupiedByPlayerId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(x => x.LastOwnedByPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMap_Player_LastOwnedByPlayerId");

            // The Settlement a tile is owned through, which gained a real foreign key once Settlements were modelled in CD-1. Navigation-less for the same reason as the two
            // above: the per-round pass reads these columns in bulk, never as an entity graph.
            entity.HasOne<Settlement>()
                .WithMany()
                .HasForeignKey(x => x.OwnedBySettlementId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMap_Settlements_OwnedBySettlementId");

            // The renamed ownership index. The occupier, last owner and claim lookups get no composite index of their own yet: each FK brings a conventional single-column
            // index with it, and nothing reads any of those columns until the per-round pass exists to do it.
            entity.HasIndex(x => new { x.GameId, x.OwnedByPlayerId })
                .HasDatabaseName("IX_CampaignMap_Game_OwnedByPlayer");

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

        // --- GameConfiguration ---------------------------------------------------------
        // Shares GameIndex's key, and the row is created with it rather than on first read - see the
        // entity for why a lazily created settings row is a problem rather than an optimisation.
        modelBuilder.Entity<GameConfiguration>(entity =>
        {
            entity.ToTable("GameConfiguration", schema);

            entity.HasKey(e => e.GameId);
            entity.Property(e => e.GameId).ValueGeneratedNever();

            entity.Property(e => e.TechnologyCustomisation).HasConversion<int>().IsRequired();
            entity.Property(e => e.UnspentResearchPointsBehaviour).HasConversion<int>().IsRequired();

            entity.HasOne(e => e.Game)
                .WithOne()
                .HasForeignKey<GameConfiguration>(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_GameConfiguration_GameIndex_GameId");
        });

        // --- Player --------------------------------------------------------------------
        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("Player", schema);
           
            entity.HasOne(p => p.Game)
                .WithMany(g => g.Players)
                .HasForeignKey(p => p.GameId)
                .OnDelete(DeleteBehavior.NoAction);

            // One colour per faction per game, so two factions can never be drawn the same. Filtered
            // because most slots have no colour yet, and NULLs must be free to coexist - the same
            // reasoning as UX_TagType_Colour and UX_Player_Game_User.
            entity.HasIndex(p => new { p.GameId, p.FactionColour })
                .IsUnique()
                .HasFilter("[FactionColour] IS NOT NULL")
                .HasDatabaseName("UX_Player_Game_FactionColour");

            // The identity names a player authors are each unique within their game, so anything that labels or
            // attributes by one of them picks out exactly one faction for the whole game. Language is deliberately
            // absent: two peoples sharing a language is a legitimate thing to express, and nothing attributes by it.
            //
            // The collation is pinned on each column rather than inherited from the server, because case-insensitivity
            // is what makes these indexes mean what they say - on a CS_AS server "The Vashti" and "the vashti" would be
            // two different names. Letting the collation settle case is also why there are no normalised sibling
            // columns: what is stored keeps the casing the player typed, which is what gets displayed.
            entity.Property(p => p.RulerName).UseCollation(Player.IDENTITY_COLLATION);
            entity.Property(p => p.FactionName).UseCollation(Player.IDENTITY_COLLATION);
            entity.Property(p => p.PeopleName).UseCollation(Player.IDENTITY_COLLATION);
            entity.Property(p => p.Demonym).UseCollation(Player.IDENTITY_COLLATION);

            // Unfiltered, unlike the colour above: these columns are non-nullable and the validator refuses a blank, so
            // there is no "undecided" value for a filter to spare.
            entity.HasIndex(p => new { p.GameId, p.RulerName })
                .IsUnique()
                .HasDatabaseName("UX_Player_Game_RulerName");

            entity.HasIndex(p => new { p.GameId, p.FactionName })
                .IsUnique()
                .HasDatabaseName("UX_Player_Game_FactionName");

            entity.HasIndex(p => new { p.GameId, p.PeopleName })
                .IsUnique()
                .HasDatabaseName("UX_Player_Game_PeopleName");

            entity.HasIndex(p => new { p.GameId, p.Demonym })
                .IsUnique()
                .HasDatabaseName("UX_Player_Game_Demonym");

            // The portrait is unique within a game too, but filtered: the default stands for "not chosen yet", which
            // every undecided player holds at once. Same shape as the colour index above, with an empty string in the
            // place of its NULL.
            entity.HasIndex(p => new { p.GameId, p.RulerPortraitPath })
                .IsUnique()
                .HasFilter("[RulerPortraitPath] <> ''")
                .HasDatabaseName("UX_Player_Game_RulerPortraitPath");
        });

        // --- PlayerCampaignMap ---------------------------------------------------------
        modelBuilder.Entity<PlayerCampaignMap>(entity =>
        {
            entity.ToTable("PlayerCampaignMap", schema);

            // Assigned by the application for the same reason as CampaignMap.CampaignMapId above.
            // Each player gets one contiguous block starting at Player.CampaignOffsetId, laid out in
            // the same row-major order, so a player tile and its world tile share a coordinate offset.
            entity.Property(x => x.PlayerCampaignMapId).ValueGeneratedNever();

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

        // --- PlayerBattleMap ----------------------------------------------------------
        // The battle map sibling of PlayerCampaignMap: one Player's fog-of-war view of one battle map tile. Written only for battle participants, when a Battle commences.
        modelBuilder.Entity<PlayerBattleMap>(entity =>
        {
            entity.ToTable("PlayerBattleMap", schema);

            // The battle tile id already addresses the tile, so the pair is a natural key and no id range has to be reserved the way PlayerCampaignMap's are.
            entity.HasKey(e => new { e.PlayerId, e.BattleMapId });

            // No navigations, and every relationship NoAction: nothing reads these rows through an entity graph, and GameDeleteExecutor removes them in its own hand-ordered step.
            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerBattleMap_GameIndex_GameId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerBattleMap_Player_PlayerId");

            entity.HasOne<BattleMapIndex>()
                .WithMany()
                .HasForeignKey(e => e.BattleMapIndexId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerBattleMap_BattleMapIndex_BattleMapIndexId");

            entity.HasOne<BattleMap>()
                .WithMany()
                .HasForeignKey(e => e.BattleMapId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerBattleMap_BattleMap_BattleMapId");

            entity.HasOne<CampaignBiome>()
                .WithMany()
                .HasForeignKey(e => e.BiomeTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerBattleMap_CampaignBiome_BiomeTypeId");

            entity.HasOne<PlayerTurn>()
                .WithMany()
                .HasForeignKey(e => e.FirstSeenOnTurnId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerBattleMap_PlayerTurn_FirstSeenOnTurnId");

            entity.HasOne<PlayerTurn>()
                .WithMany()
                .HasForeignKey(e => e.LastSeenOnTurnId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerBattleMap_PlayerTurn_LastSeenOnTurnId");

            // One Player's whole map at once - the shape of both the vision grant and any future battle map read.
            entity.HasIndex(e => new { e.PlayerId, e.BattleMapIndexId });
        });

        // --- PlayerSustenanceModifier --------------------------------------------------
        // One modifier to a Player's Forage, Hunt, Fish or Fodder value. The effective value is derived from these and the biome's base, never stored - see the entity's remarks.
        modelBuilder.Entity<PlayerSustenanceModifier>(entity =>
        {
            // A modifier with neither part would change nothing, and would still take up the source's one row for that asset and biome.
            entity.ToTable("PlayerSustenanceModifier", schema, table => table.HasCheckConstraint(
                "CK_PlayerSustenanceModifier_HasValue",
                "[FlatValue] <> 0 OR [PercentValue] <> 0"));

            entity.HasKey(e => e.Id);

            // No navigations, and every relationship NoAction: nothing reads these rows through an entity graph, and GameDeleteExecutor removes them in its own hand-ordered step.
            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerSustenanceModifier_GameIndex_GameId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerSustenanceModifier_Player_PlayerId");

            // Optional: a null biome is a modifier to every biome.
            entity.HasOne<CampaignBiome>()
                .WithMany()
                .HasForeignKey(e => e.BiomeTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerSustenanceModifier_CampaignBiome_BiomeTypeId");

            // A source grants a Player at most one modifier per resource per biome, so a source that gives both a flat and a percentage amount does it in one row.
            //
            // HasFilter(null) matters. EF's SQL Server convention filters a unique index on a nullable column to "IS NOT NULL", which would let a source hold any number of
            // all-biome rows for the same resource. Unfiltered, SQL Server treats NULLs as equal in a unique index, which is exactly the rule wanted here.
            //
            // The Player comes first because it is also the reader's lookup: every one of a Player's modifiers at once.
            entity.HasIndex(e => new { e.PlayerId, e.SourceType, e.SourceId, e.Resource, e.BiomeTypeId })
                .IsUnique()
                .HasFilter(null)
                .HasDatabaseName("UX_PlayerSustenanceModifier_Player_Source_Resource_Biome");

            // The game delete's filter.
            entity.HasIndex(e => e.GameId)
                .HasDatabaseName("IX_PlayerSustenanceModifier_GameId");
        });

        // --- CampaignMapDepletion ------------------------------------------------------
        // What has been taken from a campaign map tile, by anyone, and the last Round it was fed on. Player-neutral: a Player's available amount is derived from this and their
        // effective value, never stored - see the entity's remarks.
        modelBuilder.Entity<CampaignMapDepletion>(entity =>
        {
            // Extraction can never take more than is available, so a negative amount taken could only come from a defect.
            entity.ToTable("CampaignMapDepletion", schema, table => table.HasCheckConstraint(
                "CK_CampaignMapDepletion_TakenNonNegative",
                "[ForageTaken] >= 0 AND [HuntTaken] >= 0 AND [FishTaken] >= 0 AND [FodderTaken] >= 0"));

            // The tile id is the key, and comes from the tile rather than being generated: a tile has at most one depletion row.
            entity.HasKey(e => e.CampaignMapId);

            entity.Property(e => e.CampaignMapId)
                .ValueGeneratedNever();

            entity.Property(e => e.ForageTaken).HasDefaultValue(0L);
            entity.Property(e => e.HuntTaken).HasDefaultValue(0L);
            entity.Property(e => e.FishTaken).HasDefaultValue(0L);
            entity.Property(e => e.FodderTaken).HasDefaultValue(0L);

            // No navigations, and every relationship NoAction: nothing reads these rows through an entity graph, and GameDeleteExecutor removes them in its own hand-ordered step.
            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.CampaignMapId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMapDepletion_CampaignMap_CampaignMapId");

            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMapDepletion_GameIndex_GameId");

            // The game delete's filter.
            entity.HasIndex(e => e.GameId)
                .HasDatabaseName("IX_CampaignMapDepletion_GameId");
        });

        // --- CampaignMapClaim ----------------------------------------------------------
        // One Player's political claim on one tile. A table rather than a column because claims are non-exclusive: several Players may claim the same tile, so there is no
        // winner to put on the tile - see the entity's remarks.
        modelBuilder.Entity<CampaignMapClaim>(entity =>
        {
            entity.ToTable("CampaignMapClaim", schema);

            // The tile and the Player are the key, which is also the uniqueness rule: one claim per Player per tile. No identity column, the same as CampaignMapDepletion -
            // nothing points at a claim row, so a surrogate key would only add a counter for the game delete to reseed.
            entity.HasKey(e => new { e.CampaignMapId, e.PlayerId });

            // No navigations, every relationship NoAction: the per-round pass reads and writes these in bulk, and GameDeleteExecutor removes them in its own hand-ordered step
            // before both the tiles and the Players.
            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.CampaignMapId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMapClaim_CampaignMap_CampaignMapId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMapClaim_Player_PlayerId");

            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMapClaim_GameIndex_GameId");

            // The game delete's filter, and the per-round pass's: claim lapse and the supply range walk one Player's claims at a time, and the key's leaf carries the tile id
            // along, so the walk never touches the table itself.
            entity.HasIndex(e => new { e.GameId, e.PlayerId })
                .HasDatabaseName("IX_CampaignMapClaim_Game_Player");
        });

        ConfigureMapResourceGameState(modelBuilder, schema);

        // --- PlayerKnownStructure ------------------------------------------------------
        // The sibling of PlayerKnownForce: a Player's last-known owner, progress and state for a Structure. Upserted by the sightings step, never deleted by play.
        //
        // Recreated in F-25 rather than altered. The table it replaces, PlayerKnownStructures, came from InitialCreate and had never been written, and adding its new non-null columns to it
        // would have left unnamed DF__ default constraints behind.
        modelBuilder.Entity<PlayerKnownStructure>(entity =>
        {
            entity.ToTable("PlayerKnownStructure", schema);

            entity.HasKey(e => e.Id);

            // No navigations, the same as PlayerKnownForce: nothing reads the memory through an entity graph.
            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownStructure_GameIndex_GameId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownStructure_Player_PlayerId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.OwnerPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownStructure_Player_OwnerPlayerId");

            // A real foreign key, unlike PlayerKnownForce.UnitInstanceId: razing is a soft delete, so the Structure's row is never removed out from under this one.
            entity.HasOne<StructureInstance>()
                .WithMany()
                .HasForeignKey(e => e.StructureInstanceId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownStructure_StructureInstances_StructureInstanceId");

            entity.HasOne<Structure>()
                .WithMany()
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownStructure_Structures_StructureId");

            // NoAction with a name, where the table it replaces had an attribute-only Cascade: the game purge deletes these explicitly, before the tiles.
            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.CampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownStructure_CampaignMap_CampaignMapTileId");

            entity.HasOne<PlayerTurn>()
                .WithMany()
                .HasForeignKey(e => e.LastSeenPlayerTurnId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownStructure_PlayerTurn_LastSeenPlayerTurnId");

            // One memory per Player per Structure, which is what makes the sightings step an upsert.
            entity.HasIndex(e => new { e.GameId, e.PlayerId, e.StructureInstanceId })
                .IsUnique()
                .HasDatabaseName("UX_PlayerKnownStructure_Game_Player_Structure");
        });

        // --- PlayerReport --------------------------------------------------------------
        // The intelligence feed of docs/design/intelligence-and-sightings.md. Written only by the end-of-turn sightings step, which replaces a Turn's reports wholesale when it re-runs.
        modelBuilder.Entity<PlayerReport>(entity =>
        {
            // A report is about a Force, a Structure or an engagement, never more than one. Enforced by the database because the read projection discloses whichever subject it finds, and a row
            // carrying two would have it describe one thing with the other's identity attached. Counted rather than paired off, because three families no longer fit an "either or" expression.
            //
            // At most one rather than exactly one: the constraint has always permitted a subjectless row, every producer sets exactly one anyway, and tightening it is a behaviour change of its
            // own rather than a consequence of adding a third family.
            entity.ToTable("PlayerReport", schema, table => table.HasCheckConstraint(
                "CK_PlayerReport_OneSubject",
                "(CASE WHEN [SubjectUnitInstanceId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [SubjectStructureInstanceId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [SubjectBattleId] IS NULL THEN 0 ELSE 1 END) <= 1"));

            entity.HasKey(e => e.Id);

            // No navigations to the referenced rows, the same as MovementHistory: nothing reads the feed through an entity graph.
            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_GameIndex_GameId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.RecipientPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_Player_RecipientPlayerId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.SubjectOwnerPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_Player_SubjectOwnerPlayerId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.FirstWitnessOwnerPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_Player_FirstWitnessOwnerPlayerId");

            entity.HasOne<PlayerTurn>()
                .WithMany()
                .HasForeignKey(e => e.RaisedDuringPlayerTurnId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_PlayerTurn_RaisedDuringPlayerTurnId");

            entity.HasOne<UnitTemplate>()
                .WithMany()
                .HasForeignKey(e => e.SubjectUnitTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_UnitTemplate_SubjectUnitTemplateId");

            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.FirstCampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_CampaignMap_FirstCampaignMapTileId");

            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.LastCampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_CampaignMap_LastCampaignMapTileId");

            // Deliberately no relationship for SubjectUnitInstanceId. A destroyed Force is hard-deleted, and a foreign key would either block that delete or clear the column that says which Force
            // the report is about. See the property's remarks.

            // A Structure subject does get one: razing is a soft delete, so the Structure's row outlives every report about it.
            entity.HasOne<StructureInstance>()
                .WithMany()
                .HasForeignKey(e => e.SubjectStructureInstanceId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_StructureInstances_SubjectStructureInstanceId");

            entity.HasOne<Structure>()
                .WithMany()
                .HasForeignKey(e => e.SubjectStructureId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_Structures_SubjectStructureId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.SubjectPreviousOwnerPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_Player_SubjectPreviousOwnerPlayerId");

            // A combat subject gets one for the same reason a Structure subject does: a Battle is never hard-deleted, so it outlives every report about it.
            entity.HasOne<Battle>()
                .WithMany()
                .HasForeignKey(e => e.SubjectBattleId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_Battle_SubjectBattleId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.SubjectGroundHeldByPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReport_Player_SubjectGroundHeldByPlayerId");

            // A backstop for the sightings step's idempotency: re-running a Turn replaces its reports rather than adding to them, and this makes a slip there fail loudly instead of doubling the
            // feed. Filtered because a report with no Force subject has no track to be unique within.
            entity.HasIndex(e => new { e.RaisedDuringPlayerTurnId, e.RecipientPlayerId, e.SubjectUnitInstanceId, e.TrackOrdinal })
                .IsUnique()
                .HasFilter("[SubjectUnitInstanceId] IS NOT NULL")
                .HasDatabaseName("UX_PlayerReport_Turn_Recipient_Subject_Track");

            // The same backstop for Structure reports, which the index above filters out. A Structure folds into one report per recipient per Turn, so there is no ordinal in the key.
            entity.HasIndex(e => new { e.RaisedDuringPlayerTurnId, e.RecipientPlayerId, e.SubjectStructureInstanceId })
                .IsUnique()
                .HasFilter("[SubjectStructureInstanceId] IS NOT NULL")
                .HasDatabaseName("UX_PlayerReport_Turn_Recipient_Structure");

            // And for combat reports, which both indexes above filter out. An engagement folds into one report per recipient per Turn, so there is no ordinal here either.
            entity.HasIndex(e => new { e.RaisedDuringPlayerTurnId, e.RecipientPlayerId, e.SubjectBattleId })
                .IsUnique()
                .HasFilter("[SubjectBattleId] IS NOT NULL")
                .HasDatabaseName("UX_PlayerReport_Turn_Recipient_Battle");

            // "My reports since my last Turn ended" - the shape the feed is read in.
            entity.HasIndex(e => new { e.GameId, e.RecipientPlayerId, e.RaisedDuringPlayerTurnId })
                .HasDatabaseName("IX_PlayerReport_Recipient_Turn");
        });

        // --- PlayerReportPathTile ------------------------------------------------------
        modelBuilder.Entity<PlayerReportPathTile>(entity =>
        {
            entity.ToTable("PlayerReportPathTile", schema);

            // Keyed on the report and the tile's position along its path, so a path cannot hold two tiles at one position.
            entity.HasKey(e => new { e.PlayerReportId, e.Ordinal });

            // NoAction rather than Cascade, like every other game-scoped relationship: the sightings step and the game purge both delete these before their reports, explicitly.
            entity.HasOne(e => e.PlayerReport)
                .WithMany(report => report.PathTiles)
                .HasForeignKey(e => e.PlayerReportId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReportPathTile_PlayerReport_PlayerReportId");

            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.CampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerReportPathTile_CampaignMap_CampaignMapTileId");
        });

        // --- PlayerKnownForce ----------------------------------------------------------
        // The sibling of PlayerKnownStructure: a Player's last-known position and state for a Force. Upserted by the sightings step, never deleted by play.
        modelBuilder.Entity<PlayerKnownForce>(entity =>
        {
            entity.ToTable("PlayerKnownForce", schema);

            entity.HasKey(e => e.Id);

            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownForce_GameIndex_GameId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownForce_Player_PlayerId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.OwnerPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownForce_Player_OwnerPlayerId");

            entity.HasOne<UnitTemplate>()
                .WithMany()
                .HasForeignKey(e => e.UnitTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownForce_UnitTemplate_UnitTemplateId");

            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.LastSeenCampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownForce_CampaignMap_LastSeenCampaignMapTileId");

            entity.HasOne<PlayerTurn>()
                .WithMany()
                .HasForeignKey(e => e.LastSeenPlayerTurnId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownForce_PlayerTurn_LastSeenPlayerTurnId");

            // Deliberately no relationship for UnitInstanceId: the row has to outlive the Force it describes. See the property's remarks.

            // One memory per Player per Force, which is what makes the sightings step an upsert.
            entity.HasIndex(e => new { e.GameId, e.PlayerId, e.UnitInstanceId })
                .IsUnique()
                .HasDatabaseName("UX_PlayerKnownForce_Game_Player_Unit");
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

    /// <summary>
    /// A game's MapResources and what each Player knows of them. See <c>docs/design/campaign-map/map-resources.md</c>.
    /// </summary>
    /// <remarks>
    /// The same shape as the other newer <c>game</c> tables: no navigations, every relationship NoAction with a named constraint, and a <c>GameId</c> on every row for
    /// <c>GameDeleteExecutor</c>, which removes them in its own hand-ordered steps.
    /// </remarks>
    private static void ConfigureMapResourceGameState(ModelBuilder modelBuilder, string schema)
    {
        // --- MapResourceInstance -------------------------------------------------------
        // A MapResource on a tile. Player-neutral, like CampaignMapDepletion.
        modelBuilder.Entity<MapResourceInstance>(entity =>
        {
            entity.ToTable("MapResourceInstance", schema, table =>
            {
                // Two slots, and one row per slot (below), is what caps a tile at two MapResources - dormant ones included.
                table.HasCheckConstraint(
                    "CK_MapResourceInstance_Slot",
                    $"[Slot] BETWEEN {MapResourceInstance.MIN_SLOT} AND {MapResourceInstance.MAX_SLOT}");

                // Extraction can never take more than is there, and a spread can only use up what is left, so either going negative could only come from a defect.
                table.HasCheckConstraint(
                    "CK_MapResourceInstance_QuantityNonNegative",
                    "[Quantity] IS NULL OR [Quantity] >= 0");

                table.HasCheckConstraint(
                    "CK_MapResourceInstance_SpreadLimitNonNegative",
                    "[SpreadLimit] IS NULL OR [SpreadLimit] >= 0");
            });

            entity.HasKey(e => e.Id);

            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceInstance_GameIndex_GameId");

            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.CampaignMapId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceInstance_CampaignMap_CampaignMapId");

            entity.HasOne<MapResourceType>()
                .WithMany()
                .HasForeignKey(e => e.MapResourceTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceInstance_MapResourceType_MapResourceTypeId");

            entity.HasOne<MapResourceDistributionScenario>()
                .WithMany()
                .HasForeignKey(e => e.MapResourceDistributionScenarioId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceInstance_MapResourceDistributionScenario_ScenarioId");

            // At most one MapResource per slot, so at most two per tile. The tile comes first because it is also the lookup: every MapResource on a tile.
            entity.HasIndex(e => new { e.CampaignMapId, e.Slot })
                .IsUnique()
                .HasDatabaseName("UX_MapResourceInstance_Tile_Slot");

            // At most one of each type per tile: spreading into a tile that has the same type tops it up or revives it rather than adding a second.
            entity.HasIndex(e => new { e.CampaignMapId, e.MapResourceTypeId })
                .IsUnique()
                .HasDatabaseName("UX_MapResourceInstance_Tile_Type");

            // The end-of-Round process's load, and the game delete's filter.
            entity.HasIndex(e => e.GameId)
                .HasDatabaseName("IX_MapResourceInstance_GameId");
        });

        // --- MapResourceInstanceAmount -------------------------------------------------
        // A Distribution instance's own amount of one Asset.
        modelBuilder.Entity<MapResourceInstanceAmount>(entity =>
        {
            entity.ToTable("MapResourceInstanceAmount", schema, table => table.HasCheckConstraint(
                "CK_MapResourceInstanceAmount_AmountNonNegative",
                "[Amount] >= 0"));

            entity.HasKey(e => new { e.MapResourceInstanceId, e.AssetId });

            entity.HasOne<MapResourceInstance>()
                .WithMany()
                .HasForeignKey(e => e.MapResourceInstanceId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceInstanceAmount_MapResourceInstance_MapResourceInstanceId");

            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceInstanceAmount_GameIndex_GameId");

            entity.HasOne<Asset>()
                .WithMany()
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceInstanceAmount_Asset_AssetId");

            // The game delete's filter.
            entity.HasIndex(e => e.GameId)
                .HasDatabaseName("IX_MapResourceInstanceAmount_GameId");
        });

        // --- MapResourceInstanceLayer --------------------------------------------------
        // One layer of a stratified instance.
        modelBuilder.Entity<MapResourceInstanceLayer>(entity =>
        {
            entity.ToTable("MapResourceInstanceLayer", schema, table =>
            {
                table.HasCheckConstraint(
                    "CK_MapResourceInstanceLayer_LayerNumberPositive",
                    "[LayerNumber] >= 1");

                // An empty layer holds nothing; a layer with an Asset can be worked down to 0 but never below.
                table.HasCheckConstraint(
                    "CK_MapResourceInstanceLayer_Quantity",
                    "[Quantity] >= 0 AND ([AssetId] IS NOT NULL OR [Quantity] = 0)");
            });

            entity.HasKey(e => new { e.MapResourceInstanceId, e.LayerNumber });

            entity.HasOne<MapResourceInstance>()
                .WithMany()
                .HasForeignKey(e => e.MapResourceInstanceId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceInstanceLayer_MapResourceInstance_MapResourceInstanceId");

            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceInstanceLayer_GameIndex_GameId");

            // Optional: an empty layer has no Asset.
            entity.HasOne<Asset>()
                .WithMany()
                .HasForeignKey(e => e.AssetId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceInstanceLayer_Asset_AssetId");

            // The game delete's filter.
            entity.HasIndex(e => e.GameId)
                .HasDatabaseName("IX_MapResourceInstanceLayer_GameId");
        });

        // --- PlayerMapResourceTypeKnowledge --------------------------------------------
        // When a Player first saw, and researched, a MapResourceType. Stands in for research state until Technology is modelled.
        modelBuilder.Entity<PlayerMapResourceTypeKnowledge>(entity =>
        {
            entity.ToTable("PlayerMapResourceTypeKnowledge", schema);

            // Composite, like PlayerUnlockedComponent: a Player knows a type once or not at all.
            entity.HasKey(e => new { e.PlayerId, e.MapResourceTypeId });

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceTypeKnowledge_Player_PlayerId");

            entity.HasOne<MapResourceType>()
                .WithMany()
                .HasForeignKey(e => e.MapResourceTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceTypeKnowledge_MapResourceType_MapResourceTypeId");

            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceTypeKnowledge_GameIndex_GameId");

            // The game delete's filter.
            entity.HasIndex(e => e.GameId)
                .HasDatabaseName("IX_PlayerMapResourceTypeKnowledge_GameId");
        });

        // --- PlayerKnownMapResource ----------------------------------------------------
        // A MapResource instance a Player has discovered. Whether they can see it now is derived live from vision, never stored.
        modelBuilder.Entity<PlayerKnownMapResource>(entity =>
        {
            entity.ToTable("PlayerKnownMapResource", schema, table => table.HasCheckConstraint(
                "CK_PlayerKnownMapResource_DeepestLayerSurveyedPositive",
                "[DeepestLayerSurveyed] IS NULL OR [DeepestLayerSurveyed] >= 1"));

            entity.HasKey(e => e.Id);

            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownMapResource_GameIndex_GameId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownMapResource_Player_PlayerId");

            entity.HasOne<MapResourceInstance>()
                .WithMany()
                .HasForeignKey(e => e.MapResourceInstanceId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerKnownMapResource_MapResourceInstance_MapResourceInstanceId");

            // A Player discovers an instance once. The Player comes first because it is also the view's lookup: every instance a Player knows.
            entity.HasIndex(e => new { e.PlayerId, e.MapResourceInstanceId })
                .IsUnique()
                .HasDatabaseName("UX_PlayerKnownMapResource_Player_Instance");

            // For copying discoveries when an instance spreads, and removing them when it is deleted for good.
            entity.HasIndex(e => e.MapResourceInstanceId)
                .HasDatabaseName("IX_PlayerKnownMapResource_Instance");

            // The game delete's filter.
            entity.HasIndex(e => e.GameId)
                .HasDatabaseName("IX_PlayerKnownMapResource_GameId");
        });

        // --- PlayerMapResourceModifier -------------------------------------------------
        // One modifier to a Player's value for a MapResourceType. The same shape as PlayerSustenanceModifier, plus a Geography.
        modelBuilder.Entity<PlayerMapResourceModifier>(entity =>
        {
            // A modifier with neither part would change nothing, and would still take up the source's one row for that type, target, Biome and Geography.
            entity.ToTable("PlayerMapResourceModifier", schema, table => table.HasCheckConstraint(
                "CK_PlayerMapResourceModifier_HasValue",
                "[FlatValue] <> 0 OR [PercentValue] <> 0"));

            entity.HasKey(e => e.Id);

            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceModifier_GameIndex_GameId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceModifier_Player_PlayerId");

            entity.HasOne<MapResourceType>()
                .WithMany()
                .HasForeignKey(e => e.MapResourceTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceModifier_MapResourceType_MapResourceTypeId");

            // Optional: a null Biome is a modifier to every Biome.
            entity.HasOne<CampaignBiome>()
                .WithMany()
                .HasForeignKey(e => e.BiomeTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceModifier_CampaignBiome_BiomeTypeId");

            // Optional: a null Geography is a modifier to every Geography.
            entity.HasOne<CampaignGeography>()
                .WithMany()
                .HasForeignKey(e => e.CampaignMapGeographyTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceModifier_CampaignGeography_CampaignMapGeographyTypeId");

            // A source grants a Player at most one modifier per type, target, Biome and Geography. HasFilter(null) so the null (every Biome / every Geography) rows collide
            // too - see UX_PlayerSustenanceModifier_Player_Source_Resource_Biome.
            entity.HasIndex(e => new { e.PlayerId, e.SourceType, e.SourceId, e.MapResourceTypeId, e.Target, e.BiomeTypeId, e.CampaignMapGeographyTypeId })
                .IsUnique()
                .HasFilter(null)
                .HasDatabaseName("UX_PlayerMapResourceModifier_Player_Source_Type_Target_Biome_Geography");

            // The game delete's filter.
            entity.HasIndex(e => e.GameId)
                .HasDatabaseName("IX_PlayerMapResourceModifier_GameId");
        });

        // --- PlayerMapResourceSwitch ---------------------------------------------------
        // One source switching discovery, a yielded Asset or a layer survey on or off for a Player. An off always wins over an on; see the entity's remarks.
        modelBuilder.Entity<PlayerMapResourceSwitch>(entity =>
        {
            // Each kind names exactly what it switches: a yield switch names its Asset, a survey switch its layer, and a discovery switch neither.
            entity.ToTable("PlayerMapResourceSwitch", schema, table => table.HasCheckConstraint(
                "CK_PlayerMapResourceSwitch_KindTarget",
                $"([Kind] = {(int)MapResourceSwitchKind.Discovery} AND [AssetId] IS NULL AND [LayerNumber] IS NULL)"
                + $" OR ([Kind] = {(int)MapResourceSwitchKind.YieldAsset} AND [AssetId] IS NOT NULL AND [LayerNumber] IS NULL)"
                + $" OR ([Kind] = {(int)MapResourceSwitchKind.SurveyLayer} AND [AssetId] IS NULL AND [LayerNumber] >= 1)"));

            entity.HasKey(e => e.Id);

            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceSwitch_GameIndex_GameId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceSwitch_Player_PlayerId");

            entity.HasOne<MapResourceType>()
                .WithMany()
                .HasForeignKey(e => e.MapResourceTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceSwitch_MapResourceType_MapResourceTypeId");

            entity.HasOne<Asset>()
                .WithMany()
                .HasForeignKey(e => e.AssetId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerMapResourceSwitch_Asset_AssetId");

            // A source sets one switch per thing switched. HasFilter(null) so the null Asset / Layer columns collide, as for the modifiers.
            entity.HasIndex(e => new { e.PlayerId, e.SourceType, e.SourceId, e.Kind, e.MapResourceTypeId, e.AssetId, e.LayerNumber })
                .IsUnique()
                .HasFilter(null)
                .HasDatabaseName("UX_PlayerMapResourceSwitch_Player_Source_Kind_Type_Asset_Layer");

            // The game delete's filter.
            entity.HasIndex(e => e.GameId)
                .HasDatabaseName("IX_PlayerMapResourceSwitch_GameId");
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
            // The sentinel sits outside the enum because 0 is a real member here, PreActivation. Left implicit, EF reads "the author set PreActivation" and
            // "the author set nothing" as the same thing and writes the store default over both, so an account awaiting activation was inserted as Active.
            // No value ever equals -1, so the CLR value is always what lands; the column default now only covers inserts that bypass EF.
            entity.Property(x => x.InactiveReasonType)
                .HasDefaultValue(Domain.Enums.Authentication.InactiveReasonType.Active)
                .HasSentinel((Domain.Enums.Authentication.InactiveReasonType)(-1));

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

            // Nullable: a session that has never been refreshed has no retired hash yet.
            entity.Property(x => x.PreviousTokenHash).HasMaxLength(RefreshSession.TOKEN_HASH_MAX_LENGTH);

            // Stored as its underlying int, so the column survives members being renamed.
            entity.Property(x => x.RevokedReason).HasConversion<int>();

            // Guards the rotation against two concurrent refreshes both writing a new hash, with the loser silently overwritten.
            entity.Property(x => x.RowVersion)
                .IsRowVersion();

            // Lookups happen by token hash, so it must be unique and indexed.
            entity.HasIndex(x => x.TokenHash)
                .IsUnique()
                .HasDatabaseName("UX_RefreshSession_TokenHash");

            // A rotation presents either the current or the retired hash, so reuse detection looks both columns up and the retired one needs an index
            // of its own. Unique for the same reason as the live hash — a token must identify at most one session, whichever column it matches — and
            // filtered because the nulls are the sessions that have never rotated, which are not in conflict with each other.
            entity.HasIndex(x => x.PreviousTokenHash)
                .IsUnique()
                .HasFilter("[PreviousTokenHash] IS NOT NULL")
                .HasDatabaseName("UX_RefreshSession_PreviousTokenHash");

            // Read by the family revoke that detected reuse triggers.
            entity.HasIndex(x => x.FamilyId).HasDatabaseName("IX_RefreshSession_Family");

            // Revocation queries hit these columns: by game (delete game / kick all) and by user.
            entity.HasIndex(x => x.GameId).HasDatabaseName("IX_RefreshSession_Game");
            entity.HasIndex(x => x.UserId).HasDatabaseName("IX_RefreshSession_User");

            // The pruning sweep deletes by expiry, so it gets the index rather than scanning a table that grows with every login.
            entity.HasIndex(x => x.ExpiresAt).HasDatabaseName("IX_RefreshSession_ExpiresAt");

            // Deleting a user takes their sessions with them (unlike Players, sessions are
            // disposable and carry no game state worth preserving).
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // --- UserPermission --------------------------------------------------------------
        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.ToTable("UserPermission", schema);

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Permission).HasConversion<int>().IsRequired();

            // A permission is held or it isn't, so the same pair must never appear twice. This is
            // also the natural key the save handler upserts against.
            entity.HasIndex(x => new { x.UserId, x.Permission })
                .IsUnique()
                .HasDatabaseName("UX_UserPermission_User_Permission");

            // Grants are meaningless without the account, so they go when the user does — the same
            // reasoning as RefreshSessions above.
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

        ConfigureMapResourceCatalogue(modelBuilder, schema);
    }

    /// <summary>
    /// The MapResource catalogue: categories, types, and each type's yields, habitats, Distribution scenarios and layers. Static data, edited in the map editor and read live
    /// by games, never copied into them. See <c>docs/design/campaign-map/map-resources.md</c>.
    /// </summary>
    /// <remarks>
    /// A type owns its yields, habitats, scenarios and layers, so they are cascade-deleted with it and are written through its navigations. Everything outside that aggregate -
    /// Assets, Biomes, Geographies, and every game row naming a type - is NoAction, so a type that any game still uses cannot be deleted.
    /// </remarks>
    private static void ConfigureMapResourceCatalogue(ModelBuilder modelBuilder, string schema)
    {
        // --- MapResourceCategory -------------------------------------------------------
        modelBuilder.Entity<MapResourceCategory>(entity =>
        {
            entity.ToTable("MapResourceCategory", schema);

            entity.HasKey(e => e.Id);

            // The seed data's match key.
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UX_MapResourceCategory_Code");
        });

        // --- MapResourceType -----------------------------------------------------------
        modelBuilder.Entity<MapResourceType>(entity =>
        {
            // The two flag rules the kind depends on. MapResourceTypeValidator checks these and everything that follows from them (which values and children each kind
            // needs); the database holds the flags themselves, so no code path can store a type that is both renewable and stratified.
            entity.ToTable("MapResourceType", schema, table =>
            {
                table.HasCheckConstraint(
                    "CK_MapResourceType_RenewableOrStratified",
                    "[IsRenewable] = 0 OR [IsStratified] = 0");

                table.HasCheckConstraint(
                    "CK_MapResourceType_DistributionIsRenewable",
                    "[IsDistribution] = 0 OR [IsRenewable] = 1");
            });

            entity.HasKey(e => e.Id);

            // The seed data's match key.
            entity.HasIndex(e => e.Code)
                .IsUnique()
                .HasDatabaseName("UX_MapResourceType_Code");

            // The map editor's grouped list. Restrict: a category with types in it cannot be deleted out from under them.
            entity.HasOne(e => e.Category)
                .WithMany(c => c.MapResourceTypes)
                .HasForeignKey(e => e.MapResourceCategoryId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_MapResourceType_MapResourceCategory_MapResourceCategoryId");

            entity.HasIndex(e => new { e.MapResourceCategoryId, e.SortOrder })
                .HasDatabaseName("IX_MapResourceType_Category_SortOrder");
        });

        // --- MapResourceTypeYield ------------------------------------------------------
        modelBuilder.Entity<MapResourceTypeYield>(entity =>
        {
            // One unit extracted always gives something of each Asset listed, or the Asset would not be listed.
            entity.ToTable("MapResourceTypeYield", schema, table => table.HasCheckConstraint(
                "CK_MapResourceTypeYield_AmountPositive",
                "[AmountPerUnit] > 0"));

            entity.HasKey(e => new { e.MapResourceTypeId, e.AssetId });

            entity.HasOne(e => e.MapResourceType)
                .WithMany(t => t.Yields)
                .HasForeignKey(e => e.MapResourceTypeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MapResourceTypeYield_MapResourceType_MapResourceTypeId");

            entity.HasOne<Asset>()
                .WithMany()
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceTypeYield_Asset_AssetId");
        });

        // --- MapResourceHabitat --------------------------------------------------------
        modelBuilder.Entity<MapResourceHabitat>(entity =>
        {
            entity.ToTable("MapResourceHabitat", schema, table =>
            {
                table.HasCheckConstraint(
                    "CK_MapResourceHabitat_InitialQuantityRange",
                    "[InitialQuantityMin] IS NULL OR ([InitialQuantityMin] >= 0 AND [InitialQuantityMax] >= [InitialQuantityMin])");

                table.HasCheckConstraint(
                    "CK_MapResourceHabitat_SpreadLimitRange",
                    "[SpreadLimitMin] IS NULL OR ([SpreadLimitMin] >= 0 AND [SpreadLimitMax] >= [SpreadLimitMin])");
            });

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.MapResourceType)
                .WithMany(t => t.Habitats)
                .HasForeignKey(e => e.MapResourceTypeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MapResourceHabitat_MapResourceType_MapResourceTypeId");

            entity.HasOne<CampaignBiome>()
                .WithMany()
                .HasForeignKey(e => e.BiomeTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceHabitat_CampaignBiome_BiomeTypeId");

            // Optional: a null Geography is every Geography in the Biome.
            entity.HasOne<CampaignGeography>()
                .WithMany()
                .HasForeignKey(e => e.CampaignMapGeographyTypeId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceHabitat_CampaignGeography_CampaignMapGeographyTypeId");

            // One habitat per type, Biome and Geography, including one Biome-wide (null Geography) habitat - so HasFilter(null), for the same reason as
            // UX_PlayerSustenanceModifier_Player_Source_Resource_Biome: SQL Server treats NULLs as equal in an unfiltered unique index.
            entity.HasIndex(e => new { e.MapResourceTypeId, e.BiomeTypeId, e.CampaignMapGeographyTypeId })
                .IsUnique()
                .HasFilter(null)
                .HasDatabaseName("UX_MapResourceHabitat_Type_Biome_Geography");
        });

        // --- MapResourceDistributionScenario -------------------------------------------
        modelBuilder.Entity<MapResourceDistributionScenario>(entity =>
        {
            entity.ToTable("MapResourceDistributionScenario", schema);

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.MapResourceType)
                .WithMany(t => t.DistributionScenarios)
                .HasForeignKey(e => e.MapResourceTypeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MapResourceDistributionScenario_MapResourceType_MapResourceTypeId");

            entity.HasIndex(e => new { e.MapResourceTypeId, e.ScenarioNumber })
                .IsUnique()
                .HasDatabaseName("UX_MapResourceDistributionScenario_Type_ScenarioNumber");
        });

        // --- MapResourceDistributionScenarioAmount -------------------------------------
        modelBuilder.Entity<MapResourceDistributionScenarioAmount>(entity =>
        {
            entity.ToTable("MapResourceDistributionScenarioAmount", schema, table => table.HasCheckConstraint(
                "CK_MapResourceDistributionScenarioAmount_AmountPositive",
                "[Amount] > 0"));

            entity.HasKey(e => new { e.MapResourceDistributionScenarioId, e.AssetId });

            entity.HasOne(e => e.Scenario)
                .WithMany(s => s.Amounts)
                .HasForeignKey(e => e.MapResourceDistributionScenarioId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MapResourceDistributionScenarioAmount_MapResourceDistributionScenario_ScenarioId");

            entity.HasOne<Asset>()
                .WithMany()
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceDistributionScenarioAmount_Asset_AssetId");
        });

        // --- MapResourceDistributionScenarioBiomeWeight --------------------------------
        modelBuilder.Entity<MapResourceDistributionScenarioBiomeWeight>(entity =>
        {
            // A Biome where the scenario should never be picked simply has no row.
            entity.ToTable("MapResourceDistributionScenarioBiomeWeight", schema, table => table.HasCheckConstraint(
                "CK_MapResourceDistributionScenarioBiomeWeight_WeightPositive",
                "[Weight] > 0"));

            entity.HasKey(e => new { e.MapResourceDistributionScenarioId, e.BiomeTypeId });

            entity.HasOne(e => e.Scenario)
                .WithMany(s => s.BiomeWeights)
                .HasForeignKey(e => e.MapResourceDistributionScenarioId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MapResourceDistributionScenarioBiomeWeight_MapResourceDistributionScenario_ScenarioId");

            entity.HasOne<CampaignBiome>()
                .WithMany()
                .HasForeignKey(e => e.BiomeTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceDistributionScenarioBiomeWeight_CampaignBiome_BiomeTypeId");
        });

        // --- MapResourceTypeLayer ------------------------------------------------------
        modelBuilder.Entity<MapResourceTypeLayer>(entity =>
        {
            entity.ToTable("MapResourceTypeLayer", schema, table =>
            {
                table.HasCheckConstraint(
                    "CK_MapResourceTypeLayer_LayerNumberPositive",
                    "[LayerNumber] >= 1");

                table.HasCheckConstraint(
                    "CK_MapResourceTypeLayer_QuantityRange",
                    "[QuantityMin] >= 0 AND [QuantityMax] >= [QuantityMin]");

                table.HasCheckConstraint(
                    "CK_MapResourceTypeLayer_FuzzyFactorRange",
                    "[EstimationFuzzyFactorMin] >= 0 AND [EstimationFuzzyFactorMax] >= [EstimationFuzzyFactorMin]");
            });

            entity.HasKey(e => new { e.MapResourceTypeId, e.LayerNumber });

            entity.HasOne(e => e.MapResourceType)
                .WithMany(t => t.Layers)
                .HasForeignKey(e => e.MapResourceTypeId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_MapResourceTypeLayer_MapResourceType_MapResourceTypeId");

            entity.HasOne<Asset>()
                .WithMany()
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MapResourceTypeLayer_Asset_AssetId");
        });
    }

    private static void ConfigureMapGallerySchema(ModelBuilder modelBuilder)
    {
        const string schema = "mapgallery";

        // --- CampaignMapGallery ------------------------------------------------------
        modelBuilder.Entity<CampaignMapGallery>(entity =>
        {
            entity.ToTable("CampaignMapGallery", schema);

            // River edges default to None in the database as well as in C#, because the blank-map and Perlin generators insert tiles with raw SQL and
            // SqlBulkCopy that don't name these columns. The constraints are named so they never arrive as server-generated DF__ names.
            entity.Property(x => x.SouthRiver).HasDefaultValue(RiverWidth.None, "DF_CampaignMapGallery_SouthRiver");
            entity.Property(x => x.EastRiver).HasDefaultValue(RiverWidth.None, "DF_CampaignMapGallery_EastRiver");

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

        // --- CampaignMapGalleryResource ----------------------------------------------
        // A MapResource placed on a gallery tile by the map editor. Copied into game.MapResourceInstance at game setup.
        modelBuilder.Entity<CampaignMapGalleryResource>(entity =>
        {
            entity.ToTable("CampaignMapGalleryResource", schema, table =>
            {
                // Two slots, and one row per slot (below), is what caps a tile at two MapResources.
                table.HasCheckConstraint(
                    "CK_CampaignMapGalleryResource_Slot",
                    $"[Slot] BETWEEN {CampaignMapGalleryResource.MIN_SLOT} AND {CampaignMapGalleryResource.MAX_SLOT}");

                table.HasCheckConstraint(
                    "CK_CampaignMapGalleryResource_InitialQuantityNonNegative",
                    "[InitialQuantity] IS NULL OR [InitialQuantity] >= 0");
            });

            entity.HasKey(x => x.Id);

            // No navigations, and every relationship NoAction: gallery tiles are only ever updated in place, and a type placed on any map cannot be deleted.
            entity.HasOne<CampaignMapGallery>()
                .WithMany()
                .HasForeignKey(x => x.CampaignMapGalleryId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMapGalleryResource_CampaignMapGallery_CampaignMapGalleryId");

            entity.HasOne<MapResourceType>()
                .WithMany()
                .HasForeignKey(x => x.MapResourceTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMapGalleryResource_MapResourceType_MapResourceTypeId");

            entity.HasOne<MapResourceDistributionScenario>()
                .WithMany()
                .HasForeignKey(x => x.MapResourceDistributionScenarioId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CampaignMapGalleryResource_MapResourceDistributionScenario_ScenarioId");

            // At most one MapResource per slot, so at most two per tile. The tile comes first because it is also the lookup: every MapResource on a tile.
            entity.HasIndex(x => new { x.CampaignMapGalleryId, x.Slot })
                .IsUnique()
                .HasDatabaseName("UX_CampaignMapGalleryResource_Tile_Slot");

            // At most one of each type per tile: spread treats "the same MapResource on the tile" as a single instance.
            entity.HasIndex(x => new { x.CampaignMapGalleryId, x.MapResourceTypeId })
                .IsUnique()
                .HasDatabaseName("UX_CampaignMapGalleryResource_Tile_Type");
        });
    }

    private static void ConfigureSettlementSchema(ModelBuilder modelBuilder)
    {
        const string schema = "settlement";

        // --- Settlement ----------------------------------------------------------------
        modelBuilder.Entity<Settlement>(entity =>
        {
            entity.ToTable("Settlements", schema);

            entity.HasKey(e => e.Id);

            // Optional, because a camp carries no name - see Settlement.Name.
            entity.Property(e => e.Name)
                .HasMaxLength(Settlement.NAME_MAX_LENGTH);

            entity.Property(e => e.Tier)
                .HasConversion<int>()
                .IsRequired();

            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Settlements_GameIndex_GameId");

            entity.HasOne(e => e.StructureInstance)
                .WithMany()
                .HasForeignKey(e => e.StructureInstanceId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Settlements_StructureInstances_StructureInstanceId");

            entity.HasOne(e => e.FoundedBy)
                .WithMany()
                .HasForeignKey(e => e.FoundingPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Settlements_Player_FoundingPlayerId");

            // Self-referencing and optional: only a camp carries a parent settlement, and a camp founded where there is nothing to attach to carries none.
            //
            // NoAction to match every other foreign key here, and the only legal choice besides: SQL Server refuses a cascade that returns to the same table.
            entity.HasOne(e => e.ParentSettlement)
                .WithMany()
                .HasForeignKey(e => e.ParentSettlementId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Settlements_Settlements_ParentSettlementId");

            // The name is unique within a Game, which is what "unique in the world" means: two Games may each hold a Riverhold, one Game may not.
            //
            // Case-insensitivity comes from the database's default collation rather than from an explicit one here. Every other string column in this model relies on the
            // same default, and pinning a collation on this column alone would make it the one place where "same name" means something different.
            //
            // Filtered to the rows that have one, so the unnamed tiers do not collide with each other: SQL Server treats NULLs as equal in a unique index, which would
            // otherwise let a Game hold exactly one camp.
            entity.HasIndex(e => new { e.GameId, e.Name })
                .IsUnique()
                .HasFilter("[Name] IS NOT NULL")
                .HasDatabaseName("UX_Settlement_Game_Name");

            // One Settlement per Structure, and one Structure per Settlement. Unique rather than a plain index: the Structure is what stands on the tile, so two Settlements
            // sharing one would be two Settlements on one tile with no way to tell which the map was drawing.
            entity.HasIndex(e => e.StructureInstanceId)
                .IsUnique()
                .HasDatabaseName("UX_Settlement_StructureInstance");
        });
    }

    private static void ConfigureStructureSchema(ModelBuilder modelBuilder)
    {
        const string schema = "structure";

        // --- Structure -----------------------------------------------------------------
        modelBuilder.Entity<Structure>(entity =>
        {
            // Two legal values and no others. A catalogue row always asserts something, and nothing a Player builds can assert Occupied - see "Structures" in
            // docs/design/campaign-map/tile-ownership.md. The enum already makes both unrepresentable in code; this says so in the schema as well, because the catalogue
            // is also written by hand.
            entity.ToTable("Structures", schema, table => table.HasCheckConstraint(
                "CK_Structures_Asserts",
                $"[Asserts] IN ({(int)StructureAssertion.Claim}, {(int)StructureAssertion.Owned})"));

            entity.Property(e => e.Asserts)
                .HasConversion<int>()
                .IsRequired();
        });

        // --- StructureHistory ----------------------------------------------------------
        // The raw structure feed of docs/design/intelligence-and-sightings.md, the sibling of units.MovementHistory. Append-only, so it carries no RowVersion.
        modelBuilder.Entity<StructureHistory>(entity =>
        {
            // A Captured row says who held the Structure before, and no other row does. Enforced by the database because the vision seam reads the pre-capture owner from it, and a Captured row
            // without one would quietly lose that owner's observer rather than fail.
            entity.ToTable("StructureHistory", schema, table => table.HasCheckConstraint(
                "CK_StructureHistory_PreviousOwnerOnlyWhenCaptured",
                $"([EventType] = {(int)StructureHistoryEventType.Captured} AND [PreviousOwnerPlayerId] IS NOT NULL) OR ([EventType] <> {(int)StructureHistoryEventType.Captured} AND [PreviousOwnerPlayerId] IS NULL)"));

            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .HasConversion<int>()
                .IsRequired();

            // No navigations on either side, the same as MovementHistory: nothing reads the feed through an entity graph.
            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_StructureHistory_GameIndex_GameId");

            // A real foreign key, unlike MovementHistory.UnitInstanceId: razing is a soft delete, so a Structure's row is never removed while its history survives.
            entity.HasOne<StructureInstance>()
                .WithMany()
                .HasForeignKey(e => e.StructureInstanceId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_StructureHistory_StructureInstances_StructureInstanceId");

            entity.HasOne<Structure>()
                .WithMany()
                .HasForeignKey(e => e.StructureId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_StructureHistory_Structures_StructureId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_StructureHistory_Player_PlayerId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PreviousOwnerPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_StructureHistory_Player_PreviousOwnerPlayerId");

            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.CampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_StructureHistory_CampaignMap_CampaignMapTileId");

            entity.HasOne<PlayerTurn>()
                .WithMany()
                .HasForeignKey(e => e.PlayerTurnId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_StructureHistory_PlayerTurn_PlayerTurnId");

            // Every row of one Turn, in order - the shape the end-of-turn fold and the vision seam read the table in.
            entity.HasIndex(e => new { e.PlayerTurnId, e.TurnSequence })
                .HasDatabaseName("IX_StructureHistory_Turn_Sequence");

            // One Structure's events through a Turn, in order.
            entity.HasIndex(e => new { e.StructureInstanceId, e.PlayerTurnId, e.TurnSequence })
                .HasDatabaseName("IX_StructureHistory_Structure_Turn_Sequence");
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

        modelBuilder.Entity<PlayerTurn>(entity =>
        {
            entity.ToTable("PlayerTurn", schema);

            entity.HasKey(x => x.Id);

            // Deliberately no HasDefaultValue on LastAllocatedSequence. A database default arrives as an unnamed DF__ constraint, which is exactly what
            // migration TM-6_RenameStaleDefaultConstraints had to go back and clean up; the C# default of 0 is written on insert and says the same thing.

            entity.HasOne(x => x.TurnQueue)
                .WithMany()
                .HasForeignKey(x => x.TurnQueueId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerTurn_TurnQueue_TurnQueueId");

            entity.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerTurn_Player_PlayerId");

            // A lane hands the Turn to exactly one Player at a time, so at most one row in it may be open.
            //
            // Enforced by the database rather than only in code because the consequence of a second open Turn is silent: sequence numbers would be handed
            // out from two counters, and the feeds would interleave into an order that never happened. Nothing downstream could tell.
            entity.HasIndex(x => x.TurnQueueId)
                .IsUnique()
                .HasFilter("[EndedAt] IS NULL")
                .HasDatabaseName("UX_PlayerTurn_OpenTurnPerQueue");

            // "This Player's most recent Turn in this lane" - the map read's stamp lookup today, and the intelligence feed's "since my last Turn began"
            // later.
            //
            // Deliberately not unique on (TurnQueueId, RoundNumber, PlayerId). That looks equally true today, but it is a rule about the campaign pipeline
            // rather than about this table: Battle lanes run Rounds outside the campaign order, and EndTurnMode.Developer_AllAboutMe is specified to hand
            // one Player repeated Turns.
            entity.HasIndex(x => new { x.TurnQueueId, x.PlayerId })
                .HasDatabaseName("IX_PlayerTurn_Queue_Player");

            // "Every Turn of Round N", which is how the end-of-turn fold and any Round-level audit read the table.
            entity.HasIndex(x => new { x.TurnQueueId, x.RoundNumber })
                .HasDatabaseName("IX_PlayerTurn_Queue_Round");
        });
    }

    private static void ConfigureUnitSchema(ModelBuilder modelBuilder)
    {
        const string schema = "units";

        // --- UnitInstances --------------------------------------------------------
        modelBuilder.Entity<UnitInstance>(entity =>
        {
            entity.ToTable("UnitInstances", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.BattleMapTileId).IsRequired(false);

            // UnitInstance -> GameIndex (many units per game)
            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitInstances_GameIndex_GameId");

            // UnitInstance -> Player (many units per player)
            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitInstances_Player_PlayerId");

            entity.HasOne(e => e.CampaignMap)
                .WithMany()
                .HasForeignKey(e => e.CampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitInstances_CampaignMap_CampaignMapTileId");

            entity.HasOne(e => e.UnitTemplate)
                 .WithMany()
                 .HasForeignKey(e => e.UnitTemplateId)
                 .OnDelete(DeleteBehavior.NoAction)
                 .HasConstraintName("FK_UnitInstances_UnitTemplate_UnitTemplateId");

            // A Unit under no Command is the ordinary case, so this is nullable and its index is filtered.
            entity.HasOne(e => e.Command)
                .WithMany(c => c.Units)
                .HasForeignKey(e => e.CommandId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitInstances_Commands_CommandId");

            entity.HasIndex(e => e.CommandId)
                .HasFilter("[CommandId] IS NOT NULL");
        });

        // --- MovementHistory -------------------------------------------------------
        // The raw movement feed of docs/design/intelligence-and-sightings.md, and the highest-volume table in that design. Append-only, so it carries no RowVersion.
        modelBuilder.Entity<MovementHistory>(entity =>
        {
            // A Moved row says where the Force came from, and no other row does. Enforced by the database because the fold computes exit directions from it, and a Moved row without it would
            // quietly compute no direction at all rather than fail.
            entity.ToTable("MovementHistory", schema, table => table.HasCheckConstraint(
                "CK_MovementHistory_FromTileOnlyWhenMoved",
                $"([EventType] = {(int)ForceHistoryEventType.Moved} AND [FromCampaignMapTileId] IS NOT NULL) OR ([EventType] <> {(int)ForceHistoryEventType.Moved} AND [FromCampaignMapTileId] IS NULL)"));

            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .HasConversion<int>()
                .IsRequired();

            // No navigations on either side of these. Nothing reads the feed through an entity graph, and a collection of every step a Force ever took is not something anyone should be able to
            // Include by accident.
            entity.HasOne<GameIndex>()
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MovementHistory_GameIndex_GameId");

            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MovementHistory_Player_PlayerId");

            entity.HasOne<UnitTemplate>()
                .WithMany()
                .HasForeignKey(e => e.UnitTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MovementHistory_UnitTemplate_UnitTemplateId");

            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.CampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MovementHistory_CampaignMap_CampaignMapTileId");

            entity.HasOne<CampaignMap>()
                .WithMany()
                .HasForeignKey(e => e.FromCampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MovementHistory_CampaignMap_FromCampaignMapTileId");

            entity.HasOne<PlayerTurn>()
                .WithMany()
                .HasForeignKey(e => e.PlayerTurnId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_MovementHistory_PlayerTurn_PlayerTurnId");

            // Deliberately no relationship for UnitInstanceId. A destroyed Force is hard-deleted before the end-of-turn fold reads its rows, and a foreign key would either block that delete or
            // clear the column the fold groups a Force's path by. See the property's remarks.

            // Every row of one Turn, in order - the shape the end-of-turn fold reads the table in.
            entity.HasIndex(e => new { e.PlayerTurnId, e.TurnSequence })
                .HasDatabaseName("IX_MovementHistory_Turn_Sequence");

            // One Force's path through a Turn, in order - the shape tracks are folded from.
            entity.HasIndex(e => new { e.UnitInstanceId, e.PlayerTurnId, e.TurnSequence })
                .HasDatabaseName("IX_MovementHistory_Unit_Turn_Sequence");
        });

        // --- Commands --------------------------------------------------------------
        modelBuilder.Entity<Command>(entity =>
        {
            entity.ToTable("Commands", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name).HasMaxLength(Command.NAME_MAX_LENGTH);

            entity.Property(e => e.BattleMapTileId).IsRequired(false);

            entity.HasOne(e => e.Game)
                .WithMany()
                .HasForeignKey(e => e.GameId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Commands_GameIndex_GameId");

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.OwnerPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Commands_Player_OwnerPlayerId");

            entity.HasOne(e => e.CampaignMap)
                .WithMany()
                .HasForeignKey(e => e.CampaignMapTileId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_Commands_CampaignMap_CampaignMapTileId");

            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.OwnerPlayerId);
            entity.HasIndex(e => e.CampaignMapTileId);
        });

        modelBuilder.Entity<ForceOrder>(entity =>
        {
            entity.ToTable("ForceOrders", schema);

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

            entity.HasOne(e => e.UnitInstance)
                .WithMany(t => t.Orders)
                .HasForeignKey(e => e.UnitInstanceId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasOne(e => e.Command)
                .WithMany()
                .HasForeignKey(e => e.CommandId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ForceOrders_Commands_CommandId");

            // An order targets a Force, which is either a Unit or a Command and never both or neither. The database is what guarantees that,
            // because nothing else can: the two columns are nullable so that either may be absent, and only a constraint can say "exactly one".
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_ForceOrders_UnitOrCommandXor",
                "([UnitInstanceId] IS NOT NULL AND [CommandId] IS NULL) OR ([UnitInstanceId] IS NULL AND [CommandId] IS NOT NULL)"));

            entity.HasMany(e => e.MovementSteps)
                .WithOne(e => e.ForceOrder)
                .HasForeignKey(e => e.ForceOrderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.GameId);
            entity.HasIndex(e => e.UnitInstanceId);
            entity.HasIndex(e => new { e.GameId, e.UnitInstanceId, e.OrderStatus });
            entity.HasIndex(e => new { e.GameId, e.OrderStatus });
            entity.HasIndex(e => new { e.GameId, e.OrderType, e.OrderStatus });
            entity.HasIndex(e => e.TargetCampaignMapTileId);

            entity.HasIndex(e => e.StructureInstanceId);
            entity.HasIndex(e => e.ResourceDefinitionId);

            entity.HasIndex(e => e.TradeSourceStructureInstanceId);
            entity.HasIndex(e => e.TradeTargetStructureInstanceId);

            entity.HasIndex(e => e.DefendTargetUnitInstanceId);
            entity.HasIndex(e => e.DefendTargetStructureInstanceId);
            entity.HasIndex(x => new { x.UnitInstanceId, x.OrderStatus });

            // Filtered, because exactly one half of the target pair is populated on any given row: an unfiltered index over CommandId would be
            // mostly nulls for as long as Commands are rarer than lone Units, which the officer economy makes permanent rather than temporary.
            entity.HasIndex(e => e.CommandId)
                .HasFilter("[CommandId] IS NOT NULL");

            entity.HasIndex(x => new { x.CommandId, x.OrderStatus })
                .HasFilter("[CommandId] IS NOT NULL");
        });

        modelBuilder.Entity<ForceOrderMovementStep>(entity =>
        {
            entity.ToTable("ForceOrderMovementSteps", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.IsCompleted)
                .HasDefaultValue(false);

            entity.HasOne(e => e.ForceOrder)
                .WithMany(e => e.MovementSteps)
                .HasForeignKey(e => e.ForceOrderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(e => e.ForceOrderId);

            entity.HasIndex(e => new { e.ForceOrderId, e.StepIndex })
                .IsUnique();

            entity.HasIndex(e => new { e.CampaignMapTileId, e.IsCompleted });
        });

        modelBuilder.Entity<ForceOrderParticipant>(entity =>
        {
            entity.ToTable("ForceOrderParticipants", schema);

            entity.Property(x => x.Role)
                .HasConversion<int>()
                .IsRequired();

            // Wired to ForceOrder.Participants explicitly. Left as a bare WithMany(), EF treated that collection as a second, separate relationship and
            // invented a shadow ForceOrderId1 foreign key column alongside this one.
            entity.HasOne(e => e.ForceOrder)
                .WithMany(o => o.Participants)
                .HasForeignKey(e => e.ForceOrderId)
                .OnDelete(DeleteBehavior.NoAction);

            entity.HasIndex(x => x.ForceOrderId);

            entity.HasIndex(x => x.UnitInstanceId);

            entity.HasIndex(x => new { x.ForceOrderId, x.UnitInstanceId })
                .IsUnique();

            entity.HasOne(x => x.UnitInstance)
                .WithMany()
                .HasForeignKey(x => x.UnitInstanceId)
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

        ConfigureUnitComponentCatalogue(modelBuilder, schema);
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
    /// Configures the global component catalogue: components, body extension, slots, slot rules and buff tags.
    /// </summary>
    private static void ConfigureUnitComponentCatalogue(ModelBuilder modelBuilder, string schema)
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

        // --- UnitComponentWeaponAmmunition -----------------------------------------
        modelBuilder.Entity<UnitComponentWeaponAmmunition>(entity =>
        {
            entity.ToTable("UnitComponentWeaponAmmunition", schema);

            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Weapon)
                .WithMany(e => e.Ammunition)
                .HasForeignKey(e => e.UnitComponentWeaponId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentWeaponAmmunition_UnitComponentWeapon_UnitComponentWeaponId");

            // Reaches across to the assets schema, as a supply requirement reaches a SupplySet.
            entity.HasOne(e => e.Asset)
                .WithMany()
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentWeaponAmmunition_Asset_AssetId");

            // A weapon lists each Asset at most once, and the merge keys on the Asset.
            entity.HasIndex(e => new { e.UnitComponentWeaponId, e.AssetId })
                .IsUnique()
                .HasDatabaseName("UX_UnitComponentWeaponAmmunition_Weapon_Asset");
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

            // The slot's portable identity. Unique per body rather than globally, so two different bodies may
            // each expose a "MAIN_HAND" without colliding.
            entity.Property(e => e.Code)
                .HasMaxLength(UnitComponentSlot.CODE_MAX_LENGTH)
                .IsRequired();

            entity.HasIndex(e => new { e.UnitComponentBodyId, e.Code })
                .IsUnique()
                .HasDatabaseName("UX_UnitComponentSlot_Body_Code");

            // Required: Code is slugified from the display name, so a blank name means a blank identity.
            entity.Property(e => e.DisplayName)
                .HasMaxLength(UnitComponentSlot.DISPLAY_NAME_MAX_LENGTH)
                .IsRequired();

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

            // Nullable — only organisation-group tags carry a leadership model; stored as the enum's int value.
            // The three Unled* sizes are plain nullable ints and need no explicit configuration.
            entity.Property(e => e.LeadershipModel)
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

            // A colour is how a tag type is recognised at a glance, so no two may share one. Filtered to the
            // rows that have a colour: any number of tag types may go uncoloured, and an unfiltered unique
            // index would treat their NULLs as equal and allow only one. UpsertTagTypesHandler enforces the
            // same rule up front so a clash surfaces as a validation error rather than a database exception.
            entity.HasIndex(e => e.Colour)
                .IsUnique()
                .HasFilter("[Colour] IS NOT NULL")
                .HasDatabaseName("UX_TagType_Colour");

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

        // --- UnitComponentSupplyRequirement ----------------------------------------
        // Keyed to the component rather than to its Body extension, though only a Body may carry one today: the reconciler enforces that, so moving a
        // need onto another component type stays a validation change. The set it points at lives in the assets schema.
        modelBuilder.Entity<UnitComponentSupplyRequirement>(entity =>
        {
            entity.ToTable("UnitComponentSupplyRequirement", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Quantity).IsRequired();

            entity.HasOne(e => e.UnitComponent)
                .WithMany(e => e.SupplyRequirements)
                .HasForeignKey(e => e.UnitComponentId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentSupplyRequirement_UnitComponent_UnitComponentId");

            entity.HasOne(e => e.SupplySet)
                .WithMany()
                .HasForeignKey(e => e.SupplySetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_UnitComponentSupplyRequirement_SupplySet_SupplySetId");

            // Unique rather than a plain index on the component: two rows for one set would double the need, and the reconciler keys its merge on the set,
            // so a duplicate would make the component un-editable. It covers lookups by component as well.
            entity.HasIndex(e => new { e.UnitComponentId, e.SupplySetId })
                .IsUnique()
                .HasDatabaseName("UX_UnitComponentSupplyRequirement_Component_SupplySet");

            entity.HasIndex(e => e.SupplySetId)
                .HasDatabaseName("IX_UnitComponentSupplyRequirement_SupplySet");
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
                .WithOne(t => t.Compiled)
                .HasForeignKey<CompiledUnitTemplate>(e => e.UnitTemplateId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CompiledUnitTemplate_UnitTemplate_UnitTemplateId");

            // The organisation tag resolved from the template's components at compile time (nullable —
            // not every template carries one).
            entity.HasOne(e => e.OrganisationTag)
                .WithMany()
                .HasForeignKey(e => e.OrganisationTagId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_CompiledUnitTemplate_Tag_OrganisationTagId");
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
    /// Configures the technology schema - the Technology catalogue, its prerequisite graph, its grants and
    /// experience mappings, and every per-player research table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Follows the assets schema throughout: composite keys on the join tables, <c>NoAction</c> on every
    /// foreign key, and an explicit constraint or index name on each one. NoAction is not optional here -
    /// Player and Technology both sit at the head of several paths, and cascading either would create
    /// multiple cascade paths into them.
    /// </para>
    /// <para>
    /// The check constraints are worth the trouble because the alternative is state that can quietly
    /// disagree with itself: a <c>PlayerTechnology</c> whose status says Discovered with no round stamped,
    /// or an acquisition cost row for nothing.
    /// </para>
    /// </remarks>
    private static void ConfigureTechnologySchema(ModelBuilder modelBuilder)
    {
        const string schema = "technology";

        // --- Technology catalogue ------------------------------------------------------
        // Code is the match key every technology upsert works from, so it is length-capped and uniquely
        // indexed. Without the unique index nothing stops two rows sharing a code, and the upsert would
        // silently update whichever the dictionary happened to keep.
        modelBuilder.Entity<Technology>(entity =>
        {
            // At least one bucket must be in use, matching the design doc's "between 1 and 3 buckets". A
            // technology with all three null could never be discovered by any means.
            entity.ToTable("Technology", schema, t => t.HasCheckConstraint(
                "CK_Technology_AtLeastOneBucket",
                "[DirectedRequired] IS NOT NULL OR [ExperienceRequired] IS NOT NULL OR [DiffusionRequired] IS NOT NULL"));

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code).HasMaxLength(Technology.MAX_CODE_LENGTH).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(Technology.MAX_DISPLAY_NAME_LENGTH).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(Technology.MAX_DESCRIPTION_LENGTH);
            entity.Property(e => e.IconPath).HasMaxLength(Technology.MAX_ICON_PATH_LENGTH).IsRequired();

            entity.Property(e => e.Era).HasConversion<int>().IsRequired();

            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_Technology_Code");
            entity.HasIndex(e => e.DisplayName).IsUnique().HasDatabaseName("UX_Technology_DisplayName");

            // SortOrder is per era, the same way an AssetGroup's is per category.
            entity.HasIndex(e => new { e.Era, e.SortOrder })
                .IsUnique()
                .HasDatabaseName("UX_Technology_Era_SortOrder");
        });

        // --- ExperienceType catalogue --------------------------------------------------
        modelBuilder.Entity<ExperienceType>(entity =>
        {
            entity.ToTable("ExperienceType", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Code).HasMaxLength(ExperienceType.MAX_CODE_LENGTH).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(ExperienceType.MAX_DISPLAY_NAME_LENGTH).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(ExperienceType.MAX_DESCRIPTION_LENGTH);
            entity.Property(e => e.IconPath).HasMaxLength(ExperienceType.MAX_ICON_PATH_LENGTH).IsRequired();

            entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UX_ExperienceType_Code");
            entity.HasIndex(e => e.DisplayName).IsUnique().HasDatabaseName("UX_ExperienceType_DisplayName");
            entity.HasIndex(e => e.SortOrder).IsUnique().HasDatabaseName("UX_ExperienceType_SortOrder");
        });

        // --- TechnologyAcquisitionCost -------------------------------------------------
        modelBuilder.Entity<TechnologyAcquisitionCost>(entity =>
        {
            // A free technology has no row, so a row of nothing is always a mistake.
            entity.ToTable("TechnologyAcquisitionCost", schema, t => t.HasCheckConstraint(
                "CK_TechnologyAcquisitionCost_QuantityPositive",
                "[Quantity] > 0"));

            entity.HasKey(e => new { e.TechnologyId, e.AssetId });

            entity.HasOne(e => e.Technology)
                .WithMany(e => e.AcquisitionCosts)
                .HasForeignKey(e => e.TechnologyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TechnologyAcquisitionCost_Technology_TechnologyId");

            entity.HasOne(e => e.Asset)
                .WithMany()
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TechnologyAcquisitionCost_Asset_AssetId");

            // "What does this Asset get spent on?" - also the check the Asset delete path needs.
            entity.HasIndex(e => e.AssetId)
                .HasDatabaseName("IX_TechnologyAcquisitionCost_Asset");
        });

        // --- TechnologyPrerequisite ----------------------------------------------------
        modelBuilder.Entity<TechnologyPrerequisite>(entity =>
        {
            // A self-prerequisite would make the technology permanently unreachable. Longer cycles do the
            // same but cannot be expressed here, so the upsert handler has to reject those.
            entity.ToTable("TechnologyPrerequisite", schema, t => t.HasCheckConstraint(
                "CK_TechnologyPrerequisite_NotSelf",
                "[TechnologyId] <> [PrerequisiteTechnologyId]"));

            entity.HasKey(e => new { e.TechnologyId, e.PrerequisiteTechnologyId });

            entity.Property(e => e.PrerequisiteKind).HasConversion<int>().IsRequired();

            // Both self-referencing foreign keys must be NoAction: cascading either way would create
            // multiple cascade paths into Technology.
            entity.HasOne(e => e.Technology)
                .WithMany(e => e.Prerequisites)
                .HasForeignKey(e => e.TechnologyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TechnologyPrerequisite_Technology_TechnologyId");

            entity.HasOne(e => e.PrerequisiteTechnology)
                .WithMany(e => e.Dependents)
                .HasForeignKey(e => e.PrerequisiteTechnologyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TechnologyPrerequisite_Technology_PrerequisiteTechnologyId");

            // The discovery cascade walks this edge - "what depends on the technology just discovered?" -
            // which makes it the hottest query in the feature.
            entity.HasIndex(e => e.PrerequisiteTechnologyId)
                .HasDatabaseName("IX_TechnologyPrerequisite_Prerequisite");
        });

        // --- TechnologyGrant -----------------------------------------------------------
        modelBuilder.Entity<TechnologyGrant>(entity =>
        {
            entity.ToTable("TechnologyGrant", schema);

            entity.HasKey(e => new { e.TechnologyId, e.TargetType, e.TargetId });

            entity.Property(e => e.TargetType).HasConversion<int>().IsRequired();
            entity.Property(e => e.GrantEffect).HasConversion<int>().IsRequired();

            entity.HasOne(e => e.Technology)
                .WithMany(e => e.Grants)
                .HasForeignKey(e => e.TechnologyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TechnologyGrant_Technology_TechnologyId");

            // TargetId has no foreign key, so this index is the only way to answer "which technology
            // granted this?" - needed both to tell the player why something appeared and to warn the
            // Entity Setup Tool when two technologies grant the same thing.
            entity.HasIndex(e => new { e.TargetType, e.TargetId })
                .HasDatabaseName("IX_TechnologyGrant_Target");
        });

        // --- TechnologyExperienceType --------------------------------------------------
        modelBuilder.Entity<TechnologyExperienceType>(entity =>
        {
            entity.ToTable("TechnologyExperienceType", schema);

            entity.HasKey(e => new { e.TechnologyId, e.ExperienceTypeId });

            entity.HasOne(e => e.Technology)
                .WithMany(e => e.ExperienceTypes)
                .HasForeignKey(e => e.TechnologyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TechnologyExperienceType_Technology_TechnologyId");

            entity.HasOne(e => e.ExperienceType)
                .WithMany(e => e.Technologies)
                .HasForeignKey(e => e.ExperienceTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_TechnologyExperienceType_ExperienceType_ExperienceTypeId");

            // The allocator's lookup: given a type of experience, which technologies can take it.
            entity.HasIndex(e => e.ExperienceTypeId)
                .HasDatabaseName("IX_TechnologyExperienceType_ExperienceType");
        });

        // --- Per-player research state -------------------------------------------------
        modelBuilder.Entity<PlayerTechnology>(entity =>
        {
            // Keeps the status enum and the round stamps from drifting apart. Locked and Available share a
            // clause because neither has paid nor finished; Discovered deliberately says nothing about the
            // acquisition stamp, since a technology can be traded or conquered without ever being paid for.
            entity.ToTable("PlayerTechnology", schema, t =>
            {
                t.HasCheckConstraint(
                    "CK_PlayerTechnology_StatusStamps",
                    "([Status] IN (1, 2) AND [AcquisitionPaidOnRound] IS NULL AND [DiscoveredOnRound] IS NULL)"
                    + " OR ([Status] = 3 AND [AcquisitionPaidOnRound] IS NOT NULL AND [DiscoveredOnRound] IS NULL)"
                    + " OR ([Status] = 4 AND [DiscoveredOnRound] IS NOT NULL)");

                t.HasCheckConstraint(
                    "CK_PlayerTechnology_PointsNonNegative",
                    "[DirectedPoints] >= 0 AND [ExperiencePoints] >= 0 AND [DiffusionPoints] >= 0");

                // Mirrors CK_Technology_AtLeastOneBucket: the generator copies the catalogue's nullability
                // across, so a row with no bucket at all means the generator went wrong.
                t.HasCheckConstraint(
                    "CK_PlayerTechnology_AtLeastOneBucket",
                    "[DirectedRequired] IS NOT NULL OR [ExperienceRequired] IS NOT NULL OR [DiffusionRequired] IS NOT NULL");
            });

            entity.HasKey(e => new { e.PlayerId, e.TechnologyId });

            entity.Property(e => e.Status).HasConversion<int>().IsRequired();
            entity.Property(e => e.DiscoveryMethod).HasConversion<int>();

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerTechnology_Player_PlayerId");

            entity.HasOne(e => e.Technology)
                .WithMany()
                .HasForeignKey(e => e.TechnologyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerTechnology_Technology_TechnologyId");

            // No navigation property for the source player: it is read as an id when attributing a theft or
            // a trade, never walked as a graph.
            entity.HasOne<Player>()
                .WithMany()
                .HasForeignKey(e => e.DiscoveredFromPlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerTechnology_Player_DiscoveredFromPlayerId");

            // The end-of-turn "where can these points go?" query, run once per bucket per turn. The points
            // are included so it can be answered without touching the table itself.
            entity.HasIndex(e => new { e.PlayerId, e.Status })
                .IncludeProperties(e => new { e.DirectedPoints, e.ExperiencePoints, e.DiffusionPoints })
                .HasDatabaseName("IX_PlayerTechnology_Player_Status");

            // "Which other players in this game have this technology?" - diffusion, whether a trade is
            // worth offering, and who is worth spying on. Scoped by game because it is never asked across
            // games, and leading with GameId keeps it from spanning the whole table.
            entity.HasIndex(e => new { e.GameId, e.TechnologyId, e.Status })
                .HasDatabaseName("IX_PlayerTechnology_Game_Technology_Status");
        });

        modelBuilder.Entity<PlayerResearchState>(entity =>
        {
            // The population divisor is constrained positive rather than merely non-negative: it divides, so a zero would fault the start-of-turn
            // generation for whichever player happened to hold it.
            entity.ToTable("PlayerResearchState", schema, t => t.HasCheckConstraint(
                "CK_PlayerResearchState_TechPointsPerPopulationSizePositive",
                "[TechPointsPerPopulationSize] > 0"));

            // Shared primary key with Player: exactly one row per player.
            entity.HasKey(e => e.PlayerId);
            entity.Property(e => e.PlayerId).ValueGeneratedNever();

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerResearchState_Player_PlayerId");

            entity.HasOne(e => e.CurrentTechnology)
                .WithMany()
                .HasForeignKey(e => e.CurrentTechnologyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerResearchState_Technology_CurrentTechnologyId");

            entity.HasOne(e => e.NextTechnology)
                .WithMany()
                .HasForeignKey(e => e.NextTechnologyId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerResearchState_Technology_NextTechnologyId");

            // No reverse index on either technology column: "who is researching X?" is never asked, and
            // there is only one row per player to scan anyway.
        });

        // --- Experience accrual --------------------------------------------------------
        modelBuilder.Entity<PendingExperienceAccrual>(entity =>
        {
            entity.ToTable("PendingExperienceAccrual", schema, t => t.HasCheckConstraint(
                "CK_PendingExperienceAccrual_PointsNonNegative",
                "[Points] >= 0"));

            entity.HasKey(e => new { e.PlayerId, e.ExperienceTypeId });

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PendingExperienceAccrual_Player_PlayerId");

            entity.HasOne(e => e.ExperienceType)
                .WithMany()
                .HasForeignKey(e => e.ExperienceTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PendingExperienceAccrual_ExperienceType_ExperienceTypeId");

            entity.HasIndex(e => e.ExperienceTypeId)
                .HasDatabaseName("IX_PendingExperienceAccrual_ExperienceType");
        });

        modelBuilder.Entity<PlayerUnusedExperience>(entity =>
        {
            entity.ToTable("PlayerUnusedExperience", schema, t => t.HasCheckConstraint(
                "CK_PlayerUnusedExperience_PointsNonNegative",
                "[Points] >= 0"));

            entity.HasKey(e => new { e.PlayerId, e.ExperienceTypeId });

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnusedExperience_Player_PlayerId");

            entity.HasOne(e => e.ExperienceType)
                .WithMany()
                .HasForeignKey(e => e.ExperienceTypeId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerUnusedExperience_ExperienceType_ExperienceTypeId");

            // "Who has wasted the most of this kind of experience?" - compared across the players of one
            // game, so the game leads.
            entity.HasIndex(e => new { e.GameId, e.ExperienceTypeId })
                .HasDatabaseName("IX_PlayerUnusedExperience_Game_ExperienceType");
        });

        // --- Research generation and history -------------------------------------------
        modelBuilder.Entity<PlayerResearchModifier>(entity =>
        {
            entity.ToTable("PlayerResearchModifier", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.SourceType).HasConversion<int>().IsRequired();
            entity.Property(e => e.Bucket).HasConversion<int>().IsRequired();

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_PlayerResearchModifier_Player_PlayerId");

            // One modifier per player / source / bucket - the natural key, enforced by index because the
            // table carries a surrogate key to match PlayerSustenanceModifier.
            entity.HasIndex(e => new { e.PlayerId, e.SourceType, e.SourceId, e.Bucket })
                .IsUnique()
                .HasDatabaseName("UX_PlayerResearchModifier_Player_Source_Bucket");
        });

        modelBuilder.Entity<ResearchHistory>(entity =>
        {
            entity.ToTable("ResearchHistory", schema);

            entity.HasKey(e => e.Id);

            entity.Property(e => e.Bucket).HasConversion<int>().IsRequired();

            entity.HasOne(e => e.Player)
                .WithMany()
                .HasForeignKey(e => e.PlayerId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ResearchHistory_Player_PlayerId");

            entity.HasOne(e => e.PlayerTurn)
                .WithMany()
                .HasForeignKey(e => e.PlayerTurnId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_ResearchHistory_PlayerTurn_PlayerTurnId");

            // How the feed is read: one player's research, most recent round first.
            entity.HasIndex(e => new { e.PlayerId, e.RoundNumber })
                .HasDatabaseName("IX_ResearchHistory_Player_Round");

            // Append-only, so no RowVersion - matching MovementHistory, StructureHistory and BattleHistory.
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
