using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Contracts;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>Registration, login, session and logout behaviour.</summary>
public class AuthTests(RaceDayWebApplicationFactory factory) : IClassFixture<RaceDayWebApplicationFactory>
{
    [Fact]
    public async Task Register_Organiser_Returns201_AndStoresOnlyAHashedPassword()
    {
        var client = factory.CreateClient();
        var email = ApiClientHelpers.UniqueEmail("hash-check");

        var response = await ApiClientHelpers.RegisterOrganiserRequestAsync(client, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ApiClientHelpers.ReadAsync<AuthResponse>(response);
        Assert.Equal(Roles.Organiser, body!.Role);

        var stored = await factory.WithDbAsync(db => db.Users.Include(u => u.Organiser).SingleAsync(u => u.Email == email));
        Assert.NotEqual(ApiClientHelpers.Password, stored.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.EnhancedVerify(ApiClientHelpers.Password, stored.PasswordHash));
        Assert.NotNull(stored.Organiser);
        Assert.Equal("Test Events", stored.Organiser!.OrganisationName);
    }

    [Fact]
    public async Task Register_Participant_Returns201_AndCreatesParticipantProfile()
    {
        var client = factory.CreateClient();
        var email = ApiClientHelpers.UniqueEmail("participant-register");

        var response = await ApiClientHelpers.RegisterParticipantRequestAsync(client, email, new DateOnly(1990, 5, 15));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var stored = await factory.WithDbAsync(db => db.Users.Include(u => u.Participant).SingleAsync(u => u.Email == email));
        Assert.Equal(Roles.Participant, stored.Role);
        Assert.Equal(new DateOnly(1990, 5, 15), stored.Participant!.DateOfBirth);
    }

    [Fact]
    public async Task Register_WithExistingEmail_Returns409()
    {
        var client = factory.CreateClient();
        var email = ApiClientHelpers.UniqueEmail("duplicate");
        (await ApiClientHelpers.RegisterOrganiserRequestAsync(client, email)).EnsureSuccessStatusCode();

        var response = await ApiClientHelpers.RegisterOrganiserRequestAsync(factory.CreateClient(), email);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithUnknownRole_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            ApiClientHelpers.UniqueEmail("admin"), ApiClientHelpers.Password, "Ada Admin", "Administrator", null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_ParticipantWithoutDateOfBirth_Returns400()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            ApiClientHelpers.UniqueEmail("no-dob"), ApiClientHelpers.Password, "Nia Nodob", Roles.Participant, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ApiClientHelpers.ReadErrorAsync(response);
        Assert.Contains("Date of birth", error!.Message);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_Returns200_AndSetsSessionCookie()
    {
        var client = factory.CreateClient();
        var email = ApiClientHelpers.UniqueEmail("login-ok");
        (await ApiClientHelpers.RegisterParticipantRequestAsync(client, email)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, ApiClientHelpers.Password));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ApiClientHelpers.ReadAsync<AuthResponse>(response);
        Assert.Equal(Roles.Participant, body!.Role);
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, cookie => cookie.StartsWith("RaceDay.Session=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = factory.CreateClient();
        var email = ApiClientHelpers.UniqueEmail("login-bad");
        (await ApiClientHelpers.RegisterOrganiserRequestAsync(client, email)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401_WithSameMessageAsWrongPassword()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            ApiClientHelpers.UniqueEmail("nobody"), ApiClientHelpers.Password));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Invalid email or password.", (await ApiClientHelpers.ReadErrorAsync(response))!.Message);
    }

    [Fact]
    public async Task Profile_WithoutLogin_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/users/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Profile_AfterLogin_ReturnsTheLoggedInUsersProfile()
    {
        var client = await ApiClientHelpers.OrganiserClientAsync(factory);

        var response = await client.GetAsync("/api/users/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await ApiClientHelpers.ReadAsync<UserProfileResponse>(response);
        Assert.Equal(Roles.Organiser, profile!.Role);
        Assert.Equal("Test Events", profile.OrganisationName);
    }

    [Fact]
    public async Task Logout_EndsTheSession_SoProtectedEndpointsReturn401Again()
    {
        var client = await ApiClientHelpers.ParticipantClientAsync(factory);
        (await client.GetAsync("/api/users/profile")).EnsureSuccessStatusCode();

        var logout = await client.PostAsync("/api/auth/logout", null);
        var afterLogout = await client.GetAsync("/api/users/profile");

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }
}
