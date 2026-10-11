using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Swagger UI: every endpoint, its roles, request body and responses are documented from attributes and XML comments.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "RESTful API for RaceDay, a road running, walking and cycling event platform. " +
                      "Log in with POST /api/auth/login first: the server session cookie is then sent automatically " +
                      "by Swagger UI, so protected endpoints can be tried straight away."
    });
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "RaceDay.Api.xml"));
});

// SQL Server through EF Core. The connection string lives in appsettings.json under ConnectionStrings:RaceDayDb.
// The test host registers its own in-memory database, so SQL Server is not added in the Testing environment.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<RaceDayDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));
}

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

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

app.UseSwagger();
app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1"));

app.UseHttpsRedirection();

// Serves uploaded banner and profile images from wwwroot/uploads.
app.UseStaticFiles();
app.UseSession();

app.MapControllers();

// Apply the Code-First migrations at startup, then add the sample data if the database is empty.
// The test host sets the "Testing" environment and uses an in-memory database instead.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordService>());
}

app.Run();

// Makes the entry point visible to WebApplicationFactory in the test project.
public partial class Program;
