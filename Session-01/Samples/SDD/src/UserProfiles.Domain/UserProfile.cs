using System.Net.Mail;
using System.Text.RegularExpressions;

namespace UserProfiles.Domain;

public sealed partial class UserProfile
{
    private UserProfile(
        Guid id,
        string firstName,
        string lastName,
        string email,
        string phoneNumber,
        DateOnly dateOfBirth,
        string country,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        PhoneNumber = phoneNumber;
        DateOfBirth = dateOfBirth;
        Country = country;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; }
    public string FirstName { get; }
    public string LastName { get; }
    public string Email { get; }
    public string PhoneNumber { get; }
    public DateOnly DateOfBirth { get; }
    public string Country { get; }
    public DateTimeOffset CreatedAtUtc { get; }

    public static UserProfile Create(
        string? firstName,
        string? lastName,
        string? email,
        string? phoneNumber,
        DateOnly dateOfBirth,
        string? country,
        DateOnly today,
        DateTimeOffset createdAtUtc)
    {
        var normalizedFirstName = firstName?.Trim() ?? string.Empty;
        var normalizedLastName = lastName?.Trim() ?? string.Empty;
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var normalizedPhoneNumber = phoneNumber?.Trim() ?? string.Empty;
        var normalizedCountry = country?.Trim().ToUpperInvariant() ?? string.Empty;

        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        ValidateLength(errors, nameof(FirstName), normalizedFirstName, 2, 50);
        ValidateLength(errors, nameof(LastName), normalizedLastName, 2, 50);

        if (normalizedEmail.Length > 254
            || !MailAddress.TryCreate(normalizedEmail, out var parsedEmail)
            || !string.Equals(parsedEmail.Address, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            errors[nameof(Email)] = ["Email must be a valid address with at most 254 characters."];
        }

        if (!E164PhoneNumber().IsMatch(normalizedPhoneNumber))
        {
            errors[nameof(PhoneNumber)] = ["PhoneNumber must use E.164 format."];
        }

        if (dateOfBirth > today)
        {
            errors[nameof(DateOfBirth)] = ["DateOfBirth cannot be in the future."];
        }
        else if (CalculateAge(dateOfBirth, today) < 13)
        {
            errors[nameof(DateOfBirth)] = ["The user must be at least 13 years old."];
        }

        if (!CountryCode().IsMatch(normalizedCountry))
        {
            errors[nameof(Country)] = ["Country must be a two-letter code."];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(errors);
        }

        return new UserProfile(
            Guid.NewGuid(),
            normalizedFirstName,
            normalizedLastName,
            normalizedEmail,
            normalizedPhoneNumber,
            dateOfBirth,
            normalizedCountry,
            createdAtUtc);
    }

    private static void ValidateLength(
        IDictionary<string, string[]> errors,
        string field,
        string value,
        int minimum,
        int maximum)
    {
        if (value.Length < minimum || value.Length > maximum)
        {
            errors[field] = [$"{field} must contain between {minimum} and {maximum} characters."];
        }
    }

    private static int CalculateAge(DateOnly dateOfBirth, DateOnly today)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age;
    }

    [GeneratedRegex(@"^\+[1-9]\d{7,14}$", RegexOptions.CultureInvariant)]
    private static partial Regex E164PhoneNumber();

    [GeneratedRegex(@"^[A-Z]{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CountryCode();
}

