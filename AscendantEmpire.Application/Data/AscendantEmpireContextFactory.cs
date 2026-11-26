using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AscendantEmpire.Application.Data;

public class AscendantEmpireContextFactory : IDesignTimeDbContextFactory<AscendantEmpireContext>
{
    public AscendantEmpireContext CreateDbContext(string[] args)
    {
        string connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__AscendantEmpireDatabase")
            ?? "Server=CoreOS\\SQLExpress;Database=AscendantEmpire;Trusted_Connection=True;TrustServerCertificate=True;";

        DbContextOptionsBuilder<AscendantEmpireContext> optionsBuilder = new();
        optionsBuilder.UseSqlServer(connectionString);

        return new AscendantEmpireContext(optionsBuilder.Options);
    }
}
