using System.Text.Json;

using AscendantEmpire.Domain.Entities.Assets;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ResourceConfiguration : IEntityTypeConfiguration<Resource>
{
    public void Configure(EntityTypeBuilder<Resource> builder)
    {
        var comparer = new ValueComparer<Dictionary<string, string>>(
            (d1, d2) => DictionaryEquals(d1, d2),
            d => DictionaryHashCode(d),
            d => DictionaryCopy(d)
        );

        builder.Property(x => x.Properties)
            .HasConversion(
                v => SerializeProperties(v),
                v => DeserializeProperties(v))
            .Metadata.SetValueComparer(comparer);
    }

    private static string SerializeProperties(Dictionary<string, string>? value)
    {
        return JsonSerializer.Serialize(value ?? new Dictionary<string, string>(), new JsonSerializerOptions());
    }

    private static Dictionary<string, string> DeserializeProperties(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(value, new JsonSerializerOptions())
            ?? new Dictionary<string, string>();
    }

    private static bool DictionaryEquals(
        Dictionary<string, string>? left,
        Dictionary<string, string>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var rightValue) || rightValue != pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    private static int DictionaryHashCode(Dictionary<string, string>? value)
    {
        if (value is null || value.Count == 0)
        {
            return 0;
        }

        var hash = new HashCode();

        foreach (var pair in value.OrderBy(x => x.Key))
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value);
        }

        return hash.ToHashCode();
    }

    private static Dictionary<string, string> DictionaryCopy(Dictionary<string, string>? value)
    {
        return value is null
            ? new Dictionary<string, string>()
            : value.ToDictionary(x => x.Key, x => x.Value);
    }
}