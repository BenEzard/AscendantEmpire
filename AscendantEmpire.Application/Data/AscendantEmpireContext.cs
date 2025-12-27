using AscendantEmpire.Domain.Entities.Games;
using AscendantEmpire.Domain.Entities.Maps.CampaignMaps;
using AscendantEmpire.Domain.Entities.Util;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AscendantEmpire.Application.Data;

public class AscendantEmpireContext : DbContext
{
    // Map Schema
    public DbSet<CampaignBiome> CampaignBiomes { get; set; }
    public DbSet<CampaignGeography> CampaignGeographies { get; set; }
    public DbSet<CampaignMapGallery> CampaignMapGallery { get; set; }
    public DbSet<CampaignMapGalleryIndex> CampaignMapGalleryIndex { get; set; }

    // Game Schema
    public DbSet<GameIndex> GameIndex { get; set; }
    public DbSet<Player> Players { get; set; }
    public DbSet<CampaignMap> CampaignMaps { get; set; }
    public DbSet<PlayerCampaignMap> PlayerCampaignMaps { get; set; }

    // Util Schema
    public DbSet<Number> Numbers { get; set; }

    public AscendantEmpireContext(DbContextOptions<AscendantEmpireContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        SetDecimalPrecision(modelBuilder, 18, 3);

        // CampaignMap
        modelBuilder.Entity<CampaignMap>(b =>
        {
            b.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.NoAction);

            // Lookup tables: stop cascade paths
            b.HasOne(x => x.Biome)
                .WithMany()
                .HasForeignKey(x => x.BiomeTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.Geography)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapGeographyTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            // Optional relationships (because FK is nullable)
            b.HasOne(x => x.FirstVisitor)
                .WithMany()
                .HasForeignKey(x => x.FirstVisitedPlayerId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.LastVisitor)
                .WithMany()
                .HasForeignKey(x => x.LastVisitedPlayerId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // PlayerCampaignMap
        modelBuilder.Entity<PlayerCampaignMap>(b =>
        {
            b.HasOne(x => x.Game)
                .WithMany()
                .HasForeignKey(x => x.GameId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.Player)
                .WithMany()
                .HasForeignKey(x => x.PlayerId)
                .OnDelete(DeleteBehavior.Cascade);

            // Keep this as cascade (deleting a map deletes its per-player rows)
            b.HasOne(x => x.CampaignMap)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapId)
                .OnDelete(DeleteBehavior.Cascade);

            // Lookup tables: stop cascade paths
            b.HasOne(x => x.Biome)
                .WithMany()
                .HasForeignKey(x => x.BiomeTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.Geography)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapGeographyTypeId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // CampaignMapGallery (you’re missing the FK mapping for Index)
        modelBuilder.Entity<CampaignMapGallery>(b =>
        {
            b.HasOne(x => x.Biome)
                .WithMany()
                .HasForeignKey(x => x.BiomeTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.Geography)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapGeographyTypeId)
                .OnDelete(DeleteBehavior.NoAction);

            b.HasOne(x => x.Index)
                .WithMany()
                .HasForeignKey(x => x.CampaignMapGalleryIndexId)
                .OnDelete(DeleteBehavior.Cascade);
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
