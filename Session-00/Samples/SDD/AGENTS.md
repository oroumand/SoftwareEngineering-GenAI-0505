# UserProfiles SDD Agent Rules

## Authority

Use this order when information conflicts:

1. accepted Spec and clarification decisions;
2. this repository constitution;
3. accepted technical plan;
4. tasks and traceability;
5. current source code.

Do not silently change a higher-authority artifact to justify local code.

## Required reading

Before implementation, read:

- `.specify/memory/constitution.md`
- `specs/001-user-profile/spec.md`
- `specs/001-user-profile/clarifications.md`
- `specs/001-user-profile/plan.md`
- `specs/001-user-profile/tasks.md`

## Architecture boundaries

- Onion dependency direction is `Api -> Infrastructure -> Application -> Domain`.
- Domain references no other project and contains business invariants.
- Application references only Domain and owns use cases and repository/clock abstractions.
- Infrastructure implements Application abstractions.
- API contains transport mapping and composition only.
- Commands change state; queries do not.
- All commands and queries are dispatched through MediatR.
- Endpoint code must not access repository implementations directly.
- Do not add a generic repository, service locator, static global state or speculative SharedKernel.

## Product rules

- Input has exactly six fields defined by the Spec.
- Email uniqueness is global and case-insensitive.
- Phone number uses E.164.
- Minimum age is 13 and is evaluated through `IClock`.
- Country is an uppercase ISO alpha-2 code.
- API errors are safe and use Problem Details.

## Verification

After code changes:

```powershell
dotnet restore .\UserProfiles.Sdd.slnx
dotnet build .\UserProfiles.Sdd.slnx --no-restore
dotnet test .\UserProfiles.Sdd.slnx --no-build
```

Do not claim completion without command evidence. Do not commit, push, delete, or add secrets.

