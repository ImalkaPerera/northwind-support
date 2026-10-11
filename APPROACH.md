# Approach

How I worked on each task, and why. The details, screenshots and checks are in `docs/`, and the
decisions and limitations are in the README.

I took one small step at a time, checked it, and committed it before starting the next, so the
history follows the order I did things in.

## Task 1: the defect and the filtering

**The defect.** I reproduced it before reading any code. In the Network tab, changing Status sent
nothing, and typing in search then sent the old status. That told me the value was being saved
and the request wasn't being sent, so I followed it through the components to the effect that
loads the tickets. Its dependency array had three of the eleven filter fields, and a lint
suppression was hiding the warning. I fixed the hook rather than working around it, and wrote
tests that assert the request that is sent, which fail on the old code.

**The filtering.** The API read the filters and ignored them. I looked at where the query is
built and added one step that applies a `Where` per filter, before the count and the paging, so
the numbers match the filtered list. I tested it on a real SQLite database instead of mocks,
because mocks don't prove the filter becomes SQL.

Details: `docs/defect-117.md` and `docs/filtering.md`.

## Task 2: triage and escalation

The task said the challenge was where the rules go, so I started there. Each rule became a small
class with no database or web code in it, and one class, `TicketTriage`, puts them together so
creating and escalating share the same logic. That let me write and test the rules before there
was any endpoint or screen.

After that I went in this order, committing each step: the rules and their tests, the history
table and its migration, the create and escalate endpoints, the SLA filter in SQL, then the
screens, then the write-up.

The places where the spec left room, I decided and wrote down: how the at-risk window is
worked out, who keeps a ticket that is already at an agent's limit, how equal agents are split,
and what happens when nobody is eligible. Each is in the README under "Design decisions".

The SLA filter was checked against the real SQL Server as well as SQLite, because the two can
translate the same query differently.

Details: `docs/escalation.md`.

## Task 3: signing in

I split it into the backend first and the screens second, so the rules were in place before any
UI depended on them.

Before writing code I settled four decisions: the password hash lives on `Agent` (every user is
an agent and there are no roles), the token lasts 60 minutes (there are no refresh tokens, so a
short life limits a stolen token), the browser keeps it in `sessionStorage`, and every seeded
agent gets the same obviously-local dev password, hashed when the data is seeded.

I closed the API with one fallback policy instead of putting `[Authorize]` on each controller,
so a new endpoint is closed by default and can't be forgotten. Every login failure gives the
same 401 and message, so nobody can tell which part was wrong. Who escalated a ticket now comes
from the token, not from a text box, so it can't be faked.

On the web side, `client.ts` adds the token to every call and handles any 401 in one place, so
no page has to.

I checked it with tests that send real HTTP requests, and by hand: wrong password, signing in,
escalating as the signed-in agent, and a 401 in Postman without a token.

Details: `docs/authentication.md`.

## Task 4: Docker

I started from what the container needs to be safe and repeatable: a multi-stage build (the SDK
only to build, the smaller runtime image to run), a non-root user, and no secrets in the image.
The database password and the signing key arrive as environment variables when the container
starts. After a review I also kept `appsettings.Development.json` out of the image and published
the API on 127.0.0.1 only.

`docker compose up` starts SQL Server first, and the API waits until the database is healthy,
then migrates and seeds. I added a `/health` endpoint so Docker and a person can both see that
the API is up.

I checked it with `docker compose ps`, the health endpoint, `whoami` inside the container (it
prints `app`, not root), and the web app working against the container with `npm run api`
stopped.

Details: `docs/docker.md`.

## How I used AI

I used Claude Code as a pair programmer, one small step at a time. For each step I read the change,
ran the tests and tried it myself before committing. I didn't take everything it suggested: I kept
two indexes it called unused, because they speed up the foreign key checks. Testing also caught a
bug it introduced in the login handler. The details are in the README under "AI usage".