using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Contracts;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>Organisers manage their own events; anyone can browse them.</summary>
public class EventManagementTests(RaceDayWebApplicationFactory factory) : IClassFixture<RaceDayWebApplicationFactory>
{
    [Fact]
    public async Task Organiser_CreatesEvent_Returns201_WithTheSubmittedDetails()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);

        var response = await organiser.PostAsJsonAsync("/api/events", ApiClientHelpers.NewEventRequest("Table Mountain Trail", EventTypes.Walk));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ApiClientHelpers.ReadAsync<EventDetailResponse>(response);
        Assert.Equal("Table Mountain Trail", created!.Name);
        Assert.Equal(EventTypes.Walk, created.EventType);
        Assert.Equal(21.10m, created.Distance);
        Assert.Empty(created.Categories);
    }

    [Fact]
    public async Task Organiser_UpdatesOwnEvent_Returns200()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser, EventTypes.Run, "Original Name");

        var response = await organiser.PutAsJsonAsync($"/api/events/{eventId}", ApiClientHelpers.NewEventRequest("Renamed Run", EventTypes.Cycle));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ApiClientHelpers.ReadAsync<EventDetailResponse>(response);
        Assert.Equal("Renamed Run", updated!.Name);
        Assert.Equal(EventTypes.Cycle, updated.EventType);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var owner = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(owner);
        var intruder = await ApiClientHelpers.OrganiserClientAsync(factory);

        var response = await intruder.PutAsJsonAsync($"/api/events/{eventId}", ApiClientHelpers.NewEventRequest("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_DeletesOwnEvent_Returns204_AndTheEventIsGone()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);

        var delete = await organiser.DeleteAsync($"/api/events/{eventId}");
        var fetch = await organiser.GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fetch.StatusCode);
        var categoriesLeft = await factory.WithDbAsync(db => db.Categories.CountAsync(c => c.EventId == eventId));
        Assert.Equal(0, categoriesLeft);
    }

    [Fact]
    public async Task Anonymous_CanBrowseEvents_AndSeeTheirCategories()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser, EventTypes.Run, "Public Browse Run");
        await ApiClientHelpers.CreateCategoryAsync(organiser, eventId, "Under 20", 15, 19);
        var anonymous = factory.CreateClient();

        var list = await anonymous.GetAsync("/api/events");
        var detail = await anonymous.GetAsync($"/api/events/{eventId}");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var events = await ApiClientHelpers.ReadAsync<List<EventSummaryResponse>>(list);
        Assert.Contains(events!, e => e.EventId == eventId);

        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await ApiClientHelpers.ReadAsync<EventDetailResponse>(detail);
        Assert.Single(body!.Categories);
        Assert.Equal("Under 20", body.Categories[0].Name);
    }

    [Fact]
    public async Task GetEvents_FilteredByType_OnlyReturnsMatchingEvents()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var cycleId = await ApiClientHelpers.CreateEventAsync(organiser, EventTypes.Cycle, "Filter Cycle");
        var runId = await ApiClientHelpers.CreateEventAsync(organiser, EventTypes.Run, "Filter Run");

        var response = await factory.CreateClient().GetAsync("/api/events?type=Cycle");

        var events = await ApiClientHelpers.ReadAsync<List<EventSummaryResponse>>(response);
        Assert.Contains(events!, e => e.EventId == cycleId);
        Assert.DoesNotContain(events!, e => e.EventId == runId);
        Assert.All(events!, e => Assert.Equal(EventTypes.Cycle, e.EventType));
    }

    [Fact]
    public async Task GetEvents_WithUnknownType_Returns400()
    {
        var response = await factory.CreateClient().GetAsync("/api/events?type=Swim");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEvent_ForUnknownId_Returns404()
    {
        var response = await factory.CreateClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithUnknownEventType_Returns400()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);

        var response = await organiser.PostAsJsonAsync("/api/events", ApiClientHelpers.NewEventRequest("Bad Type", "Swim"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithDistanceOverTheSchemaLimit_Returns400()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var request = ApiClientHelpers.NewEventRequest("Too Long") with { Distance = 1000m };

        var response = await organiser.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
