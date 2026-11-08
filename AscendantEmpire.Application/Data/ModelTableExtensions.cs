using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AscendantEmpire.Application.Data;

/// <summary>
/// Reads an entity's table out of the EF model, for the write paths that have to name a table outside EF.
/// </summary>
/// <remarks>
/// <c>SqlBulkCopy</c> takes its destination as text, which is otherwise a literal nothing ties to the model: renaming a table or moving it between schemas
/// would leave every literal compiling and failing at run time.
/// </remarks>
internal static class ModelTableExtensions
{
    /// <summary>
    /// The entity's table, bracketed and schema-qualified, in the form a bulk copy's destination takes.
    /// </summary>
    /// <exception cref="InvalidOperationException">The type is not a mapped entity, or maps to no table.</exception>
    internal static string SchemaQualifiedTableName<TEntity>(this DbContext context) where TEntity : class
    {
        IEntityType entity = context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not a mapped entity, so it has no table to bulk-copy into.");

        string table = entity.GetTableName()
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} maps to no table, so it cannot be bulk-copied into.");

        // A table configured with no schema goes to the connection's default, which is what leaving the qualifier off means.
        string? schema = entity.GetSchema();

        return schema == null ? $"[{table}]" : $"[{schema}].[{table}]";
    }
}
