# Execution Evidence

This file is updated only from commands that were actually run.

## Restore

PASS on 2026-07-25. Five projects restored with the repository `NuGet.Config`,
the bundled MediatR feed and the machine package cache. NuGet audit and
certificate revocation lookup were disabled only for the sandboxed offline
verification environment.

## Build

PASS on 2026-07-25:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

## Tests

PASS on 2026-07-25:

```text
Failed: 0
Passed: 6
Skipped: 0
Total: 6
```

The tests cover normalization, invalid input, duplicate email, exact request
property count and inward architecture references.

## HTTP smoke

PASS on 2026-07-25 against `http://127.0.0.1:5071`:

```text
POST valid profile        201
normalized email          neda@example.com
normalized country        IR
POST duplicate email      409
POST invalid profile      400
GET created profile       200
GET missing profile       404
GET profile list          200
GET health                200
```

MediatR 12.5 produced no license warning during the runtime smoke.
