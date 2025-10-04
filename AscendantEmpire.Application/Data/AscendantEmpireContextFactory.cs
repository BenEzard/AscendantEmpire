using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AscendantEmpire.Application.Data;

public class AscendantEmpireContextFactory : IDesignTimeDbContextFactory<AscendantEmpireContext>
{
    public AscendantEmpireContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AscendantEmpireContext>();

        // Use your real connection string here
        optionsBuilder.UseSqlServer("Server=CoreOS\\SQLExpress;Database=AscendantEmpire;Trusted_Connection=True;TrustServerCertificate=True;");

        return new AscendantEmpireContext(optionsBuilder.Options);
    }
}
