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

I checked the SLA filter against the real SQL Server as well as SQLite, because the two can
translate the same query differently.

Details: `docs/escalation.md`.

## How I used AI

I used Claude Code to learn this codebase's .NET conventions and to draft code and tests in
small steps. I read each change, ran the tests, and checked the result before committing. For
example, I noticed the domain throws an ArgumentException that the error middleware doesn't map,
so a bad escalation reason could give a 500. I checked: the validator stops it first, and a
2-character reason returns 400.
