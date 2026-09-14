# Clarification Record

| Decision | Accepted answer | Effect |
|---|---|---|
| Email uniqueness | global and case-insensitive | repository must protect normalized email |
| Phone format | E.164 | one deterministic validation rule |
| Minimum age | 13 at the current UTC date | time must be injected |
| Country | two ASCII letters, normalized uppercase | stable wire/domain value |
| Persistence | process-local in-memory repository | suitable only for live educational demo |
| Authentication | out of scope | endpoints are public in this demo |
| Update/delete | out of scope | avoids lifecycle decisions unrelated to comparison |
| Error format | RFC-style Problem Details | safe and consistent HTTP boundary |

No material ambiguity remains for the accepted educational scope.

