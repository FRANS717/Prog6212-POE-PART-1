using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using RaceDay.Api.Contracts;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>Each role is blocked from the other role's functions, and anonymous users are blocked from protected ones.</summary>
public class RoleAccessTests(RaceDayWebApplicationFactory factory) : IClassFixture<RaceDayWebApplicationFactory>
{
    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);

        var response = await participant.PostAsJsonAsync("/api/events", ApiClientHelpers.NewEventRequest());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrolInAnEvent_Returns403()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);

        var response = await organiser.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewEventEnrolments_Returns403()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);

        var response = await participant.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCaptureResults_Returns403()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);

        var response = await participant.PostAsJsonAsync(
            $"/api/events/{eventId}/results",
            new CaptureResultRequest(1, new TimeSpan(1, 0, 0), 1));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotUploadProfilePicture_Returns403()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);

        var response = await organiser.PostAsync("/api/users/profile/picture", MultipartImage());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCreateEvent_Returns401()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/api/events", ApiClientHelpers.NewEventRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotCreateCategory_Returns401()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);

        var response = await factory.CreateClient().PostAsJsonAsync(
            $"/api/events/{eventId}/categories",
            new CategoryRequest("Anon", null, null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotViewEnrolments_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/enrolments");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>A tiny PNG-signature file wrapped as multipart form data, used where only the role check matters.</summary>
    private static MultipartFormDataContent MultipartImage()
    {
        var content = new MultipartFormDataContent();
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0 };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "image", "banner.png");
        return content;
    }
}
