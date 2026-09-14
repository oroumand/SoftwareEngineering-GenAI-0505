# Feature Specification: Register and Read User Profiles

**Feature ID:** USR-001  
**Status:** Accepted for educational demo  
**Input field count:** exactly 6

## Problem

An API consumer needs to register a basic user profile and read registered profiles. A vague implementation can appear correct while silently accepting duplicate identities, malformed phone numbers, underage users or inconsistent country values.

## In scope

- register one profile;
- get a profile by identifier;
- list profiles;
- validate the six input fields;
- reject duplicate email addresses;
- return observable HTTP outcomes.

## Out of scope

- authentication and authorization;
- update, delete and soft-delete;
- database persistence and migrations;
- email/phone verification;
- address, avatar, password and roles;
- pagination.

## Input contract

| Property | Required | Rule |
|---|---:|---|
| `FirstName` | yes | trimmed, 2–50 characters |
| `LastName` | yes | trimmed, 2–50 characters |
| `Email` | yes | valid address, max 254, normalized lowercase |
| `PhoneNumber` | yes | E.164, leading `+`, 8–15 digits |
| `DateOfBirth` | yes | not future; age at least 13 |
| `Country` | yes | two-letter ISO-style code, normalized uppercase |

`Id` and `CreatedAtUtc` are system-generated output fields and are not user input properties.

## User stories and acceptance scenarios

### US1 — Register a valid profile (P1)

Given all six fields are valid and the normalized email does not exist  
When the consumer submits the profile  
Then the API returns `201 Created`, a generated identifier and creation time.

### US2 — Reject invalid input (P1)

Given one or more fields violate the input contract  
When the consumer submits the profile  
Then the API returns `400 Bad Request` without persisting a profile.

### US3 — Reject duplicate email (P1)

Given `sara@example.com` is registered  
When another request uses `SARA@EXAMPLE.COM`  
Then the API returns `409 Conflict` and only one profile remains.

### US4 — Read profiles (P2)

Given profiles exist  
When a consumer requests the list or a known identifier  
Then the API returns normalized profile data.

Given an identifier does not exist  
When it is requested  
Then the API returns `404 Not Found`.

## Functional requirements

- **FR-001:** The create contract SHALL expose exactly the six specified input fields.
- **FR-002:** The system SHALL normalize names by trimming, email to lowercase and country to uppercase.
- **FR-003:** The system SHALL enforce all input rules before persistence.
- **FR-004:** The system SHALL enforce case-insensitive global email uniqueness.
- **FR-005:** The system SHALL generate `Id` and `CreatedAtUtc`.
- **FR-006:** The system SHALL provide create, get-by-id and list behavior.
- **FR-007:** The API SHALL map validation, conflict and missing-resource outcomes to 400, 409 and 404.
- **FR-008:** Unexpected errors SHALL not expose stack traces or internal exception text.

## Success criteria

- **SC-001:** All specified acceptance scenarios have executable tests.
- **SC-002:** The complete solution builds with zero warnings and zero errors.
- **SC-003:** Architecture tests show Domain has no project dependency and Application does not reference Infrastructure or API.
- **SC-004:** A smoke request can create a valid profile and retrieve it by the returned location.

