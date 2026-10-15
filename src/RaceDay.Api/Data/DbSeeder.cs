using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Data;

/// <summary>
/// Adds the sample data from the Part 1 SQL script (2 organisers, 3 participants, 3 events, 9 categories,
/// 6 enrolments, 4 results) when the database is empty. Unlike the SQL script, the passwords here are real
/// BCrypt hashes, so every seeded account can log in with <see cref="SeedPassword"/>.
/// </summary>
public static class DbSeeder
{
    public const string SeedPassword = "Password123!";

    public static async Task SeedAsync(RaceDayDbContext db, IPasswordService passwords, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var hash = passwords.HashPassword(SeedPassword);
        var now = DateTime.Now;

        // Accounts and profiles, created in the same order as the SQL script's IDs.
        var organiser1 = new User { Email = "organiser1@raceday.com", FullName = "John Organiser", Role = Roles.Organiser, PasswordHash = hash, CreatedAt = now };
        var organiser2 = new User { Email = "organiser2@raceday.com", FullName = "Sarah Events", Role = Roles.Organiser, PasswordHash = hash, CreatedAt = now };
        var participant1 = new User { Email = "participant1@raceday.com", FullName = "Tom Runner", Role = Roles.Participant, PasswordHash = hash, CreatedAt = now };
        var participant2 = new User { Email = "participant2@raceday.com", FullName = "Lisa Walker", Role = Roles.Participant, PasswordHash = hash, CreatedAt = now };
        var participant3 = new User { Email = "participant3@raceday.com", FullName = "Mike Cyclist", Role = Roles.Participant, PasswordHash = hash, CreatedAt = now };

        organiser1.Organiser = new Organiser { OrganisationName = "Cape Town Events", ContactNumber = "+27 82 123 4567" };
        organiser2.Organiser = new Organiser { OrganisationName = "Durban Sports", ContactNumber = "+27 83 987 6543" };
        participant1.Participant = new Participant { DateOfBirth = new DateOnly(1990, 5, 15) };
        participant2.Participant = new Participant { DateOfBirth = new DateOnly(1985, 10, 20) };
        participant3.Participant = new Participant { DateOfBirth = new DateOnly(1995, 3, 8) };

        db.Users.AddRange(organiser1, organiser2, participant1, participant2, participant3);

        // Three events, each with its own categories.
        var cycleTour = new Event
        {
            Organiser = organiser1.Organiser!,
            Name = "Cape Town Cycle Tour",
            Description = "Annual cycling event through Cape Town",
            EventDate = new DateTime(2026, 3, 15, 7, 0, 0),
            Location = "Cape Town, Western Cape",
            Distance = 109.00m,
            EventType = EventTypes.Cycle,
            CreatedAt = now
        };
        var marathon = new Event
        {
            Organiser = organiser1.Organiser!,
            Name = "Soweto Marathon",
            Description = "Iconic marathon through Soweto",
            EventDate = new DateTime(2026, 4, 20, 6, 30, 0),
            Location = "Soweto, Gauteng",
            Distance = 42.20m,
            EventType = EventTypes.Run,
            CreatedAt = now
        };
        var walkathon = new Event
        {
            Organiser = organiser2.Organiser!,
            Name = "Durban Walkathon",
            Description = "Scenic coastal walk for charity",
            EventDate = new DateTime(2026, 5, 10, 8, 0, 0),
            Location = "Durban, KwaZulu-Natal",
            Distance = 21.10m,
            EventType = EventTypes.Walk,
            CreatedAt = now
        };

        var cycleUnder20 = NewCategory(cycleTour, "Under 20", "Cyclists under 20 years old", 15, 19);
        var cycleSenior = NewCategory(cycleTour, "Senior", "Cyclists 20-39 years old", 20, 39);
        var cycleVeteran = NewCategory(cycleTour, "Veteran", "Cyclists 40+ years old", 40, null);
        var runUnder20 = NewCategory(marathon, "Under 20", "Runners under 20", 15, 19);
        var runSenior = NewCategory(marathon, "Senior", "Runners 20-39 years old", 20, 39);
        var runVeteran = NewCategory(marathon, "Veteran", "Runners 40+ years old", 40, null);
        var walkUnder20 = NewCategory(walkathon, "Under 20", "Walkers under 20", 15, 19);
        var walkSenior = NewCategory(walkathon, "Senior", "Walkers 20-39 years old", 20, 39);
        var walkVeteran = NewCategory(walkathon, "Veteran", "Walkers 40+ years old", 40, null);

        db.Events.AddRange(cycleTour, marathon, walkathon);
        db.Categories.AddRange(
            cycleUnder20, cycleSenior, cycleVeteran,
            runUnder20, runSenior, runVeteran,
            walkUnder20, walkSenior, walkVeteran);

        // Enrolments in the same order as the SQL script, so IDs 1 to 6 match.
        var enrolments = new[]
        {
            Enrol(participant1.Participant!, cycleTour, cycleSenior, EnrolmentStatuses.Confirmed, now),
            Enrol(participant1.Participant!, marathon, runSenior, EnrolmentStatuses.Pending, now),
            Enrol(participant2.Participant!, cycleTour, cycleVeteran, EnrolmentStatuses.Confirmed, now),
            Enrol(participant2.Participant!, walkathon, walkSenior, EnrolmentStatuses.Confirmed, now),
            Enrol(participant3.Participant!, cycleTour, cycleUnder20, EnrolmentStatuses.Pending, now),
            Enrol(participant3.Participant!, walkathon, walkVeteran, EnrolmentStatuses.Confirmed, now)
        };
        db.Enrolments.AddRange(enrolments);

        // Results for enrolments 1, 3, 4 and 6, same as the SQL script.
        db.Results.AddRange(
            new Result { Enrolment = enrolments[0], FinishTime = new TimeSpan(3, 45, 30), FinishPosition = 47, Status = ResultStatuses.Published },
            new Result { Enrolment = enrolments[2], FinishTime = new TimeSpan(4, 12, 15), FinishPosition = 78, Status = ResultStatuses.Published },
            new Result { Enrolment = enrolments[3], FinishTime = new TimeSpan(2, 30, 45), FinishPosition = 12, Status = ResultStatuses.Published },
            new Result { Enrolment = enrolments[5], FinishTime = new TimeSpan(3, 55, 20), FinishPosition = 34, Status = ResultStatuses.Published });

        await db.SaveChangesAsync(ct);
    }

    private static Category NewCategory(Event ev, string name, string description, int minAge, int? maxAge) =>
        new() { Event = ev, Name = name, Description = description, MinAge = minAge, MaxAge = maxAge };

    private static Enrolment Enrol(Participant participant, Event ev, Category category, string status, DateTime now) =>
        new()
        {
            Participant = participant,
            Event = ev,
            Category = category,
            Status = status,
            RegistrationDate = now
        };
}
