/*
    CM-25 - Reset all games and their map data
    ==========================================

    Run this ONCE, BEFORE applying the ApplicationAssignedCampaignMapTileIds migration.

    Why it is needed
    ----------------
    That migration rebuilds game.CampaignMap and game.PlayerCampaignMap without their IDENTITY
    property, so the application can assign tile ids from coordinates instead of trying to predict
    what the database would assign. Clearing the tables first keeps the rebuild trivial, and the
    existing rows are unusable anyway: their PlayerCampaignMap ids were numbered in column-major
    order, which is not the row-major order every map read assumes.

    What it deletes
    ---------------
    Every game, and everything scoped to a game or a player: troops, orders, structures, battles,
    turn queues, per-player unlocks, per-player military organisation, and both campaign maps.

    What it preserves
    -----------------
    All catalogue and reference data: biomes, geographies, tag types, unit components, the map
    gallery, users and permissions, and the GLOBAL unit templates (those with no GameId and no
    PlayerId) together with their compiled snapshots and component rows. Only player-owned custom
    templates are removed, because they belong to a game that is going away.

    This script is destructive, and is deliberately left for a human to run.
*/

-- Several tables in this database carry filtered indexes, and DELETE against those requires
-- QUOTED_IDENTIFIER ON. sqlcmd does not set it by default, so set it explicitly rather than
-- depending on whichever client happens to run this.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

BEGIN TRY

    -- Custom (player-owned) templates only. Global templates have both GameId and PlayerId null
    -- and must survive: they are catalogue data, and new games are seeded from them.
    DECLARE @CustomTemplates TABLE (UnitTemplateId INT PRIMARY KEY);

    INSERT INTO @CustomTemplates (UnitTemplateId)
    SELECT [Id]
    FROM [unitdesigns].[UnitTemplate]
    WHERE [GameId] IS NOT NULL
       OR [PlayerId] IS NOT NULL;

    ---------------------------------------------------------------------------
    -- 1. Troops and their orders
    ---------------------------------------------------------------------------
    DELETE FROM [troops].[TroopOrderParticipants];
    DELETE FROM [troops].[TroopOrders];
    DELETE FROM [units].[TroopInstances];

    ---------------------------------------------------------------------------
    -- 2. Structures
    ---------------------------------------------------------------------------
    DELETE FROM [game].[PlayerKnownStructures];
    DELETE FROM [game].[StructureInstances];

    ---------------------------------------------------------------------------
    -- 3. Turn queues
    ---------------------------------------------------------------------------
    DELETE FROM [turns].[TurnQueueParticipant];
    DELETE FROM [turns].[TurnQueue];

    ---------------------------------------------------------------------------
    -- 4. Battles and battle maps
    ---------------------------------------------------------------------------
    DELETE FROM [battle].[BattleParticipant];
    DELETE FROM [battle].[Battle];
    DELETE FROM [battle].[BattleMap];
    DELETE FROM [battle].[BattleMapIndex];

    ---------------------------------------------------------------------------
    -- 5. Per-player unit design state
    ---------------------------------------------------------------------------
    DELETE FROM [unitdesigns].[PlayerUnlockedTemplate];
    DELETE FROM [unitdesigns].[PlayerUnlockedComponent];
    DELETE FROM [unitdesigns].[PlayerUnitDesignStates];
    DELETE FROM [unitdesigns].[CompiledDesignStats];
    DELETE FROM [unitdesigns].[UnitDesigns];

    ---------------------------------------------------------------------------
    -- 6. Player-owned custom templates (global ones are left alone)
    ---------------------------------------------------------------------------
    DELETE FROM [unitdesigns].[TemplateSupersession]
    WHERE [PredecessorTemplateId] IN (SELECT [UnitTemplateId] FROM @CustomTemplates)
       OR [SuccessorTemplateId] IN (SELECT [UnitTemplateId] FROM @CustomTemplates);

    DELETE FROM [unitdesigns].[UnitTemplateComponent]
    WHERE [UnitTemplateId] IN (SELECT [UnitTemplateId] FROM @CustomTemplates);

    DELETE FROM [unitdesigns].[CompiledUnitTemplate]
    WHERE [UnitTemplateId] IN (SELECT [UnitTemplateId] FROM @CustomTemplates);

    DELETE FROM [unitdesigns].[UnitTemplate]
    WHERE [Id] IN (SELECT [UnitTemplateId] FROM @CustomTemplates);

    ---------------------------------------------------------------------------
    -- 7. Per-player military organisation
    ---------------------------------------------------------------------------
    DELETE FROM [military].[PlayerOrganisationStructure];
    DELETE FROM [military].[PlayerOfficerRank];

    ---------------------------------------------------------------------------
    -- 8. The maps, then the players, then the games.
    --    CampaignMap carries FKs to Player (ControlledByPlayerId, FirstSeenByPlayerId,
    --    LastVisitedByPlayerId), so it has to go before Player does.
    ---------------------------------------------------------------------------
    DELETE FROM [game].[PlayerCampaignMap];
    DELETE FROM [game].[CampaignMap];
    DELETE FROM [game].[Player];
    DELETE FROM [game].[GameIndex];

    ---------------------------------------------------------------------------
    -- 9. Restart the surviving identity counters, so the next game is id 1 again.
    --    CampaignMap and PlayerCampaignMap are absent here on purpose: the migration
    --    removes their IDENTITY property, and the application numbers them from now on.
    ---------------------------------------------------------------------------
    DBCC CHECKIDENT ('[game].[GameIndex]', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('[game].[Player]', RESEED, 0) WITH NO_INFOMSGS;

    COMMIT TRANSACTION;

    PRINT 'CM-25 reset complete. Now apply the ApplicationAssignedCampaignMapTileIds migration.';

END TRY
BEGIN CATCH

    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
    END

    PRINT 'CM-25 reset FAILED and was rolled back. Nothing was changed.';
    THROW;

END CATCH
