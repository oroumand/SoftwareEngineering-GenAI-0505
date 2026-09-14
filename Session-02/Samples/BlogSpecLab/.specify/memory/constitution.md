<!--
Sync Impact Report
- Version change: template (unversioned) -> 1.0.0
- Modified principles: placeholder principles -> eight initial BlogSpecLab principles
- Added sections: Core Principles; Compliance and Quality Gates; Governance
- Removed sections: unresolved template placeholder sections
- Follow-up TODOs: none
-->
# BlogSpecLab Constitution

## Core Principles

### I. Unified Technical Platform

All .NET-based projects in the solution MUST be developed with .NET 10 and MUST target
`net10.0`. The SDK version declared in shared build configuration and in `global.json`, when
present, MUST be compatible with .NET 10. A different .NET version or target framework MAY be
used only after this constitution has been formally amended.

This uniform platform prevents library incompatibilities, inconsistent development, build, and
deployment behavior, and unnecessary maintenance complexity. Compliance MUST be checked by
inspecting `.csproj` files, shared build configuration, and `global.json`. CI MUST fail when it
finds an incompatible target framework or the solution cannot build with .NET 10.

### II. Dependencies Point Inward Only

The production solution structure MUST follow Onion Architecture, and dependencies between
production layers MUST point only toward inner layers.

- Domain MUST NOT depend on any other solution project, external package, infrastructure
  technology, database, ASP.NET Core, or third-party framework. It MAY use only standard .NET
  facilities.
- Among solution projects, Application MAY depend only on Domain. Application MUST NOT depend on
  Infrastructure, API, or Host.
- Infrastructure MAY depend on Application and Domain to implement ports and contracts, but it
  MUST NOT depend on API or Host.
- API or Host, acting as the Composition Root, MAY reference Application and Infrastructure to
  register and connect dependencies.
- Controllers and other API presentation components MUST NOT receive or consume concrete
  Infrastructure types directly. They MUST interact with the system through Application use
  cases and contracts.
- Test projects MAY depend on the layer under test and on test libraries. They are not part of
  the production dependency graph.

An inner layer MUST NOT create a reverse reference to reach an outer-layer capability. External
technologies and services MUST be accessed through contracts defined in the appropriate inner
layer and implemented by Infrastructure.

This direction keeps business logic and use cases independent of web interfaces, databases, and
supporting technologies. Every pull request MUST be checked for project references, Domain
package references, and namespace dependencies. Automated architecture tests MUST detect every
prohibited dependency between layers.

### III. Aggregate-Centered Structure

The Aggregate MUST be the primary unit for organizing business artifacts in every architecture
layer. An Aggregate MUST have the same name in every layer that contains artifacts for it, and
all artifacts owned by that Aggregate MUST reside in that layer's corresponding Aggregate
folder.

Each Aggregate folder MUST have a clear technical substructure appropriate to the layer. Domain
MAY contain `Entities`, `ValueObjects`, `Events`, and `Services`; Application MAY contain use-case
artifacts such as commands, queries, handlers, DTOs, and validators; Infrastructure MAY contain
repository implementations, persistence mappings, and related adapters; API MAY contain
controllers, HTTP models, and mappings for the Aggregate.

Global folders such as `Entities`, `Services`, `Repositories`, or `Controllers` that mix artifacts
from multiple Aggregates MUST NOT be used. Artifacts genuinely not owned by one Aggregate—such
as the Composition Root, global migrations, shared technical infrastructure, or cross-Aggregate
coordinators—MAY reside in a separate folder with an explicit name and responsibility. Only
artifacts that are genuinely shared, stable, and Aggregate-independent MAY reside in `Shared` or
`Common`; those folders MUST NOT be used to avoid assigning ownership or to collect unrelated
files.

This structure makes code ownership and discovery clear. Pull-request review MUST verify artifact
ownership, consistent Aggregate naming across layers, layer-appropriate substructure, and the
continued cohesion of shared folders.

### IV. Rich, Behavior-Oriented Domain

Business logic, state-transition rules, and Aggregate invariants MUST be implemented in Domain
within the appropriate Aggregate Root, Entity, Value Object, or Domain Service. Entity and
Aggregate internal state MUST NOT be changed directly from outside; state changes MUST occur only
through meaningful Domain methods.

An Aggregate MUST validate its initial data when created. An invalid state MUST NOT be observable
through the Aggregate's public boundary or remain after a Domain operation completes. Domain
entities that contain only getters and setters while their business logic resides in Application,
Controller, or Infrastructure MUST NOT be used.

A Domain Service MAY be used only when Domain behavior does not naturally belong to a specific
Entity, Value Object, or Aggregate Root. A Domain Service MUST NOT extract behavior that belongs
to an Aggregate or be used to create an anemic domain model. Application, Infrastructure, and API
MUST NOT bypass Aggregate rules or invariants.

This model keeps business knowledge in its natural location and protects invariants centrally.
Code review MUST examine creation, state changes, business-rule placement, and invariant
protection. Every new or changed invariant MUST have Domain unit tests proving both valid behavior
and rejection of invalid state.

### V. Application Coordinates Use Cases

Application MUST define and coordinate system use cases, but it MUST NOT implement core business
logic or internal Aggregate rules. Application MAY load Aggregates through contracts, invoke
Domain behavior, coordinate operations, call external ports, and return use-case results.

Use-case execution policies—including transaction coordination, use-case authorization,
idempotency, and coordination of external-service calls—MAY reside in Application. Rules that
determine whether a business state is valid or invalid MUST reside in Domain.

Application MUST NOT depend on database implementations, Entity Framework Core, ASP.NET Core
controllers, HTTP details, or concrete Infrastructure types. Contracts Application requires for
persistence, messaging, or external services MUST be defined in an inner layer and implemented by
Infrastructure.

This separation keeps use-case orchestration distinct from Domain behavior and permits independent
testing. Use-case review MUST verify that Application coordinates flow, delegates business rules
to Domain, hides technology through contracts, and consumes no API type or concrete
Infrastructure implementation.

### VI. Standard, Thin Controllers

Every business HTTP endpoint MUST be implemented with an ASP.NET Core MVC controller derived from
`ControllerBase`. Minimal APIs MUST NOT be used for business endpoints. Operational endpoints
provided directly by a framework or infrastructure—such as health checks, metrics, and OpenAPI—
are exempt only when they expose no business behavior.

A controller MUST be responsible only for receiving and converting an HTTP request, applying
boundary validation, invoking the appropriate Application use case, and converting its result to
an HTTP response. A controller MUST NOT contain business logic, directly manage Aggregate state,
access a `DbContext` or Infrastructure repository directly, or consume a concrete Infrastructure
type.

This standard creates a consistent API contract and prevents system logic from becoming coupled
to HTTP or ASP.NET Core. API-route review MUST verify controller use, the strictly operational
nature of exceptions, the absence of direct Infrastructure or database access, and the absence
of business rules in controllers.

### VII. Architecture and Domain Rules Are Testable

Automated architecture tests MUST protect every dependency-direction rule in this constitution.
Every new or changed Domain invariant MUST have a unit test proving at least one valid path and
prevention of invalid state. Domain tests MUST cover Aggregate creation, important behaviors, and
invariant protection.

CI MUST run the build, Domain tests, architecture tests, and all other automated project tests. A
change with a failed build, failed test, or architecture-rule violation MUST NOT be merged. A test
MUST NOT be disabled, deleted, or bypassed to make CI pass temporarily, except when the associated
behavior or rule has been formally changed and an appropriate replacement test is included in the
same change.

Automated enforcement provides durable protection beyond manual review. CI results for every
required suite MUST be recorded, and successful completion of all required checks MUST be a
condition for pull-request approval and merge.

### VIII. Secure Input and Output Boundaries

All input received through HTTP, messages, files, databases, or external services MUST be
validated before use in a use case. Boundary validation MUST NOT replace Domain invariant
protection; Domain MUST independently preserve valid state regardless of outer-layer validation.

Every protected operation MUST have explicit, testable authentication and authorization. Secrets,
credentials, and sensitive data MUST NOT be stored in the repository or in committable
configuration. Logs and API responses MUST NOT expose secrets, credentials, stack traces, internal
implementation details, or unnecessary sensitive data. Input and access errors MUST be converted
to controlled responses that do not expose internal details.

These controls prevent invalid or malicious input, unauthorized access, and sensitive-data
disclosure while preserving Domain defense in depth. Automated tests MUST cover invalid input,
unauthorized access, and error responses. Pull-request review MUST inspect sensitive-data handling,
and CI MUST scan the repository for exposed secrets.

## Compliance and Quality Gates

Every specification, implementation plan, task set, code review, and pull request MUST be checked
against all eight principles. Reviewers MUST record or otherwise verify applicable platform,
dependency, Aggregate organization, Domain behavior, Application responsibility, controller,
testing, and security checks. An exception is permitted only where a principle explicitly uses
MAY or states an exemption; convenience, schedule pressure, or existing noncompliance does not
create an exception.

CI MUST enforce all checks that this constitution identifies as automated. Manual review MUST
cover rules that cannot yet be reliably automated. Identified noncompliance MUST be corrected
before merge, or the constitution MUST first be formally amended when the intended change alters
a governing rule.

## Governance

This constitution is the highest project governance authority for architecture and engineering
practice. When another project document or convention conflicts with it, this constitution MUST
prevail.

Amendments MUST be proposed as an explicit constitution change. Each proposal MUST state its
rationale, affected principles or sections, compatibility impact, and any migration required for
existing code or documentation. An amendment MUST be reviewed and approved through the project's
normal pull-request process before dependent changes are merged. A rule governed here MUST NOT be
bypassed through a local convention, waiver, or undocumented exception.

Constitution versions MUST follow semantic versioning:

- MAJOR for backward-incompatible governance changes, including removal or fundamental
  redefinition of a principle.
- MINOR for a new principle or section, or a material expansion of governance.
- PATCH for clarifications and non-semantic wording corrections.

Every amendment MUST update the version, the Last Amended date, and the Sync Impact Report.
Ratification date MUST remain the date of initial adoption. Compliance MUST be reviewed during
planning and again during pull-request review; CI evidence and required tests MUST be available
before approval. Reviewers MUST reject changes that violate the constitution unless an approved
amendment establishing the new rule is part of or precedes the change.

**Version**: 1.0.0 | **Ratified**: 2026-08-10 | **Last Amended**: 2026-08-10
