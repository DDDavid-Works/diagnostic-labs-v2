using DiagnosticLabs.Application.Schema;
using DiagnosticLabs.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Tests.Persistence;

public class SchemaVersionTests
{
    private static readonly string[] Known = ["001_A", "002_B", "003_C"];

    [Fact]
    public void Same_migrations_are_current()
    {
        var result = SchemaCheck.Evaluate(Known, ["003_C", "001_A", "002_B"]);

        Assert.True(result.IsCurrent);
    }

    [Fact]
    public void Missing_migrations_mean_the_database_is_behind()
    {
        var result = SchemaCheck.Evaluate(Known, ["001_A"]);

        Assert.Equal(SchemaStatus.DatabaseBehind, result.Status);
        Assert.Contains("002_B", result.Detail);
        Assert.Contains("003_C", result.Detail);
        Assert.DoesNotContain("not been set up", result.Message);
    }

    [Fact]
    public void A_database_with_no_migrations_is_not_set_up()
    {
        var result = SchemaCheck.Evaluate(Known, []);

        Assert.Equal(SchemaStatus.DatabaseBehind, result.Status);
        Assert.Contains("not been set up", result.Message);
    }

    [Fact]
    public void Unknown_migrations_mean_the_program_is_older()
    {
        var result = SchemaCheck.Evaluate(Known, ["001_A", "002_B", "003_C", "004_D"]);

        Assert.Equal(SchemaStatus.DatabaseAhead, result.Status);
        Assert.Contains("004_D", result.Detail);
    }

    [Fact]
    public void Program_older_wins_when_both_differences_exist()
    {
        var result = SchemaCheck.Evaluate(Known, ["001_A", "004_D"]);

        Assert.Equal(SchemaStatus.DatabaseAhead, result.Status);
    }

    [Fact]
    public async Task An_unreachable_server_is_reported_not_thrown()
    {
        // Nothing listens on this port; a short timeout keeps the test quick.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=x;User Id=u;Password=p;Connect Timeout=2;TrustServerCertificate=True")
            .Options;
        await using var db = new AppDbContext(options);

        var result = await new SchemaVersionService(db).CheckAsync();

        Assert.Equal(SchemaStatus.Unreachable, result.Status);
        Assert.False(result.IsCurrent);
    }

    [Fact]
    public void The_program_knows_its_migrations()
    {
        // Guards against a build that ships without the migration assembly, which would read as "database ahead".
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer("Server=.;Database=x").Options;
        using var db = new AppDbContext(options);

        Assert.NotEmpty(db.Database.GetMigrations());
    }
}
