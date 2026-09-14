# Traceability Matrix

| Requirement | Implementation | Verification |
|---|---|---|
| FR-001 | `CreateUserProfileRequest`, `CreateUserProfileCommand` | exact-six-properties test |
| FR-002 | `UserProfile.Create` | normalization test |
| FR-003 | `UserProfile.Create` | invalid input test |
| FR-004 | command handler + in-memory repository lock | duplicate-email test |
| FR-005 | `UserProfile.Create` + `IClock` | normalization/creation test |
| FR-006 | MediatR command and two queries + endpoints | build and HTTP smoke |
| FR-007 | `ApiExceptionMiddleware` | HTTP smoke: 400/409/404 |
| FR-008 | catch-all safe Problem Details mapping | code review + HTTP behavior |
| SC-002 | complete solution | recorded restore/build output |
| SC-003 | project references + reflection tests | architecture tests |
| SC-004 | POST Location + GET by id | recorded HTTP smoke |

