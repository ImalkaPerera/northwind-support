# Northwind Support Desk

A support ticket system: raise tickets, filter them, assign them to agents and escalate them.
**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

## What I built

| # | Task | In this README | Write-up |
| --- | --- | --- | --- |
| 1 | Fix DEFECT-117, filter in the database | [DEFECT-117](#the-defect-defect-117) · [Filtering](#filtering) | [defect](docs/defect-117.md), [filtering](docs/filtering.md) |
| 2 | Triage, escalation, SLA status | [Task 2 rules](#the-task-2-rules-as-i-understood-them) | [escalation](docs/escalation.md) |
| 3 | Agent sign-in with JWT | [Authentication](#authentication) | [authentication](docs/authentication.md) |
| 4 | Dockerise the API | [Docker](#docker) | [docker](docs/docker.md) |

All four tasks are done.

The assignment is [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md), my approach is
[`APPROACH.md`](APPROACH.md).

### The Task 2 rules as I understood them

Details are in [docs/escalation.md](docs/escalation.md).

- **Priority and due date:** the requested priority (Medium if none), always Critical for a
  `ForcesCriticalPriority` category. Windows are 4, 8, 24 and 72 hours; Premium gets half, never
  under 1 hour. They live in the `Sla` config section.
- **Owner:** an active agent below their limit, specialised where needed; fewest open tickets,
  then lowest id. Nobody fits: unassigned, with a reason.
- **Escalation and status:** one level up with a reason, due date restarted, history written;
  Critical, Resolved and Closed are refused. SLA status is derived, never stored.

## Quick start

**Prerequisites:** .NET SDK 10, Node.js 22 (20.19+ works), Docker Desktop running.

```bash
npm install
docker compose up --build        # SQL Server and the API, API on http://localhost:5080
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web    # web app on http://localhost:5173
```

PowerShell:

```powershell
$env:VITE_API_PROXY_TARGET="http://localhost:5080"; npm run web
```

Or run the API on your machine, with only the database in Docker:

```bash
npm install
npm run db:up                    # SQL Server on localhost:1433
npm run api                      # terminal 1: migrates, seeds, API on https://localhost:7043
npm run web                      # terminal 2: web app on http://localhost:5173
```

`.env` is optional; every value has a local default. Other commands, the API list and
troubleshooting are in [docs/running.md](docs/running.md).

## Development login

Local use only. Every seeded agent has the same password, hashed when the data is seeded.

- Email: `alex.turner@northwind-support.example`
- Password: `LocalDev-Password-1`

Other agents use their own email with the same password. Yuki Tanaka is inactive and can't sign in.

## Design decisions

**The defect**

- **Chosen:** keep the filters that requests use separate from the search box text. Dropdowns
  apply at once; the search text applies 300 ms after typing stops.
- **Rejected:** adding every filter to the existing dependency array, because the debounce wraps
  the whole effect and dropdowns would wait too. Also rejected: a remount key or forced reload,
  which hides the cause.

**Filtering**

- **Chosen:** one `ApplyFilters` with a `Where` per filter sent, so a new filter is one more `if`.
  **Rejected:** filtering in C#, which breaks the count and paging, and a specification class,
  which is more than seven filters need.
- **Chosen:** tests on in-memory SQLite, because mocks don't prove the filters become real SQL.

**Escalation and triage**

- **Chosen:** rules in small domain classes (`SlaPolicy`, `PriorityResolver`, `AgentSelector`,
  `Ticket.Escalate`) shared through `TicketTriage`. **Rejected:** writing them in the handlers,
  where create and escalate would each have a copy.
- **Chosen:** the at-risk window comes from config, by priority and tier. **Rejected:** due date
  minus created date, which is wrong after an escalation, and a stored column, which needs a
  migration for something the config already answers.
- **Chosen:** history foreign keys use `Restrict`. **Rejected:** cascade, because an audit trail
  shouldn't vanish by accident.
- **Chosen:** the ticket being escalated isn't counted against its own agent's limit.
  **Rejected:** counting it, because an agent at their limit would lose every ticket they hold.
- **Chosen:** ties go to the lowest id. **Rejected:** random, which isn't repeatable.
- **Chosen:** the SLA filter compares dates with spans worked out in C#. **Rejected:** date
  arithmetic in SQL, which differs between SQL Server and SQLite.

**Authentication**

- **Chosen:** a nullable `PasswordHash` column on `Agent`. **Rejected:** a `Users` table; every
  user is an agent and there are no roles, so it only adds a join.
- **Chosen:** 60 minute tokens. **Rejected:** longer, because there are no refresh tokens, so a
  shorter life limits a stolen one.
- **Chosen:** `sessionStorage`. **Rejected:** `localStorage`, which keeps the token after the
  browser closes.
- **Chosen:** one fallback authorization policy, `[AllowAnonymous]` only on login. **Rejected:**
  `[Authorize]` per controller, where a new endpoint could be left open.

**Patterns I used**

- **Factory:** `SlaPolicyFactory` builds the policy from config once and fails at startup if a
  value is missing, so a bad config never reaches a request.
- **Domain service:** `AgentSelector` and `TicketTriage` hold rules that span several objects and
  don't belong on one entity; create and escalate both use them.
- **Composable filter steps:** `ApplyFilters` and `SlaStatusFilter` add one condition each, so a
  new filter is one more step, not a rewrite.

## The tasks

### The defect (DEFECT-117)

**Root cause:** the `useTicketList` effect only depended on `search`, `page` and `pageSize`, so
changing status or priority updated the state but never sent a request.

**Fix:** every filter change sends a request at once; only the search text is debounced. I also
removed the lint suppression that hid the missing dependency.

**Verify:** change Status and a `GET /api/tickets?status=...` appears in the Network tab. The
regression tests assert the request and fail on the original code.

### Filtering

**Where:** `TicketQueries.ApplyFilters`. Each filter sent adds one `Where` before the count and
paging, so `totalCount` and the pager match the filtered list.

**Checked:** `TicketQueriesFilterTests` run on SQLite, so the filters become SQL, and fail on the
original query. The SLA filter was also run on the real SQL Server.

**Next filter:** add the field to `TicketQuery` and one more `if` in `ApplyFilters`.

### Authentication

**Credentials:** `PasswordHash` on `Agent`, salted and hashed with ASP.NET Identity's
`PasswordHasher` (PBKDF2). I wrote no hashing myself.

**Token:** `POST /api/auth/login` returns a signed JWT, its expiry and the agent's name. A wrong
email, a wrong password and an inactive agent give the same 401. It lasts 60 minutes, and the key
comes from `Jwt__SigningKey`, never from code. `escalatedBy` comes from the token.

**Storage:** the web app keeps it in `sessionStorage` and sends it from `client.ts`. Any script on
the page can read it, so an XSS bug would expose it.

**For production:** an httpOnly cookie with short tokens, refresh tokens and revocation, a key from
a secret store, and lockout or rate limiting.

### Docker

**Image:** multi-stage `apps/api/Dockerfile`, built from the repo root. The SDK image builds; the
ASP.NET image runs it as the non-root `app` user on HTTP port 8080. Project files are restored
before the source is copied, so a code change reuses the restore layer. The API image is 455 MB;
the SDK image is 1.3 GB.

**Environment:** nothing is baked in. Compose passes `ConnectionStrings__Default`,
`Jwt__SigningKey` (from `JWT_SIGNING_KEY`) and `ASPNETCORE_ENVIRONMENT`; see `.env.example`.

**Migrations:** here the API migrates and seeds itself in `Development`. In a real deployment
migrations would be a separate release step, such as an EF Core migration bundle, so instances
don't migrate at once and the app's database user doesn't need schema rights.

**Checked:** a fresh stack on an empty volume (a second project, not the literal `down -v` on my
main one), the cached restore layer, `whoami` as `app`, and the web app in a browser. See
[docs/docker.md](docs/docker.md).

## Across all tasks

### Database

**Added:** the `TicketEscalations` table (`AddTicketEscalations`) and a nullable `PasswordHash`
column on `Agent` (`AddAgentPasswordHash`).

**Index:** `(TicketId, EscalatedAtUtc DESC)` serves "one ticket's history, newest first"; the two
agent indexes EF added back the foreign key checks.

### Testing

```bash
npm test         # dotnet test, then the web tests (Vitest). No database or Docker needed
```

**Tested:** 207 backend tests (203 unit, 4 architecture) and 64 web tests: SLA boundaries with a
fixed clock, agent choice, escalation, filters on SQLite, login and hashing, real HTTP calls
through the app (401 without a token, 200 with), and the web screens and sign-in guard.

**Not tested, on purpose:** SQL Server itself (SQLite stands in; I checked the SLA filter there by
hand), two tickets raised at once, the Docker build, the 503 from `/health`, and a browser end to
end run.

## AI usage

**Tools and purpose.** Claude Code, as a pair programmer: to get up to speed on the existing .NET
code, draft code and tests, and help word the docs. The decisions and the checking were mine.

**A workflow that worked.** Small steps. For each one I asked for a plain-English explanation of
the change, ran the tests, tried it in the browser or Postman, and only then committed it.

**Accepted as-is.** The `ApplyFilters` design (one `Where` per filter, before the count) and the
small rule classes behind `TicketTriage`.

**Changed or rejected.** It called the `FromAgentId` and `ToAgentId` indexes unused. I kept them,
because they make the `Restrict` foreign key checks fast when an agent changes.

**Bugs.** It introduced one: the first login handler kept a hash in a static field, so tests
passed or failed depending on their order. Running the full test class exposed it, and it was
removed. It also helped me find one: the `eslint-disable` comment in `useTicketList` was hiding
the warning that pointed at DEFECT-117.

**How I verified it.** The backend and web tests, regression tests that fail on the original code,
the SLA filter checked against the real SQL Server, and the main flows tried by hand (screenshots
in `docs/`).

## Known limitations

- **Task 1:** a slow request can overwrite a newer result. Search uses `LIKE '%text%'`, which
  can't use an index, and is case-insensitive on SQL Server but not on SQLite.
- **Task 2:** two tickets raised at once could both go to an agent with room for one. Changing
  the `Sla` section or a customer's tier changes the status of existing tickets. Ticket
  references use the highest id + 1 and can jump or clash; that was there before my changes.
- **Task 3:** no refresh tokens or revocation, so a deactivated agent's token works until it
  expires. No lockout or rate limiting. All seeded agents share one development password. Not
  built, as the task says: registration, password reset, OAuth/SSO, MFA, roles.
- **Task 4:** the container runs as `Development`, so Swagger is on, and compose has local
  defaults for the SQL password and signing key. The image has no `HEALTHCHECK` of its own.
