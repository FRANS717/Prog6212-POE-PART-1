using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Api.Contracts;
using RaceDay.Api.Models;

namespace RaceDay.Api.Tests.Infrastructure;

/// <summary>Shared helpers for building test users, logging in, and sending JSON requests.</summary>
public static class ApiClientHelpers
{
    public const string Password = "Password123!";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>A unique email address, so tests never clash on the email UNIQUE rule.</summary>
    public static string UniqueEmail(string prefix) => $"jarred.{prefix}.{Guid.NewGuid():N}@test.raceday";

    /// <summary>Registers an Organiser through the API and returns the response.</summary>
    public static Task<HttpResponseMessage> RegisterOrganiserRequestAsync(HttpClient client, string email, string organisationName = "Jarred Events") =>
        client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, Password, "Jarred Organiser", Roles.Organiser, organisationName, "+27 11 000 0000", null));

    /// <summary>Registers a Participant through the API and returns the response.</summary>
    public static Task<HttpResponseMessage> RegisterParticipantRequestAsync(HttpClient client, string email, DateOnly? dateOfBirth = null) =>
        client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(
            email, Password, "Jarred Participant", Roles.Participant, null, null, dateOfBirth ?? new DateOnly(1995, 1, 1)));

    /// <summary>Creates an Organiser account and logs the client in as that Organiser.</summary>
    public static async Task<HttpClient> OrganiserClientAsync(RaceDayWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail("organiser");
        var registered = await RegisterOrganiserRequestAsync(client, email);
        registered.EnsureSuccessStatusCode();
        await LoginAsync(client, email);
        return client;
    }

    /// <summary>Creates a Participant account and logs the client in as that Participant.</summary>
    public static async Task<HttpClient> ParticipantClientAsync(RaceDayWebApplicationFactory factory, DateOnly? dateOfBirth = null)
    {
        var client = factory.CreateClient();
        var email = UniqueEmail("participant");
        var registered = await RegisterParticipantRequestAsync(client, email, dateOfBirth);
        registered.EnsureSuccessStatusCode();
        await LoginAsync(client, email);
        return client;
    }

    /// <summary>Logs the client in. The session cookie is kept by the test client for later requests.</summary>
    public static async Task LoginAsync(HttpClient client, string email, string password = Password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Creates an event through the API as the logged-in Organiser and returns its ID.</summary>
    public static async Task<int> CreateEventAsync(HttpClient organiserClient, string eventType = EventTypes.Run, string name = "Jarred Run")
    {
        var response = await organiserClient.PostAsJsonAsync("/api/events", NewEventRequest(name, eventType));
        response.EnsureSuccessStatusCode();
        var detail = await ReadAsync<EventDetailResponse>(response);
        return detail!.EventId;
    }

    /// <summary>Adds a category to an event through the API as the logged-in Organiser and returns its ID.</summary>
    public static async Task<int> CreateCategoryAsync(HttpClient organiserClient, int eventId, string name = "Senior", int? minAge = 20, int? maxAge = 39)
    {
        var response = await organiserClient.PostAsJsonAsync(
            $"/api/events/{eventId}/categories",
            new CategoryRequest(name, "Jarred's category", minAge, maxAge));
        response.EnsureSuccessStatusCode();
        var category = await ReadAsync<CategoryResponse>(response);
        return category!.CategoryId;
    }

    public static EventRequest NewEventRequest(string name = "Jarred Run", string eventType = EventTypes.Run) =>
        new(
            name,
            "An event created by the automated tests",
            new DateTime(2027, 6, 1, 7, 0, 0),
            "Cape Town, Western Cape",
            21.10m,
            eventType,
            null);

    public static async Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions);

    public static async Task<ErrorResponse?> ReadErrorAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);

    public static bool IsStatus(HttpResponseMessage response, HttpStatusCode status) => response.StatusCode == status;
}
