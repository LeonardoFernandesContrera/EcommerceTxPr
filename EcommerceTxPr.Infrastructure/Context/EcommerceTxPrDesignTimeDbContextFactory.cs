using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EcommerceTxPr.Infrastructure.Context;

public sealed class EcommerceTxPrDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<EcommerceTxPrDbContext>
{
    private const string DesignTimeConnectionString =
        "Server=localhost;Database=EcommerceTxPrDesignTime;" +
        "Integrated Security=True;TrustServerCertificate=True;" +
        "Connect Timeout=1";

    public EcommerceTxPrDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EcommerceTxPrDbContext>()
            .UseSqlServer(DesignTimeConnectionString)
            .Options;

        return new EcommerceTxPrDbContext(options);
    }
}
