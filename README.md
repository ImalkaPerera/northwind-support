# Northwind Support Desk

A support ticket system: raise tickets, filter them, assign them to agents and escalate them.
**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

## What I built

| # | Task | In this README | Write-up |
| --- | --- | --- | --- |
| 1 | Fix DEFECT-117, filter in the database | [DEFECT-117](#the-defect-defect-117-task-1) · [Filtering](#filtering-task-1) | [defect](docs/defect-117.md), [filtering](docs/filtering.md) |
| 2 | Triage, escalation, SLA status | [Escalation and triage](#escalation-and-triage-task-2) | [escalation](docs/escalation.md) |
| 3 | Agent sign-in with JWT | [Authentication](#authentication-task-3) | [authentication](docs/authentication.md) |
| 4 | Dockerise the API | [Docker](#docker-task-4) | [docker](docs/docker.md) |

All four tasks are done.

The assignment is [`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md), my approach is
[`APPROACH.md`](APPROACH.md).

## Quick start

**Prerequisites:** .NET SDK 10, Node.js 22 (20.19+ works), Docker Desktop running.

```bash
npm install
```

**Option A: Docker** (SQL Server and the API in containers, API on http://localhost:5080):

```bash
docker compose up --build
VITE_API_PROXY_TARGET=http://localhost:5080 npm run web    # web app on http://localhost:5173
```

PowerShell: `$env:VITE_API_PROXY_TARGET="http://localhost:5080"; npm run web`

**Option B: local** (API on your machine, only the database in Docker):

```bash
npm run db:up                    # SQL Server on localhost:1433
npm run api                      # terminal 1: migrates, seeds, API on https://localhost:7043
npm run web                      # terminal 2: web app on http://localhost:5173
```

`.env` is optional; every value has a local default. Other commands, the API list and
troubleshooting are in [docs/running.md](docs/running.md).

## Development login

Local use only. Every seeded agent shares this password, hashed when the data is seeded.

- Email: `alex.turner@northwind-support.example`
- Password: `LocalDev-Password-1`

Other agents use their own email. Yuki Tanaka is inactive and can't sign in.

## Design decisions

**The defect**

- **Chosen:** keep the request filters separate from the search box text: dropdowns apply at
  once, search 300 ms after typing stops.
- **Rejected:** adding every filter to the existing dependency array, because the debounce wraps
  the whole effect and dropdowns would wait, and a remount key or forced reload, which hides the
  cause.

**Filtering**

- **Chosen:** one `ApplyFilters` with a `Where` per filter sent. **Rejected:** filtering in C#,
  which breaks the count and paging, and a specification class, which is more than seven filters
  need.
- **Chosen:** tests on in-memory SQLite, because mocks don't prove the filters become real SQL.

**Escalation and triage**

- **Chosen:** small domain classes (`SlaPolicy`, `PriorityResolver`, `AgentSelector`,
  `Ticket.Escalate`) shared through `TicketTriage`. **Rejected:** rules in the handlers, where
  create and escalate would each have a copy.
- **Chosen:** the at-risk window from config, by priority and tier. **Rejected:** due date minus
  created date, which is wrong after an escalation, and a stored column, which needs a migration
  the config makes unnecessary.
- **Chosen:** history foreign keys use `Restrict`. **Rejected:** cascade, because an audit trail
  shouldn't vanish by accident.
- **Chosen:** the escalated ticket isn't counted against its own agent's limit. **Rejected:**
  counting it, because an agent at their limit would lose every ticket they hold.
- **Chosen:** ties go to the lowest id. **Rejected:** random, which isn't repeatable.
- **Chosen:** the SLA filter runs in SQL; only the window lengths are worked out in C# and passed
  in as constants. **Rejected:** date arithmetic in SQL, which differs between SQL Server and
  SQLite.

**Authentication**

- **Chosen:** a nullable `PasswordHash` on `Agent`. **Rejected:** a `Users` table; every user is
  an agent and there are no roles, so it only adds a join.
- **Chosen:** 60 minute tokens. **Rejected:** longer, because without refresh tokens a short life
  limits a stolen one.
- **Chosen:** `sessionStorage`. **Rejected:** `localStorage`, which keeps the token after the
  browser closes.
- **Chosen:** one fallback authorization policy, `[AllowAnonymous]` only on login. **Rejected:**
  `[Authorize]` per controller, where a new endpoint could be left open.

**Patterns I used**

- **Factory:** `SlaPolicyFactory` builds the policy from config once and fails at startup on a
  missing value, so bad config never reaches a request.
- **Domain service:** `AgentSelector` and `TicketTriage` hold rules that span several objects;
  create and escalate both use them.
- **Composable filter steps:** `ApplyFilters` and `SlaStatusFilter` add one condition each, so a
  new filter is one more step.

## The tasks

### The defect (DEFECT-117, Task 1)

**Root cause:** the `useTicketList` effect only depended on `search`, `page` and `pageSize`, so
changing status or priority updated the state but never sent a request.

**Fix:** every filter change sends a request at once; only the search text is debounced. I also
removed the lint suppression that hid the missing dependency.

**Verify:** change Status and a `GET /api/tickets?status=...` appears in the Network tab. The
regression tests assert the request and fail on the original code.

**More:** [docs/defect-117.md](docs/defect-117.md).

### Filtering (Task 1)

**Where:** `TicketQueries.ApplyFilters`. Each filter sent adds one `Where` before the count and
paging, so `totalCount` and the pager match the filtered list.

**Checked:** `TicketQueriesFilterTests` run on SQLite, so the filters become SQL, and fail on the
original query.

**Next filter:** add the field to `TicketQuery` and one more `if` in `ApplyFilters`.

**More:** [docs/filtering.md](docs/filtering.md).

### Escalation and triage (Task 2)

**Rules as I understood them:**

- **Priority and due date:** the requested priority (Medium if none), Critical for a
  `ForcesCriticalPriority` category. Windows are 4, 8, 24 and 72 hours, halved for Premium with a
  1 hour floor. They are in the `Sla` section of `appsettings.json`.
- **Owner:** active, below their limit, specialised where needed; fewest open tickets, then
  lowest id. Nobody fits: unassigned, with a reason.
- **Escalation and status:** one level up with a reason, due date restarted, history written;
  Critical, Resolved and Closed are refused. SLA status is derived, never stored.

**Where:** small domain classes shared through `TicketTriage`, so create and escalate use the same
rules; the windows come from the `Sla` config.

**Checked:** boundary tests with a fixed clock, SQLite for the history and the SLA filter, the SLA
filter on the real SQL Server, and the flows by hand.

**More:** [docs/escalation.md](docs/escalation.md).

### Authentication (Task 3)

**Credentials:** `PasswordHash` on `Agent`, salted and hashed with ASP.NET Identity's
`PasswordHasher` (PBKDF2). I wrote no hashing myself.

**Token:** `POST /api/auth/login` returns a signed JWT, its expiry and the agent's name. A wrong
email, a wrong password and an inactive agent give the same 401. It lasts 60 minutes, and the key
comes from `Jwt__SigningKey`, never from code. `escalatedBy` comes from the token.

**Storage:** the web app keeps it in `sessionStorage` and sends it from `client.ts`. Any script on
the page can read it, so an XSS bug would expose it.

**For production:** an httpOnly cookie with short tokens, refresh tokens and revocation, a key from
a secret store, and lockout or rate limiting.

**More:** [docs/authentication.md](docs/authentication.md).

### Docker (Task 4)

**Image:** multi-stage `apps/api/Dockerfile`, built from the repo root: the SDK image builds, the
ASP.NET image runs it as the non-root `app` user on HTTP port 8080. Project files are restored
first, so a code change reuses the restore layer. The API image is 455 MB; the SDK image is 1.3 GB.

**Environment:** the image holds no connection string or secret; `.dockerignore` keeps
`appsettings.Development.json` out. Compose passes `ConnectionStrings__Default`, `Jwt__SigningKey`
(from `JWT_SIGNING_KEY`) and `ASPNETCORE_ENVIRONMENT` at run time, and falls back to local
development values for the SQL password and signing key when there is no `.env`; see
`.env.example`.

**Migrations:** here the API migrates and seeds itself in `Development`. In a real deployment
migrations would be a separate release step, such as an EF Core migration bundle, so instances
don't migrate at once and the app's database user doesn't need schema rights.

**Checked:** `docker compose down -v && docker compose up --build` ended with `/health` returning
200 and 40 seeded tickets at `/api/tickets` (401 without a token). Also the cached restore layer,
`whoami` printing `app`, the image sizes above from `docker images`, and the web app in a browser.
See [docs/docker.md](docs/docker.md).

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

**Tested:** 207 backend tests (203 unit, 4 architecture) and 64 web tests, covering SLA
boundaries, agent choice, escalation, filters, login, 401 and 200 over real HTTP, and the web
screens.

**Not tested, on purpose:** SQL Server itself (SQLite stands in; the SLA filter was checked there by
hand), two tickets raised at once, the Docker build, the 503 from `/health`, and a browser end to
end run.

## AI usage

**Tools and purpose.** Claude Code, as a pair programmer, to learn the existing .NET code and draft
code, tests and docs; the decisions and the checking were mine.

**A workflow that worked.** Small steps: I had each change explained in plain English, ran the
tests, tried it in the browser or Postman, then committed.

**Accepted as-is.** The `ApplyFilters` design and the small rule classes behind `TicketTriage`.

**Changed or rejected.** It called the `FromAgentId` and `ToAgentId` indexes unused; I kept them
because they speed up the `Restrict` foreign key checks.

**Bugs.** It introduced a login handler that kept a hash in a static field, so tests passed or
failed by order (found by running the whole test class, then removed), and it helped me spot the
`eslint-disable` that hid DEFECT-117.

**How I verified it.** The backend and web tests, regression tests that fail on the original code,
the SLA filter on the real SQL Server, and the main flows by hand (screenshots in `docs/`).

## Known limitations

- **Task 1:** a slow request can overwrite a newer result. Search uses `LIKE '%text%'`, which
  can't use an index and is case-insensitive on SQL Server but not SQLite.
- **Task 2:** two tickets raised at once could both go to an agent with room for one. Changing the
  `Sla` section or a customer's tier changes the status of existing tickets. References use the
  highest id + 1 and can jump or clash, as before my changes.
- **Task 3:** no refresh tokens or revocation, so a deactivated agent's token works until it
  expires; no lockout or rate limiting; all seeded agents share one development password. Not
  built, as the task says: registration, password reset, OAuth/SSO, MFA, roles.
- **Task 4:** the container runs as `Development`, so Swagger is on, and compose has local
  defaults for the SQL password and signing key. The image has no `HEALTHCHECK`.
- **Also not built:** email verification and HTTPS certificate work; notifications, background
  jobs, real-time updates, microservices, event sourcing, multi-tenancy, internationalisation,
  caching, CI/CD, a visual redesign; and, as Task 4 says, Kubernetes/Helm, registry pushes, HTTPS
  in the container, dockerising the React app, Docker secrets or a vault, multi-architecture
  builds and any deployment beyond the Task 4 image.
