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
     *    modelBuilder.Entity<FactionResourceState>(entity =>
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
    });*/
}
