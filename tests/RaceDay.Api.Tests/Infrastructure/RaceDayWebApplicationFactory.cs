using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;

namespace RaceDay.Api.Tests.Infrastructure;

/// <summary>
/// Runs the real API in memory for tests. The "Testing" environment skips SQL Server and migrations, and each
/// factory instance gets its own in-memory database, so test classes do not share data.
/// </summary>
public class RaceDayWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"RaceDayTests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    /// <summary>Runs a database action against the same in-memory store the API uses, for checking what was saved.</summary>
    public async Task<T> WithDbAsync<T>(Func<RaceDayDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        return await action(db);
    }
}
