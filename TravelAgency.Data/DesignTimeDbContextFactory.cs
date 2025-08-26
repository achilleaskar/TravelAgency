using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;


namespace TravelAgency.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TravelAgencyDbContext>
{
    public TravelAgencyDbContext CreateDbContext(string[] args)
    {
        var cfg = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var cs = cfg.GetConnectionString("MySql")
                 ?? "server=localhost;user=root;password=1234;database=travelagency;";

        var opts = new DbContextOptionsBuilder<TravelAgencyDbContext>()
            .UseMySql(cs, ServerVersion.AutoDetect(cs))
            .Options;

        return new TravelAgencyDbContext(opts);
    }
}