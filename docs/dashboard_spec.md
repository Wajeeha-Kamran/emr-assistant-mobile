# Dashboard screen — build specification

Agreed 16 Aug 2026. Replaces the earlier design, which showed a consultation
history list and a queue of unsigned notes.

---

## What this screen is for

One thing: **start a consultation**. Everything else on it exists only to catch
consultations that did not finish.

The app has a single purpose, so the dashboard is not a workspace or a home
feed. It is a launcher with a safety net.

---

## Why there is no history list

The backend has no endpoint that lists past consultations, and there is no
patient entity — a session has an id, a doctor, timestamps and a status,
nothing that identifies who it was with. A history list would be numbers and
times with no clinical meaning, and would need a new endpoint to produce it.

If it is ever wanted, a patient reference on the session comes first. It is not
needed for this project.

---

## Why there is no "unsigned notes" queue

The doctor signs the note before leaving the consultation. If something in it is
wrong they edit it first — that is what the draft is for. Signing is not a task
that should pile up, so the dashboard does not present it as one.

Unsigned notes still appear in the attention card, but as a **failure** to be
recovered, not as pending work. See Rule 1.

---

## Elements

### 1. Header

Greeting, the doctor's name, today's date.

Source: `GET /api/v1/auth/me`, already called after login.

### 2. Primary action — Start consultation

The dominant element on the screen. Full-width, gradient, unmissable.

On tap: `POST /api/v1/sessions` -> navigate to the record screen with the new
session id.

### 3. Attention card

Consultations that did not finish.

Source: `GET /api/v1/attention`. Loaded when the dashboard appears and on
pull-to-refresh. Not polled.

```json
{ "items": [...], "count": 1, "counts": { "SYNC_FAILED": 1, ... } }
```

Each item carries `reason` (what went wrong), `action` (what to offer),
`session_id`, and `note_id` where a note exists. Five reasons, one per stage of
the consultation:

| `reason` | Card text | Tap does |
|---|---|---|
| `TRANSCRIPT_FAILED` | "Transcription failed" | `POST /api/v1/sessions/{session_id}/transcript/retry`, then open the transcript screen in its waiting state |
| `TRANSCRIPT_STALLED` | "Transcription didn't finish" | same as above |
| `NOTE_NOT_GENERATED` | "Note not created" | `POST /api/v1/sessions/{session_id}/soap-notes/generate`, then open the note screen |
| `NOT_SIGNED` | "Not signed" | Open the note screen, editable |
| `SYNC_FAILED` | "Not sent to EMR" | Open the note screen with the sync banner and a **Retry** button |

Read `action` rather than switching on `reason` where possible — the backend
decides the recovery path so the client does not have to duplicate the rule.

Subtitle on each row: the consultation's time, from `created_at`.

**Empty state — do not hide the card.** When `count == 0`:

> **All consultations complete**
> Nothing needs your attention.

Keeping it visible makes the feature demonstrable without deliberately breaking
a sync, and it reassures rather than leaving a blank space.

**Error state.** If the request fails, show "Couldn't check" with a retry
affordance. Never show the empty state on a failed request — that claims
everything is fine when it is unknown.

---

## Rules this screen depends on

### Rule 1 — the note screen cannot be left unsigned

Back navigation is blocked while the note is a draft. Leaving requires signing
or an explicit discard.

The honest limitation, worth being able to state: this is a client rule, not a
database guarantee. If the app is killed mid-note, an unsigned note survives.
That is exactly what `NOT_SIGNED` recovers, so the two decisions fit together —
signing is required, and nothing is lost if that requirement is interrupted.

### Rule 2 — the sign screen reports the sync outcome

Signing queues a background push to the EMR; it does not complete
synchronously. After signing, poll `GET /api/v1/soap-notes/{note_id}/sync-status`:

- `SUCCESS` -> "Sent to EMR", the consultation is finished
- `FAILED` -> "Not sent to EMR" with **Retry**
- `PENDING` -> keep polling

A doctor who waits here never needs the dashboard card. The card is for the one
who did not wait.

---

## Why the attention card matters beyond convenience

Consultation audio is deleted only once its note is both signed and
successfully synced. A consultation stuck at any stage therefore keeps a
recording of a patient's voice on disk indefinitely.

The card is not only how the doctor finishes interrupted work — it is how the
system stops accumulating recordings. That is the answer if the supervisor asks
why a single-purpose app needs it.

---

## Assets still needed

- Hero illustration (the lungs image from the design)
- Serif face for "Your clinical day, clearly organized." — or substitute
  Poppins, which is already registered

---

## Not in scope for this screen

- Consultation history
- Consultation counts for the day
- Patient names or references (no patient entity exists)
- A session whose recording was started but never stopped. No audio was stored
  and no transcript exists, so there is nothing to resume and nothing on disk
  to clean up.
