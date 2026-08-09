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
        └── Task         (the unit of work, e.g. "BM-3")     ← what you normally create
              └── TaskItem  (optional sub-steps, a nested tree on a canvas)
                    └── TaskItemPrecedence  (arrows between items on the canvas)
```

The important consequence: **a task is created against a Component, never against the
Project.** There is no `projectId` on a task — you must resolve the right
`projectComponentId` first.

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
| 11 | General | `G` |
| 12 | Game Mechanism | `GM` |
| 13 | Game Setup | `GS` |
| 14 | Map Editor | `ME` |
| 15 | Resource System | `RS` |
| 16 | Technology | `T` |
| 17 | Unit Design | `UD` |

Use **General (11)** when a task doesn't clearly belong to a specific component
(cross-cutting refactors, tidy-ups, infrastructure).

---

## 4. Enumerations

Both are sent and returned as **integers**, not strings.

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
| `description` | **yes** | Must be present, but **may be an empty string**. No length cap — free-form text; multi-line and Markdown-style content is normal and expected. |
| `taskOrdering` | no | Manual ordering within the component. Must be `>= 0`. Defaults to `0`. |
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
| `GET` | `/project-components?projectId=4` | All components of a project (empty array, not 404, when there are none). |
| `GET` | `/project-components/{id}` | One component. |
| `POST` | `/project-components` | Create (`projectId`, `name`, `abbreviation`, `componentOrdering`). |
| `PUT` | `/project-components/{id}` | Full replacement (`name`, `abbreviation`, `componentOrdering`). Cannot move between projects. |
| `DELETE` | `/project-components/{id}` | Soft-delete. → 204 |

`abbreviation` is **required**, **uppercase letters and digits only**, and **unique across
all components** (DB-enforced; a clash is a `409`).

### Tasks — `/tasks`

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/tasks?projectComponentId={id}` | A component's tasks. Optional `&includeDeleted=true`. |
| `GET` | `/tasks?projectComponentId={id}&statuses=0&statuses=1` | Filter by status — repeat the parameter once per value. |
| `GET` | `/tasks?projectComponentId={id}&priorities=2&priorities=0` | Filter by priority, same repeated form. Combines with `statuses` as an **AND**. |
| `GET` | `/tasks/status-counts?projectId=4` | Per-status counts across the whole project. Statuses with no tasks are absent rather than zero. |
| `GET` | `/tasks/{id}` | One task. Optional `?includeDeleted=true`. |
| `POST` | `/tasks` | Create one. → 201 |
| `POST` | `/tasks/bulk` | Create many, atomically. → 201 |
| `PUT` | `/tasks/{id}` | Full replacement — see below. |
| `PUT` | `/tasks/{id}/status` | `{ "status": 1 }` |
| `PUT` | `/tasks/{id}/priority` | `{ "priority": 2 }` |
| `PUT` | `/tasks/{id}/colour` | `{ "colour": "#1d4ed8" }` or `{ "colour": null }` to clear |
| `DELETE` | `/tasks/{id}` | Soft-delete (stamps `deletedAt`; the row survives). → 204 |

An unrecognised status or priority in a filter is a `400`, not an empty list.

#### `PUT /tasks/{id}` is a **full replacement**

Send the complete intended state, not just the changed fields — anything omitted is
overwritten with its default. Body: `projectComponentId`, `title`, `description`,
`taskOrdering`, `status`, `priority`, `colour`, `completedAt`.

Supplying a **different** `projectComponentId` **moves the task to that component** and
re-keys its code.

For a one-field change, use the dedicated lightweight endpoints
(`/status`, `/priority`, `/colour`) instead — they don't round-trip the whole task and
can't accidentally blank a field.

### Task items — `/task-items`

Optional sub-steps within a task, forming a nested tree laid out on a canvas.

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/task-items?taskId={id}` | The task's whole item tree, returned **flat** — rebuild the hierarchy from each item's `parentTaskItemId`. |
| `GET` | `/task-items/{id}` | One item. |
| `POST` | `/task-items` | Create (`taskId`, optional `parentTaskItemId`, `title`, `description`, `status`, `siblingOrdering`). |
| `PUT` | `/task-items/{id}` | Full replacement (`title`, `description`, `status`, `siblingOrdering`, `completedAt`). Cannot re-parent or move to another task. |
| `PUT` | `/task-items/{id}/position` | `{ "canvasX": 120, "canvasY": 340 }` |
| `DELETE` | `/task-items/{id}` | Soft-delete. → 204 |

Task items use a **different** status enum, `TaskItemStatus`:
`0` Not Started, `1` Design, `2` Development, `3` Testing, `4` Completed, `5` Cancelled.

A `parentTaskItemId` must belong to the **same task**, or it's a `400`.

### Task item precedences — `/task-item-precedences`

The arrows drawn between a task's items on the canvas.

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/task-item-precedences?taskId={id}` | All links for a task. |
| `POST` | `/task-item-precedences` | Create (`fromTaskItemId`, `toTaskItemId`, `fromSide`, `toSide`). |
| `DELETE` | `/task-item-precedences/{id}` | **Hard** delete — the row is removed. → 204 |

Sides are the lowercase strings `"top"`, `"bottom"`, `"left"`, `"right"`. Both endpoints
must belong to the same task. A duplicate link is a `409`.

---

## 7. Status codes and errors

| Code | Meaning |
|---|---|
| `200` | Success with a body |
| `201` | Created; the created entity is returned |
| `204` | Success, no content (deletes) |
| `400` | Validation error |
| `404` | The entity — or a referenced parent, e.g. the component — doesn't exist or is soft-deleted |
| `409` | Uniqueness conflict (duplicate component abbreviation, duplicate precedence link) |
| `500` | Server error |

Every non-success response uses the same shape, **an array of messages**:

```json
{ "errors": ["Task Title is required.", "Task Ordering cannot be negative."] }
```

Validation failures are aggregated, so one response can list several problems at once. On
a bulk create, failures are aggregated across the whole batch.

### Soft deletes

Projects, components, tasks and task items are all **soft-deleted** — a `deletedAt`
timestamp is stamped and the row stays. They're excluded from reads by default; pass
`?includeDeleted=true` to see them. Precedence links are the one exception: they are hard
deleted.

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
7. **Report the returned `taskCode`** (e.g. `G-133`) back to the user — that's the handle
   they'll use to refer to the task.

### Things not to do

- Don't invent or send `taskNumber` / `taskCode` — the server assigns them.
- Don't use `PUT /tasks/{id}` to change one field; use the `/status`, `/priority` or
  `/colour` endpoints.
- Don't create a new component to hold a task without asking. Abbreviations are globally
  unique and prefix every task code in the component — that's not a reversible decision to
  take unilaterally.
- Don't re-create a task that failed validation without fixing the reported error; the
  `errors` array says exactly what was wrong.
