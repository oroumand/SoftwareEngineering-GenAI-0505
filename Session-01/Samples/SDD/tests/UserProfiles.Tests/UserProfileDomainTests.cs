using UserProfiles.Domain;

namespace UserProfiles.Tests;

public sealed class UserProfileDomainTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 25, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NormalizesAcceptedFields()
    {
        var profile = UserProfile.Create(
            "  Sara ",
            " Ahmadi  ",
            "SARA@Example.com",
            "+989121234567",
            new DateOnly(1998, 5, 10),
            "ir",
            DateOnly.FromDateTime(Now.UtcDateTime),
            Now);

        Assert.Equal("Sara", profile.FirstName);
        Assert.Equal("Ahmadi", profile.LastName);
        Assert.Equal("sara@example.com", profile.Email);
        Assert.Equal("IR", profile.Country);
        Assert.Equal(Now, profile.CreatedAtUtc);
    }

    [Fact]
    public void Create_RejectsInvalidPhoneUnderageUserAndCountry()
    {
        var exception = Assert.Throws<DomainValidationException>(() =>
            UserProfile.Create(
                "Ali",
                "Young",
                "ali@example.com",
                "09120000000",
                new DateOnly(2020, 1, 1),
                "Iran",
                DateOnly.FromDateTime(Now.UtcDateTime),
                Now));

        Assert.Contains(nameof(UserProfile.PhoneNumber), exception.Errors.Keys);
        Assert.Contains(nameof(UserProfile.DateOfBirth), exception.Errors.Keys);
        Assert.Contains(nameof(UserProfile.Country), exception.Errors.Keys);
    }
}

