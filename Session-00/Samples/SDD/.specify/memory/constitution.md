# UserProfiles Demo Constitution

## I. Specification is the source of truth

Public behavior, scope, invariants and success criteria come from the accepted Spec. Code is a derived artifact. Material ambiguity is resolved in `clarifications.md` before implementation.

## II. Onion boundaries are enforced

Domain is dependency-free. Application depends only on Domain. Infrastructure implements ports owned by Application. API is a thin transport and composition boundary. Business rules do not live in endpoints or repositories.

## III. CQRS uses MediatR

State-changing behavior is expressed as commands; reads are expressed as queries. Handlers are discovered and invoked through MediatR. A local imitation of mediator abstractions is prohibited.

## IV. Explicit time and persistence

Business logic does not read system time directly; it consumes `IClock`. Application owns `IUserProfileRepository`; Infrastructure owns its implementation. In-memory persistence is an explicit educational constraint, not a production claim.

## V. Secure, observable contracts

User input is validated at the domain/use-case boundary. Duplicate email returns conflict, invalid input returns bad request, missing user returns not found and unexpected errors do not expose internal details.

## VI. Evidence before completion

Restore, build, tests and architecture dependency checks must pass. Each functional requirement must map to implementation and verification evidence in the traceability matrix.

**Version:** 1.0.0  
**Ratified:** 2026-07-25

