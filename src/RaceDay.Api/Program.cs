using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// SQL Server through EF Core. The connection string lives in appsettings.json under ConnectionStrings:RaceDayDb.
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));

var app = builder.Build();

app.UseHttpsRedirection();

app.MapControllers();

// Apply the Code-First migrations at startup. The test host sets the "Testing" environment and uses an in-memory database instead.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<RaceDayDbContext>().Database.MigrateAsync();
}

app.Run();

// Makes the entry point visible to WebApplicationFactory in the test project.
public partial class Program;
