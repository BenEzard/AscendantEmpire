# Project Task Management (Planner API)

Work on Ascendant Empire is tracked in a separate application, **Planner**
(`P:\Development\Planner`), which exposes a REST API. This document explains how to add
and manage Ascendant Empire tasks through that API.

Planner is a .NET solution with a React frontend. It is **not** part of this repository —
this document only describes how to talk to it.

---

## 1. Connecting

| | |
|---|---|
| **Base URL (preferred)** | `https://localhost:7207` |
| **Base URL (plain HTTP)** | `http://localhost:5150` — issues a **307 redirect** to the HTTPS URL |
| **Auth** | None. The API is unauthenticated and local-only. |
| **Content type** | `application/json` |
| **API reference (dev only)** | `https://localhost:7207/scalar/v1` (OpenAPI doc at `/openapi/v1.json`) |

The dev certificate is self-signed, so certificate validation has to be skipped:

- **Git Bash / curl:** add `-k`
- **PowerShell:** call `curl.exe` explicitly (bare `curl` is an alias for
  `Invoke-WebRequest`, and Windows PowerShell 5.1 has no `-SkipCertificateCheck`)

### Is it running?

```bash
curl -s -k -o /dev/null -w "%{http_code}\n" https://localhost:7207/projects
```

`200` means the API is up. If it isn't, it can be started from `P:\Development\Planner`
with `dotnet run --project Planner.Api --launch-profile https`. **Do not start it without
being asked** — assume the user has it running, and say so if it isn't reachable.

---

## 2. The data model

```
Project  (Ascendant Empire, id 4)
  └── ProjectComponent   (Battle Map, Combat Mechanics, …)   ← has an abbreviation, e.g. "BM"
        ├── TaskGroup    (an optional named box inside the component, e.g. "Intelligence and Sightings")
        └── Task         (the unit of work, e.g. "BM-3")     ← what you normally create
                          ↳ may sit inside one TaskGroup of the same component, or loose
```

The important consequence: **a task is created against a Component, never against the
Project.** There is no `projectId` on a task — you must resolve the right
`projectComponentId` first.

A **TaskGroup** is a named box a component uses to arrange its own tasks — a group and the
tasks in it always belong to the same component, and a task is in at most one group. Groups
are optional; a task with `taskGroupId: null` is simply loose in the component's well.

> **Task items are gone.** Earlier versions of this document described `/task-items` and
> `/task-item-precedences` — a nested sub-step tree drawn on a canvas. Those endpoints no
> longer exist (they return `404`), and task groups replaced them.

### Task codes and numbering

Every task gets a human-readable code of the form `<component abbreviation>-<number>`,
e.g. `BM-3`.

- The number is **per-component** and **server-assigned** — never send it yourself.
- Numbers are **never reused**. Deleting or cancelling a task retires its number.
- The code is composed from the component's *current* abbreviation at read time, so
  renaming an abbreviation re-labels every task in that component immediately.
- Moving a task to a different component re-keys it against the destination's counter.

---

## 3. Ascendant Empire components

Project id **4**. These ids were correct at the time of writing — re-check with
`GET /project-components?projectId=4` if anything looks off, since ids are database
identities, not constants.

| Component id | Name | Abbreviation |
|---|---|---|
| 5 | Battle Map | `BM` |
| 6 | Combat Mechanics | `COMM` |
| 7 | City Development | `CD` |
| 8 | Campaign Map | `CM` |
| 9 | Diplomacy | `D` |
| 10 | Entity Setup | `ES` |
| 22 | Forces | `F` |
| 11 | General | `G` |
| 12 | Game Mechanism | `GM` |
| 13 | Game Setup | `GS` |
| 14 | Map Editor | `ME` |
| 23 | MatchMaker | `MM` |
| 15 | Resource System | `RS` |
| 24 | System | `S` |
| 16 | Technology | `T` |
| 17 | Unit Design | `UD` |

Use **General (11)** when a task doesn't clearly belong to a specific component
(cross-cutting refactors, tidy-ups, infrastructure).

> Component **22 was renamed** from "Troop Management" (`TM`) to "Forces" (`F`). Because a
> task code is composed from the component's *current* abbreviation at read time, every task
> that used to read `TM-n` now reads `F-n`. The old `TM-` codes appear in commit messages and
> in older documents and refer to the same tasks.

---

## 4. Enumerations

All three are sent and returned as **integers**, not strings.

### `status` — `ProjectTaskStatus`

| Value | Meaning |
|---|---|
| `0` | Not Started |
| `1` | Underway |
| `2` | Completed |
| `3` | Cancelled |

Omitted on create → `0` (Not Started).

### `priority` — `TaskPriority`

| Value | Meaning |
|---|---|
| `0` | Low |
| `1` | Medium |
| `2` | High |

Omitted or `null` on create → **`1` (Medium)**, deliberately *not* the enum's zero
member. On **update**, `null` means "leave the priority as it is" (a task always has one,
so `null` can only mean "not supplied").

### `createdWithin` — `TaskCreatedDateRange`

A **read filter only**; it is never stored on a task.

| Value | Meaning |
|---|---|
| `0` | All Time — no narrowing by age. The resting state. |
| `1` | Today — created at or after local midnight this morning. |
| `2` | Yesterday — created during the local day before today, and no later. |
| `3` | Last 30 Days — today plus the 29 days before it. |

Unlike `statuses` and `priorities` this is a **single** value, not a repeated parameter: the
windows nest inside one another, so asking for several could only ever mean the widest one.
Windows are whole **local** days on the server, not rolling 24-hour spans. Omitting the
parameter binds to `0`, which is why All Time is the zero member.

### `colour`

An optional highlight colour: a full `"#rrggbb"` hex string (e.g. `"#1d4ed8"`), or `null`
for no colour. Unlike priority, `null` is a **real value** here — passing it clears the
colour. Anything that isn't a full six-digit hex string is a `400`.

---

## 5. Adding a task

### Single task

`POST /tasks` → **201 Created**, returning the created task including its new `id`,
`taskNumber` and `taskCode`.

```bash
curl -k -X POST https://localhost:7207/tasks \
  -H "Content-Type: application/json" \
  -d '{
    "projectComponentId": 11,
    "title": "Attach permissions to a User",
    "description": "# Overview\nCreate permissions that can be attached to a user account.\n\n# Detail\n* create a game\n* join a game",
    "taskOrdering": 0,
    "status": 0,
    "priority": 1,
    "colour": null
  }'
```

#### Request fields

| Field | Required | Notes |
|---|---|---|
| `projectComponentId` | **yes** | Must be an existing, live component. `404` otherwise. |
| `title` | **yes** | Non-empty, **max 200 characters**. |
| `description` | no | Omitted, `null`, empty and whitespace-only are all treated alike and stored as `null`. Otherwise stored exactly as sent — **not trimmed**, because leading whitespace is meaningful in Markdown. No length cap; multi-line and Markdown-style content is normal and expected. |
| `taskOrdering` | no | Manual ordering within the component. Must be `>= 0`. Defaults to `0`. |
| `taskGroupId` | no | Put the new task straight into a group. An unknown or soft-deleted group is a `404`; a live group belonging to a **different component** is a `400`. `null` (the default) creates it loose. The task is appended to the end of the group. **Ignored by `/tasks/bulk`** — see below. |
| `status` | no | Defaults to `0` (Not Started). |
| `priority` | no | Defaults to `1` (Medium). |
| `colour` | no | `"#rrggbb"` or `null`. |

Do **not** send `id`, `taskNumber`, `taskCode`, `createdAt`, `updatedAt` or `completedAt`
on create — they are all server-assigned.

#### Response

```json
{
  "id": 230,
  "projectComponentId": 11,
  "taskNumber": 132,
  "taskCode": "G-132",
  "title": "Attach permissions to a User",
  "description": "# Overview\n…",
  "taskOrdering": 0,
  "taskGroupId": null,
  "taskGroupOrderNumber": 0,
  "status": 0,
  "priority": 1,
  "colour": null,
  "createdAt": "2026-07-28T08:52:39.6704978+00:00",
  "updatedAt": "2026-07-28T08:52:39.6704978+00:00",
  "completedAt": null
}
```

### Many tasks at once

`POST /tasks/bulk` → **201 Created**, returning the created tasks (with their new ids) as
`{ "tasks": [ … ] }`.

Takes a **JSON array** of exactly the same objects as the single create. Each item carries
its own `projectComponentId`, so one batch can span several components. The insert is
**atomic and all-or-nothing** — one bad item fails the whole batch. Maximum **500** tasks
per batch.

```bash
curl -k -X POST https://localhost:7207/tasks/bulk \
  -H "Content-Type: application/json" \
  -d '[
    { "projectComponentId": 5,  "title": "Fog of war on the battle map", "description": "…", "priority": 2 },
    { "projectComponentId": 14, "title": "Undo/redo in the map editor",  "description": "…" },
    { "projectComponentId": 5,  "title": "Unit stacking rules",          "description": "…" }
  ]'
```

Each component's numbers are claimed as one contiguous block, so codes never interleave.

**Prefer bulk whenever adding more than one task** — it is one round-trip and one
transaction.

> **Bulk ignores `taskGroupId`.** The field is accepted on the way in (it is the same DTO)
> but the bulk handler does not copy it onto the created rows, so every task in a batch is
> created loose, silently. To put a batch into a group, create them in bulk and then follow
> up with one `PUT /tasks/{id}/group` per task.

---

## 6. Full endpoint reference

Routes are kebab-cased from the controller names.

### Projects — `/projects`

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/projects` | All projects. Add `?id=4` for one project, `?includeDeleted=true` to include soft-deleted. |
| `POST` | `/projects` | Create (`name`, `description`). → 201 |
| `PUT` | `/projects/{id}` | Full replacement (`name`, `description`, `lastOpenedAt`, `closedAt`). |
| `POST` | `/projects/{id}/open` | Stamp `lastOpenedAt` with server time. |
| `DELETE` | `/projects/{id}` | Soft-delete. → 204 |

### Components — `/project-components`

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/project-components?projectId=4` | All components of a project (empty array, not 404, when there are none). Optional `&includeDeleted=true`. |
| `GET` | `/project-components/{id}` | One component. Optional `?includeDeleted=true`. |
| `POST` | `/project-components` | Create (`projectId`, `name`, `abbreviation`, `componentOrdering`). |
| `PUT` | `/project-components/{id}` | Full replacement (`name`, `abbreviation`, `componentOrdering`). Cannot move between projects. |
| `DELETE` | `/project-components/{id}` | Soft-delete. → 204 |

`abbreviation` is **required**, **uppercase letters and digits only**, and **unique within its
project** — two different projects may each have a component abbreviated `API`, but one project
may not have two. The uniqueness is DB-enforced across **every** row, soft-deleted ones
included, so a deleted component's abbreviation cannot be reused in the same project. A clash
is a `409`. Renaming an abbreviation **re-labels every task in the component immediately**,
since codes are composed at read time.

### Project settings — `/project-settings`

The defaults a project's board opens with in the Planner UI — its saved filter state. **Nothing
in the task workflow needs these**; they are listed for completeness. Addressed by project id,
since a project has at most one set.

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/project-settings?projectId=4` | The project's settings. A project that has never saved any gets the built-in defaults with `isSaved: false` — a `200`, not a `404`. A `404` means the *project* doesn't exist. |
| `PUT` | `/project-settings/{projectId}` | Save the settings. `201` on the first save, `200` thereafter. |
| `DELETE` | `/project-settings/{projectId}` | Discard the saved settings, so the project falls back to the defaults. → 204 |

The body mirrors the `GET /tasks` filters: `filterStatuses`, `filterPriorities`,
`filterColours`, `filterNoColour`, `filterCreatedWithin`, `filterSearchTitle`,
`filterSearchDescription`, plus `filterShowEmptyComponents` and `filterApplyOtherFilters`.

### Tasks — `/tasks`

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/tasks?projectComponentId={id}` | A component's tasks. Optional `&includeDeleted=true`. See the filter table below. |
| `GET` | `/tasks/status-counts?projectId=4` | Per-status counts across the whole project. Statuses with no tasks are absent rather than zero. |
| `GET` | `/tasks/{id}` | One task. Optional `?includeDeleted=true`. |
| `POST` | `/tasks` | Create one. → 201 |
| `POST` | `/tasks/bulk` | Create many, atomically. → 201 |
| `PUT` | `/tasks/{id}` | Full replacement — see below. |
| `PUT` | `/tasks/{id}/status` | `{ "status": 1 }` |
| `PUT` | `/tasks/{id}/priority` | `{ "priority": 2 }` |
| `PUT` | `/tasks/{id}/colour` | `{ "colour": "#1d4ed8" }` or `{ "colour": null }` to clear |
| `PUT` | `/tasks/{id}/group` | Move into, within, or out of a group — see [Task groups](#task-groups--task-groups). |
| `DELETE` | `/tasks/{id}` | Soft-delete (stamps `deletedAt`; the row survives). → 204 |

#### `GET /tasks` filters

All of them **AND** together, and all are optional.

| Parameter | Form | Notes |
|---|---|---|
| `includeDeleted` | `bool` | Default false. |
| `statuses` | repeated | `&statuses=0&statuses=1`. Omitted or empty means every status. |
| `priorities` | repeated | `&priorities=2&priorities=0`. Omitted or empty means every priority. |
| `search` | string | Case-insensitive `like '%term%'`. Empty or whitespace means "no search". **Max 500 characters.** |
| `searchTitle` | `bool` | Default **true**. Whether `search` looks at the title. Ignored with no term. |
| `searchDescription` | `bool` | Default **true**. Whether `search` looks at the description. Setting both this and `searchTitle` to false leaves the term nowhere to match and returns an empty collection. |
| `createdWithin` | single int | `TaskCreatedDateRange` — see §4. Omitted means `0` (All Time). |
| `colours` | repeated | `"#rrggbb"`, matched case-insensitively. **The `#` must be percent-encoded** as `%23`, or everything after it is read as the URL fragment: `&colours=%23ef4444`. |
| `noColour` | `bool` | Default false. Alone it narrows to *only* uncoloured tasks; alongside `colours` it widens to "one of those colours, or none". A separate parameter because null can't be an element of `colours`. |

An unrecognised status, priority or creation-date value is a `400`, not an empty list. So is a
malformed colour or an over-long search term.

#### `PUT /tasks/{id}` is a **full replacement**

Send the complete intended state, not just the changed fields — anything omitted is
overwritten with its default. Body: `projectComponentId`, `title`, `description`,
`taskOrdering`, `status`, `priority`, `colour`, `completedAt`.

There is deliberately **no `taskGroupId`** in that list: this endpoint cannot change group
membership, and does not blank it either. Use `PUT /tasks/{id}/group`.

Supplying a **different** `projectComponentId` **moves the task to that component** and
re-keys its code — and, as a side effect, **ungroups it**, since its old group belongs to the
component it just left.

For a one-field change, use the dedicated lightweight endpoints
(`/status`, `/priority`, `/colour`) instead — they don't round-trip the whole task and
can't accidentally blank a field.

### Task groups — `/task-groups`

Named boxes a component uses to arrange its own tasks.

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/task-groups?projectComponentId={id}` | A component's groups, **oldest first**. Empty array, not 404, when there are none. Optional `&includeDeleted=true`. |
| `GET` | `/task-groups/{id}` | One group. Optional `?includeDeleted=true`. |
| `POST` | `/task-groups` | Create (`projectComponentId`, `title`). → 201 |
| `PUT` | `/task-groups/{id}` | Rename. `title` is the **only** mutable field. |
| `DELETE` | `/task-groups/{id}` | Soft-delete the group. → 204 |

A group's response carries only `id`, `title`, `projectComponentId`, `createdAt` and
`updatedAt` — **not its tasks**. `GET /tasks` already returns the whole component's tasks and
each one carries its own `taskGroupId`, so fill the boxes by matching on that rather than
fetching the same rows twice.

A group cannot be moved to another component, and membership is never changed from this side —
it is always changed from the task, below.

**Deleting a group deletes no tasks.** Only the box goes; every task inside it is released back
into the component's well in the same operation.

#### Moving a task — `PUT /tasks/{id}/group`

```json
{ "taskGroupId": 1, "taskGroupOrderNumber": 0 }
```

One endpoint covers all four gestures: joining a group, moving between groups, reordering
within a group, and leaving one.

| Field | Notes |
|---|---|
| `taskGroupId` | The destination group, or **`null` to ungroup** — that's a legitimate value, not an omission. |
| `taskGroupOrderNumber` | Zero-based position **among the other tasks** in the destination. Must be `>= 0` (a negative is a `400`), and is otherwise **clamped**, so a large number simply means "put it last". |

The destination group must exist, be live, and **belong to the same component as the task** —
that last one is a `400` rather than a `404`, since both rows exist and just don't belong
together.

Order numbers are maintained as a dense `0..n-1` sequence: both the destination and the group
the task left are renumbered in full, so gaps never accumulate. Ungrouping resets
`taskGroupOrderNumber` to `0`, since position is meaningless outside a group.

#### Group membership and the other task endpoints

- `PUT /tasks/{id}` (full replacement) has **no `taskGroupId` field** and deliberately leaves
  membership alone — with one exception: supplying a different `projectComponentId` **moves the
  task to that component and ungroups it**, because a group belongs to one component and a
  foreign task has no place in its box.
- `POST /tasks` accepts `taskGroupId`; `POST /tasks/bulk` silently ignores it.

---

## 7. Status codes and errors

| Code | Meaning |
|---|---|
| `200` | Success with a body |
| `201` | Created; the created entity is returned |
| `204` | Success, no content (deletes) |
| `400` | Validation error |
| `404` | The entity — or a referenced parent, e.g. the component — doesn't exist or is soft-deleted |
| `409` | Uniqueness conflict — a duplicate component abbreviation. Only the component endpoints raise it. |
| `500` | Server error |

Every non-success response uses the same shape, **an array of messages**:

```json
{ "errors": ["Task Title is required.", "Task Ordering cannot be negative."] }
```

Validation failures are aggregated, so one response can list several problems at once. On
a bulk create, failures are aggregated across the whole batch.

### Soft deletes

Projects, components, tasks and task groups are all **soft-deleted** — a `deletedAt`
timestamp is stamped and the row stays. They're excluded from reads by default; pass
`?includeDeleted=true` to see them.

---

## 8. Adding a task: the workflow to follow

1. **Check the API is reachable** (`GET /projects`). If it isn't, say so — don't try to
   start it unless asked.
2. **Pick the component.** Match the work to a component from the table in §3; use
   **General (11)** if nothing fits. If you're unsure which component is right, **ask** —
   a task in the wrong component gets the wrong code, and codes are never reissued.
3. **Confirm the component id** with `GET /project-components?projectId=4` if the ids in
   this document look stale.
4. **Write the task.** A short `title` (≤ 200 chars) and a `description` carrying the real
   content. Existing tasks use Markdown-ish headed sections, and matching that is worth
   doing:

   ```
   # Overview
   One or two sentences on what this is and why.

   # Detail
   * the specifics
   * bullet by bullet
   ```

   Some older tasks instead use `Current Functionality` / `Proposed Functionality`
   headings for changes to existing behaviour — either is fine.
5. **Set `priority` deliberately** if you have an opinion; otherwise omit it and let it
   default to Medium.
6. **`POST /tasks`** for one, **`POST /tasks/bulk`** for several.
7. **Check for a matching task group.** `GET /task-groups?projectComponentId={id}` — if the
   component has a group that plainly covers the work (a group named after the feature you're
   adding tasks for, say), put the tasks in it. After a bulk create that means one
   `PUT /tasks/{id}/group` per task, because bulk ignores `taskGroupId`. Don't create a new
   group without asking.
8. **Report the returned `taskCode`** (e.g. `G-133`) back to the user — that's the handle
   they'll use to refer to the task.

### Things not to do

- Don't invent or send `taskNumber` / `taskCode` — the server assigns them.
- Don't use `PUT /tasks/{id}` to change one field; use the `/status`, `/priority` or
  `/colour` endpoints.
- Don't create a new component to hold a task without asking. An abbreviation is unique
  within the project, can never be reused once taken (even after the component is deleted),
  and prefixes every task code in the component — that's not a reversible decision to take
  unilaterally.
- Don't create a new task group without asking either. Putting tasks into an *existing* group
  that clearly fits is fine and reversible; inventing the organising scheme is not your call.
- Don't send `taskGroupId` to `/tasks/bulk` and assume it took. It is accepted and ignored.
- Don't re-create a task that failed validation without fixing the reported error; the
  `errors` array says exactly what was wrong.
