using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ScoreCode.Database;

public class ScoreCodeDbFactory : IDesignTimeDbContextFactory<ScoreCodeDb>
{
    public ScoreCodeDb CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ScoreCodeDb>()
            .UseNpgsql("Host=localhost;Port=5432;Database=scorecode;Username=sc;Password=sc_password")
            .Options;

        return new ScoreCodeDb(options);
    }
}
