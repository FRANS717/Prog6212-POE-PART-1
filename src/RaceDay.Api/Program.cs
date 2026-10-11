using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// SQL Server through EF Core. The connection string lives in appsettings.json under ConnectionStrings:RaceDayDb.
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));

builder.Services.AddScoped<IPasswordService, PasswordService>();

// Server-side session: the UserId and Role are stored on the server and identified by an HttpOnly cookie.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

var app = builder.Build();

app.UseHttpsRedirection();
app.UseSession();

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
