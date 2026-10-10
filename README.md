# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

1. Update this **README** with what you built, your design decisions (chosen, rejected and why),
   the defect's root cause, dev login credentials, how to run and test it, AI usage, and known
   limitations. A reviewer must be able to run it without asking you anything.
2. Add an **APPROACH.md** explaining your approach and thought process for each task.
3. Commit both, then send us the **link to your fork**, the **dev login credentials**, and a
   **rough note of time spent** on what. (If the fork is private, add the reviewers as collaborators.)

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `slaStatus`, `sortBy`, `sortDirection`. Filters are applied in the database. |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket. It is triaged: priority, due date and owner are chosen, and a `triage` object says why. `201` even when no agent is eligible |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `POST /api/tickets/{id}/escalate` | Raise the priority one level with a `reason` and `escalatedBy`. `400` bad input, `404` unknown ticket, `409` Critical, Resolved or Closed |
| `GET /api/tickets/{id}/escalations` | The escalation history, newest first |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |

## The defect (DEFECT-117)

**Root cause:** the `useTicketList` effect only depended on `search`, `page` and `pageSize`.
Changing a filter such as status or priority updated the state but never triggered a new API
request.

**Fix:** I restructured the hook so every filter change sends a request straight away. Only the
search text is debounced (300 ms), so typing doesn't send a request per keystroke. I also removed
the lint suppression that was hiding the missing-dependency warning.

**Testing:** the DEFECT-117 tests assert the request that is sent after each filter change; 7 of
them fail on the original code and pass with the fix. Debounce and reset are tested too.

**Check it by hand:** change Status in the web app and watch a `GET /api/tickets?status=...`
request appear immediately in the Network tab.

See [docs/defect-117.md](docs/defect-117.md) for the investigation and screenshots.

## Filtering

**Where:** `TicketQueries.ApplyFilters` (Infrastructure). Each filter that is sent adds one
`Where` before the count and paging, so the database does all the work and `totalCount` and the
pager match the filtered list.

**Rules:**

- `status`, `priority`, `categoryId`, `customerId` and `assignedAgentId` are exact matches.
- `unassignedOnly=true` keeps only tickets with no agent.
- `search` is trimmed, and ignored if blank. It matches the title, the reference or the
  customer name.
- All filters must match (AND).

**Testing:** the tests in `TicketQueriesFilterTests` run the query against an in-memory SQLite
database, so the filters really become SQL. With the original query put back, 12 of the 14 fail.

**Adding a filter:** add the field to `TicketQuery` and one more `if` in `ApplyFilters`.

See [docs/filtering.md](docs/filtering.md) for the before/after and the tests.

## Ticket escalation (Task 2)

**What it does:** a new ticket is triaged on create: its priority, due date and owner come from
the rules, and the response says why. A ticket can be escalated one level with a reason, and
each escalation is kept in a history. SLA status is derived, never stored, and filterable.

**Where:** small domain classes (`SlaPolicy`, `PriorityResolver`, `AgentSelector`,
`Ticket.Escalate`) that `TicketTriage` combines, so create and escalate share them. The windows
and thresholds are in the `Sla` section of `appsettings.json`.

**Testing:** boundary tests with a fixed clock, mocked handlers, SQLite for the history and the
SLA filter, and Vitest for the screens. The filter was also checked on the real SQL Server.

**Check it by hand:** raise a Security ticket for a Premium customer: it comes back Critical,
with an agent and a due date two hours away. Escalate any ticket below Critical and the history
appears under it.

See [docs/escalation.md](docs/escalation.md) for the rules, examples and screenshots.

## Design decisions

**The defect**

- **Chosen:** keep the filters that requests are made with separate from the text in the search
  box. Dropdowns apply straight away; the search text is applied 300 ms after typing stops.
- **Rejected:** adding every filter to the existing dependency array. The debounce wraps the
  whole effect, so dropdowns would wait 300 ms too, and the task asks for an immediate request.
- **Rejected:** a remount key or a forced reload. The task says that hides the problem instead of
  fixing the cause.

**Filtering**

- **Chosen:** one `ApplyFilters` method with a `Where` per filter that was sent, so the next
  filter is one more `if`.
- **Rejected:** filtering in C# after loading. It breaks the count and paging, and the task
  forbids it.
- **Rejected:** a specification class or a dynamic-LINQ library, which is more than seven filters
  need.
- **Chosen:** tests against in-memory SQLite, because mocks and EF's in-memory provider don't
  prove that the filters become real SQL.

**Escalation and triage**

- **Chosen:** the rules sit in small domain classes (`SlaPolicy`, `PriorityResolver`,
  `AgentSelector`, `Ticket.Escalate`) that create and escalate both use through `TicketTriage`.
  **Rejected:** writing them in the handlers, because they would be written twice.
- **Chosen:** the at-risk window comes from the config for the ticket's priority and customer
  tier. **Rejected:** due date minus created date, which is wrong after an escalation, and a
  stored window column, which needs a migration and a seeder change for something the config
  already answers.
- **Chosen:** history foreign keys use `Restrict`. **Rejected:** cascade, because an audit trail
  shouldn't disappear by accident.
- **Chosen:** the ticket being escalated is not counted against its own agent's limit.
  **Rejected:** counting it, because an agent at their limit would lose every ticket they hold.
- **Chosen:** equal agents are separated by the lowest id. **Rejected:** random or alphabetical,
  which are not repeatable or favour the same person.
- **Chosen:** the SLA filter compares dates with values worked out in C#. **Rejected:** date
  arithmetic in SQL, which differs between SQL Server and SQLite, and filtering in memory.

**Patterns I used**

- **Factory:** `SlaPolicyFactory` builds the policy once from configuration and fails at startup
  if a value is missing.
- **Domain service:** `AgentSelector` and `TicketTriage` hold rules that involve several
  aggregates and don't belong on one entity.
- **Composable filter steps:** `ApplyFilters` and `SlaStatusFilter` add one condition at a time,
  so a new filter is one more step.

## AI usage

I used Claude Code throughout this assignment. I'm new to .NET, so I used it to explain the
project and unfamiliar code, to help when I got stuck, to draft code and tests, and to help with
the wording and formatting of the documents.

I also ran the app and the tests myself to check what it produced.

## Known limitations

- If a slow request finishes after a newer one, it can overwrite the newer result. I left this out
  to keep the defect fix focused on its cause.
- Search uses `LIKE '%text%'`, which can't use an index. That's fine for this data; a large table
  would need full-text search.
- Search is case-insensitive on SQL Server because of its collation. The tests run on SQLite,
  which is case-sensitive, so they use exact case.
- The filter tests run on SQLite, not SQL Server itself. The SLA filter was also checked against
  the real SQL Server by hand.
- Open-ticket counts are read and then used, so two tickets raised at the same moment could both
  go to an agent who has room for only one.
- "Escalated by" is a text field until Task 3 takes it from the signed-in user.
- Changing the `Sla` section or a customer's tier changes the status of tickets that already
  exist, because the at-risk window is worked out from them.
- Ticket references are built from the highest ticket id + 1. SQL Server can skip about 1,000
  identity values after a restart, so references can jump (TCK-0041, then TCK-1003). If two
  tickets are created at the same moment they can compute the same reference, and the unique
  index makes one of them fail. A database sequence would fix both. This existed before my
  changes and is outside the tasks.
