using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Contracts;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>Result capture by the Organiser, participant result history, and category rules.</summary>
public class ResultsAndCategoriesTests(RaceDayWebApplicationFactory factory) : IClassFixture<RaceDayWebApplicationFactory>
{
    [Fact]
    public async Task Organiser_CapturesAResult_AndTheParticipantSeesItWithTheFieldSize()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);
        var enrolment = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(
            await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId)));

        var capture = await organiser.PostAsJsonAsync(
            $"/api/events/{eventId}/results",
            new CaptureResultRequest(enrolment!.EnrolmentId, new TimeSpan(3, 45, 30), 47));

        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);

        var mine = await ApiClientHelpers.ReadAsync<List<ResultResponse>>(await participant.GetAsync("/api/results/me"));
        var result = Assert.Single(mine!, r => r.EnrolmentId == enrolment.EnrolmentId);
        Assert.Equal(new TimeSpan(3, 45, 30), result.FinishTime);
        Assert.Equal(47, result.FinishPosition);
        Assert.Equal(1, result.TotalFinishers);
    }

    [Fact]
    public async Task Organiser_CapturesTwoResultsForTheSameEnrolment_Returns400()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);
        var enrolment = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(
            await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId)));
        var request = new CaptureResultRequest(enrolment!.EnrolmentId, new TimeSpan(2, 0, 0), 5);
        (await organiser.PostAsJsonAsync($"/api/events/{eventId}/results", request)).EnsureSuccessStatusCode();

        var second = await organiser.PostAsJsonAsync($"/api/events/{eventId}/results", request);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Organiser_CapturesAPositionBelowOne_Returns400()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);
        var enrolment = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(
            await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId)));

        var response = await organiser.PostAsJsonAsync(
            $"/api/events/{eventId}/results",
            new CaptureResultRequest(enrolment!.EnrolmentId, new TimeSpan(2, 0, 0), 0));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CorrectsAResult_Returns200_WithTheNewValues()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);
        var enrolment = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(
            await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId)));
        var created = await ApiClientHelpers.ReadAsync<ResultResponse>(await organiser.PostAsJsonAsync(
            $"/api/events/{eventId}/results",
            new CaptureResultRequest(enrolment!.EnrolmentId, new TimeSpan(2, 0, 0), 9)));

        var response = await organiser.PutAsJsonAsync(
            $"/api/results/{created!.ResultId}",
            new UpdateResultRequest(new TimeSpan(1, 59, 59), 3));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ApiClientHelpers.ReadAsync<ResultResponse>(response);
        Assert.Equal(3, updated!.FinishPosition);
        Assert.Equal(new TimeSpan(1, 59, 59), updated.FinishTime);
    }

    [Fact]
    public async Task Organiser_AddsACategory_Returns201()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync(
            $"/api/events/{eventId}/categories",
            new CategoryRequest("Veteran", "40 and over", 40, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var category = await ApiClientHelpers.ReadAsync<CategoryResponse>(response);
        Assert.Equal("Veteran", category!.Name);
        Assert.Null(category.MaxAge);
    }

    [Fact]
    public async Task Organiser_AddsACategoryWithMinAgeAboveMaxAge_Returns400()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);

        var response = await organiser.PostAsJsonAsync(
            $"/api/events/{eventId}/categories",
            new CategoryRequest("Backwards", null, 40, 20));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCategory_WithParticipantEnrolments_Returns409()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);
        (await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId))).EnsureSuccessStatusCode();

        var response = await organiser.DeleteAsync($"/api/categories/{categoryId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_DocumentListsTheAuthAndEventEndpoints()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"/api/auth/register\"", json);
        Assert.Contains("\"/api/auth/login\"", json);
        Assert.Contains("\"/api/events/{eventId}/enrol\"", json);
        Assert.Contains("\"/api/results/me\"", json);
    }
}
