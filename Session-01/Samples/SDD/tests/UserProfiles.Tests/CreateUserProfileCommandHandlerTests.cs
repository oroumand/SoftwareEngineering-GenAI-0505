using UserProfiles.Application.Commands.CreateUserProfile;
using UserProfiles.Application.Exceptions;
using UserProfiles.Infrastructure;

namespace UserProfiles.Tests;

public sealed class CreateUserProfileCommandHandlerTests
{
    [Fact]
    public async Task Handle_RejectsDuplicateEmailIgnoringCase()
    {
        var repository = new InMemoryUserProfileRepository();
        var clock = new FixedClock(
            new DateTimeOffset(2026, 7, 25, 10, 30, 0, TimeSpan.Zero));
        var handler = new CreateUserProfileCommandHandler(repository, clock);

        await handler.Handle(CreateCommand("sara@example.com"), CancellationToken.None);

        await Assert.ThrowsAsync<DuplicateEmailException>(() =>
            handler.Handle(CreateCommand("SARA@EXAMPLE.COM"), CancellationToken.None));

        Assert.Single(await repository.ListAsync(CancellationToken.None));
    }

    private static CreateUserProfileCommand CreateCommand(string email) =>
        new(
            "Sara",
            "Ahmadi",
            email,
            "+989121234567",
            new DateOnly(1998, 5, 10),
            "IR");
}

