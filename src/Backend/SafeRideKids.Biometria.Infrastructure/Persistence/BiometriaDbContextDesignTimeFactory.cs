using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SafeRideKids.Biometria.Infrastructure.Persistence;

/// <summary>
/// Factory de design-time para permitir 'dotnet ef migrations add' sem depender de Program.cs.
/// Usa BIOMETRIA_PG_CONNSTRING quando setada; caso contrário, fallback localhost.
/// </summary>
public sealed class BiometriaDbContextDesignTimeFactory : IDesignTimeDbContextFactory<BiometriaDbContext>
{
    public BiometriaDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("BIOMETRIA_PG_CONNSTRING")
                   ?? "Host=localhost;Database=saferidekids_biometria_design;Username=postgres;Password=postgres";
        var opts = new DbContextOptionsBuilder<BiometriaDbContext>()
            .UseNpgsql(conn, npgsql =>
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory", BiometriaDbContext.SchemaName))
            .Options;
        return new BiometriaDbContext(opts);
    }
}
