using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Contracts;
using RaceDay.Api.Models;
using RaceDay.Api.Tests.Infrastructure;

namespace RaceDay.Api.Tests;

/// <summary>Participants enrol in an event by category; the enrolment is saved and visible to the right people.</summary>
public class EnrolmentTests(RaceDayWebApplicationFactory factory) : IClassFixture<RaceDayWebApplicationFactory>
{
    [Fact]
    public async Task Participant_EnrolsInCategory_Returns201_AndTheEnrolmentIsRecorded()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId, "Senior", 20, 39);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory, new DateOnly(1995, 1, 1));

        var response = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(response);
        Assert.Equal(eventId, enrolment!.EventId);
        Assert.Equal(categoryId, enrolment.CategoryId);
        Assert.Equal(EnrolmentStatuses.Pending, enrolment.Status);

        var stored = await factory.WithDbAsync(db => db.Enrolments.SingleAsync(en => en.EnrolmentId == enrolment.EnrolmentId));
        Assert.Equal(eventId, stored.EventId);
        Assert.Equal(categoryId, stored.CategoryId);
    }

    [Fact]
    public async Task Participant_EnrolsTwiceInTheSameEvent_Returns400()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);
        (await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId))).EnsureSuccessStatusCode();

        var second = await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Contains("already enrolled", (await ApiClientHelpers.ReadErrorAsync(second))!.Message);
    }

    [Fact]
    public async Task Participant_EnrolsWithCategoryFromAnotherEvent_Returns400()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventA = await ApiClientHelpers.CreateEventAsync(organiser, EventTypes.Run, "Event A");
        var eventB = await ApiClientHelpers.CreateEventAsync(organiser, EventTypes.Run, "Event B");
        var categoryOfB = await ApiClientHelpers.CreateCategoryAsync(organiser, eventB);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);

        var response = await participant.PostAsJsonAsync($"/api/events/{eventA}/enrol", new EnrolRequest(categoryOfB));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Participant_OutsideTheCategoryAgeRange_Returns400()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var seniorCategory = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId, "Senior", 20, 39);
        // Born in 2010, so the participant is 17 on the event date (2027-06-01), which is outside the 20 to 39 range.
        var youngParticipant = await ApiClientHelpers.ParticipantClientAsync(factory, new DateOnly(2010, 1, 1));

        var response = await youngParticipant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(seniorCategory));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("outside", (await ApiClientHelpers.ReadErrorAsync(response))!.Message);
    }

    [Fact]
    public async Task Organiser_ViewsEnrolmentsForTheirEvent_IncludingTheParticipant()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);
        var enrolled = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(
            await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId)));

        var response = await organiser.GetAsync($"/api/events/{eventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await ApiClientHelpers.ReadAsync<List<EnrolmentResponse>>(response);
        Assert.Contains(list!, en => en.EnrolmentId == enrolled!.EnrolmentId);
    }

    [Fact]
    public async Task Participant_ListsOnlyTheirOwnEnrolments()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);
        var first = await ApiClientHelpers.ParticipantClientAsync(factory);
        var second = await ApiClientHelpers.ParticipantClientAsync(factory);
        var firstEnrolment = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(
            await first.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId)));
        var secondEnrolment = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(
            await second.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId)));

        var list = await ApiClientHelpers.ReadAsync<List<EnrolmentResponse>>(await first.GetAsync("/api/enrolments"));

        Assert.Contains(list!, en => en.EnrolmentId == firstEnrolment!.EnrolmentId);
        Assert.DoesNotContain(list!, en => en.EnrolmentId == secondEnrolment!.EnrolmentId);
    }

    [Fact]
    public async Task Organiser_ConfirmsAnEnrolment_Returns200_AndTheStatusChanges()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);
        var enrolment = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(
            await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId)));

        var response = await organiser.PutAsJsonAsync(
            $"/api/enrolments/{enrolment!.EnrolmentId}/status",
            new UpdateEnrolmentStatusRequest(EnrolmentStatuses.Confirmed));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(response);
        Assert.Equal(EnrolmentStatuses.Confirmed, updated!.Status);
    }

    [Fact]
    public async Task Organiser_SetsAnUnknownEnrolmentStatus_Returns400()
    {
        var organiser = await ApiClientHelpers.OrganiserClientAsync(factory);
        var eventId = await ApiClientHelpers.CreateEventAsync(organiser);
        var categoryId = await ApiClientHelpers.CreateCategoryAsync(organiser, eventId);
        var participant = await ApiClientHelpers.ParticipantClientAsync(factory);
        var enrolment = await ApiClientHelpers.ReadAsync<EnrolmentResponse>(
            await participant.PostAsJsonAsync($"/api/events/{eventId}/enrol", new EnrolRequest(categoryId)));

        var response = await organiser.PutAsJsonAsync(
            $"/api/enrolments/{enrolment!.EnrolmentId}/status",
            new UpdateEnrolmentStatusRequest("Maybe"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
