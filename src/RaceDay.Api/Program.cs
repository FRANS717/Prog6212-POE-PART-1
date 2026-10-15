using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Swagger builds its docs from the attributes and XML comments on each endpoint.
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

// The connection string is in appsettings.json under ConnectionStrings:RaceDayDb.
// Tests bring their own in-memory database, so I skip SQL Server when the environment is Testing.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<RaceDayDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("RaceDayDb")));
}

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

// The session lives on the server. The browser only keeps an HttpOnly cookie that points to it.
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

// Serves the uploaded banners and profile pictures from wwwroot/uploads.
app.UseStaticFiles();
app.UseSession();

app.MapControllers();

// On startup I apply the migrations, then seed the sample data if the database is empty.
// Tests run in the Testing environment, so this block is skipped for them.
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, scope.ServiceProvider.GetRequiredService<IPasswordService>());
}

app.Run();

// Lets WebApplicationFactory reach this entry point from the test project.
public partial class Program;
