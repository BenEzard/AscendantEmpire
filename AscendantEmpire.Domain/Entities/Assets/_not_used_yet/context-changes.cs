using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AscendantEmpire.Domain.Entities.Assets._not_used_yet;

internal class context_changes
{
    /*    To use the dictionary json in tables
     *    
     *            //var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.General);

            //var dictionaryConverter = new ValueConverter<Dictionary<string, string>, string>(
            //    v => JsonSerializer.Serialize(v, jsonOptions),
            //    v => string.IsNullOrWhiteSpace(v)
            //        ? new Dictionary<string, string>()
            //        : JsonSerializer.Deserialize<Dictionary<string, string>>(v, jsonOptions) ?? new Dictionary<string, string>());

            //var dictionaryComparer = new ValueComparer<Dictionary<string, string>>(
            //    (a, b) => JsonSerializer.Serialize(a ?? new Dictionary<string, string>(), jsonOptions) ==
            //              JsonSerializer.Serialize(b ?? new Dictionary<string, string>(), jsonOptions),
            //    v => JsonSerializer.Serialize(v ?? new Dictionary<string, string>(), jsonOptions).GetHashCode(),
            //    v => v == null ? new Dictionary<string, string>() : v.ToDictionary(k => k.Key, k => k.Value));

                //entity.Property(x => x.Properties)
            //    .HasConversion(dictionaryConverter)
            //    .Metadata.SetValueComparer(dictionaryComparer);


     *    
     *    
     *    
     *    
     *    
     *    
     *    modelBuilder.Entity<FactionAssetState>(entity =>
            {
                entity.ToTable("FactionAssetState", schema);
                entity.HasKey(x => new { x.FactionId, x.AssetId });

                entity.HasIndex(x => new { x.FactionId, x.AvailabilityState });
                entity.HasIndex(x => new { x.FactionId, x.IsCurrentlyLuxury });

                entity.HasOne(x => x.Asset)
                    .WithMany(x => x.FactionStates)
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.NoAction);
    });

            modelBuilder.Entity<FactionAssetPriority>(entity =>
            {
                entity.ToTable("FactionAssetPriority", schema);
                entity.HasKey(x => new { x.FactionId, x.AssetCategoryId, x.AssetId });

                entity.HasIndex(x => new { x.FactionId, x.AssetCategoryId, x.PriorityOrder }).IsUnique();

    entity.HasOne(x => x.Category)
                    .WithMany(x => x.FactionPriorities)
                    .HasForeignKey(x => x.AssetCategoryId)
                    .OnDelete(DeleteBehavior.NoAction);

    entity.HasOne(x => x.Asset)
                    .WithMany(x => x.FactionPriorities)
                    .HasForeignKey(x => x.AssetId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

    modelBuilder.Entity<AssetBank>(entity =>
    {
        entity.ToTable("AssetBank", schema);
        entity.HasKey(x => x.Id);

        entity.HasIndex(x => new { x.FactionId, x.Kind });
        entity.HasIndex(x => x.NetworkKey);
    });

    modelBuilder.Entity<AssetBankBalance>(entity =>
    {
        entity.ToTable("AssetBankBalance", schema);
        entity.HasKey(x => new { x.AssetBankId, x.AssetId });

        entity.Property(x => x.Quantity).IsRequired();

        entity.HasOne(x => x.Bank)
            .WithMany(x => x.Balances)
            .HasForeignKey(x => x.AssetBankId)
            .OnDelete(DeleteBehavior.NoAction);

        entity.HasOne(x => x.Asset)
            .WithMany(x => x.BankBalances)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.NoAction);
    });

    modelBuilder.Entity<AssetBankSettlementMembership>(entity =>
    {
        entity.ToTable("AssetBankSettlementMembership", schema);
        entity.HasKey(x => x.SettlementId);

        entity.HasIndex(x => x.AssetBankId);

        entity.HasOne(x => x.Bank)
            .WithMany(x => x.SettlementMemberships)
            .HasForeignKey(x => x.AssetBankId)
            .OnDelete(DeleteBehavior.NoAction);
    });




    modelBuilder.Entity<WorldAssetSnapshot>(entity =>
    {
        entity.ToTable("WorldAssetSnapshot", schema);
        entity.HasKey(x => x.Id);

        entity.Property(x => x.RelativeValue).HasPrecision(18, 6);

        entity.HasIndex(x => new { x.TurnNumber, x.AssetId }).IsUnique();

        entity.HasOne(x => x.Asset)
            .WithMany(x => x.WorldSnapshots)
            .HasForeignKey(x => x.AssetId)
            .OnDelete(DeleteBehavior.NoAction);
    });*/
}
