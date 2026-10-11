using System.Net.Mail;

namespace RaceDay.Api.Contracts;

/// <summary>Shared input checks used by several controllers. Each returns null when the value is valid, otherwise a message for the client.</summary>
public static class Validation
{
    public const int MinPasswordLength = 8;

    public static string? CheckEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return "Email is required.";
        try
        {
            var address = new MailAddress(email.Trim());
            return address.Address == email.Trim() ? null : "Email is not valid.";
        }
        catch (FormatException)
        {
            return "Email is not valid.";
        }
    }

    public static string? CheckPassword(string? password) =>
        string.IsNullOrEmpty(password) || password.Length < MinPasswordLength
            ? $"Password must be at least {MinPasswordLength} characters."
            : null;

    public static string? CheckRequiredText(string? value, string fieldName, int maxLength) =>
        string.IsNullOrWhiteSpace(value)
            ? $"{fieldName} is required."
            : value.Trim().Length > maxLength
                ? $"{fieldName} must be {maxLength} characters or fewer."
                : null;

    public static string? CheckDateOfBirth(DateOnly? dateOfBirth, DateOnly today)
    {
        if (dateOfBirth is null) return "Date of birth is required for participants.";
        if (dateOfBirth.Value >= today) return "Date of birth must be in the past.";
        if (dateOfBirth.Value < today.AddYears(-120)) return "Date of birth is not valid.";
        return null;
    }
}
