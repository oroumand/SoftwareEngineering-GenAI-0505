# Technical Plan

## Technical context

- .NET 10 / ASP.NET Core Minimal API
- Onion Architecture
- CQRS with the official `MediatR` 12.5 package (Apache-2.0; selected to avoid
  commercial-license runtime noise in an educational demo)
- in-memory Repository adapter for deterministic live demo
- xUnit tests
- Problem Details error responses

## Project structure

```text
src/
  UserProfiles.Domain/
  UserProfiles.Application/
  UserProfiles.Infrastructure/
  UserProfiles.Api/
tests/
  UserProfiles.Tests/
```

## Dependency direction

```text
UserProfiles.Api
  -> UserProfiles.Infrastructure
  -> UserProfiles.Application
  -> UserProfiles.Domain
```

API also references Application to send MediatR requests. Infrastructure references Application to implement ports. No inner layer references an outer layer.

## Domain design

`UserProfile.Create` owns normalization and invariants. It receives `today` and `createdAtUtc`; hidden time access is prohibited. Invalid state cannot be constructed through the public API.

## Application design

- `CreateUserProfileCommand` changes state.
- `GetUserProfileByIdQuery` and `ListUserProfilesQuery` read state.
- handlers use `IUserProfileRepository` and `IClock`.
- DTO mapping occurs in Application.
- duplicate email is an application conflict because it requires repository state.

## Infrastructure design

`InMemoryUserProfileRepository` is a singleton adapter. A lock makes uniqueness check plus insertion atomic inside the process. `SystemClock` supplies UTC time.

## API design

Endpoints map request/response only and call `ISender`. Middleware maps known exceptions to safe Problem Details. API never resolves the repository directly.

## Test strategy

- domain invariants and normalization;
- duplicate email command behavior;
- repository case-insensitive uniqueness;
- command/query flow;
- reflection-based architecture reference checks.

## Rejected alternatives

- EF Core/database: not required by the intent and makes a short live demo dependent on external state.
- hand-written mediator: violates the explicit MediatR requirement.
- generic repository: adds abstraction without a second aggregate or demonstrated reuse.
- validation in endpoint: couples business rules to HTTP and weakens reuse.
