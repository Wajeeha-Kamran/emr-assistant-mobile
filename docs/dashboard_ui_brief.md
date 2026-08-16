# Dashboard screen — UI brief

Everything needed to draw or generate the screen. Behaviour and API contracts
are in `dashboard_spec.md`; this file is the visual half.

Frame: **mobile portrait, 420 x 880**, matching the login and splash screens.

---

## 1. Design tokens — use these exact values

Already defined in `Resources/Styles/Brand.xaml`:

| Token | Hex | Used for |
|---|---|---|
| `BrandDeep` | `#2A0A4A` | Header gradient start |
| `BrandPurple` | `#5B2E9D` | Header gradient end, button start |
| `BrandPurpleLight` | `#8B5CD6` | Button end, accents |
| `BrandAccent` | `#A77BF3` | Highlights |
| `TextPrimary` | `#241245` | Headings, row titles |
| `TextSecondary` | `#6B6480` | Body, row subtitles |
| `TextMuted` | `#9A94A8` | Footnotes |
| `Surface` | `#FFFFFF` | Cards |
| `SurfaceMuted` | `#F4F2F9` | Page background |
| `FieldBorder` | `#DCD7E8` | Card borders, dividers |
| `SecureGreen` | `#1F8A5B` | All-clear state |
| `SuccessBg` | `#D1E7DD` | All-clear icon tile |
| `Danger` | `#8C1D18` | Sync failure |
| `DangerBg` | `#F9DEDC` | Sync failure icon tile |

**Two new tokens to add** for the transcription failures, which are neither
fatal nor fine:

| Token | Hex |
|---|---|
| `Warning` | `#8A5A00` |
| `WarningBg` | `#FDF0D5` |

**Type.** Poppins SemiBold for headings, Inter for everything read as content.
Both are already registered.

---

## 2. Layout, top to bottom

Page background `SurfaceMuted`. Horizontal padding **20** throughout.

### A. Header block

A deep purple panel bleeding to the top edge of the screen.

- Fill: linear gradient `BrandDeep` -> `BrandPurple`, top-left to bottom-right
- Height **230**, bottom corners rounded **28**, top corners square
- Inside, padding 20 sides / 54 top (below the status bar)

Contents:

| Element | Style |
|---|---|
| `logo_mark.png`, 26x26, top-left | existing asset |
| "EMR Assistant" beside it | Poppins SemiBold 15, white |
| Avatar circle top-right, 38x38 | white at 18% opacity, doctor's initials in Inter SemiBold 14, white |
| "Good morning," | Inter Regular 15, white 75% |
| "Dr. Wajeeha Kamran" | Poppins SemiBold 26, white |
| "Saturday, 16 August" | Inter Regular 13, white 60% |

Greeting block sits 22 below the logo row.

### B. Primary action card — overlaps the header

A white card that rises over the header's bottom edge. This overlap is what
makes it the focus of the screen.

- Width: full, inside the 20 padding
- Top margin **-38** so it sits across the header edge
- Fill `Surface`, corner radius **24**
- Shadow: `#26241245`, offset 0,10, radius 24
- Padding 22

Contents, stacked:

1. "Start a consultation" — Poppins SemiBold 19, `TextPrimary`
2. "Record the visit. The note writes itself." — Inter Regular 13.5, `TextSecondary`, 4 below
3. Button, 18 below: full width, height **54**, radius **14**, gradient
   `BrandPurple` -> `BrandPurpleLight` (the existing `PrimaryButton` style),
   label "Start consultation" in Inter SemiBold 16, white, with a 18px
   microphone glyph to the left of the text

Optional: the lungs illustration at 30% width, right-aligned behind the text at
12% opacity. Only if it does not compete with the button.

### C. Section label

24 below the card, left aligned:

> **Needs attention**

Inter SemiBold 13, `TextSecondary`, letter spacing 0.4.

### D. Attention card

- Fill `Surface`, radius **20**, border 1px `FieldBorder`
- Shadow: `#14241245`, offset 0,6, radius 16
- Padding 0 — rows manage their own padding

---

## 3. Attention row anatomy

Each row: height **72**, padding 16 sides.

```
[ 44x44 icon tile ]   Title                    [ action pill ]
                      Subtitle
──────────────────────────────────────────────────────────────
```

- **Icon tile**: 44x44, radius 12, filled with the reason's background colour,
  glyph 20px in the reason's foreground colour, centred
- **Title**: Inter SemiBold 14.5, `TextPrimary`
- **Subtitle**: Inter Regular 12.5, `TextSecondary`, 2 below the title
- **Action pill**: height 32, radius 16, horizontal padding 14, background
  `SurfaceMuted`, border 1px `FieldBorder`, label Inter SemiBold 12.5 in
  `BrandPurple`
- **Divider**: 1px `#EFECF5`, inset 76 from the left, between rows only

### The five row types

| `reason` | Icon tile | Glyph | Title | Action pill |
|---|---|---|---|---|
| `TRANSCRIPT_FAILED` | `WarningBg` / `Warning` | waveform | Transcription failed | Retry |
| `TRANSCRIPT_STALLED` | `WarningBg` / `Warning` | waveform | Transcription didn't finish | Retry |
| `NOTE_NOT_GENERATED` | `#EDE6FA` / `BrandPurple` | document | Note not created | Create |
| `NOT_SIGNED` | `#EDE6FA` / `BrandPurple` | pen | Not signed | Open |
| `SYNC_FAILED` | `DangerBg` / `Danger` | cloud with a slash | Not sent to EMR | Retry |

Subtitle on every row is the consultation time, from `created_at`:
"Today, 2:15 PM" / "Yesterday, 9:40 AM" / "14 Aug, 4:05 PM".

Glyphs needed as `PathGeometry` resources in `Brand.xaml` — the five existing
icons do not cover these: **waveform**, **document**, **pen**, **cloud-slash**,
**microphone**, **check**.

---

## 4. The four states

The attention card is always present. Only its contents change.

### Populated

Rows as above. Above the first row, a 40-high strip with a count:

> **2 consultations need attention**

Inter SemiBold 13, `TextPrimary`, padding 16, with a divider beneath.

### Empty — `count == 0`

Centred, padding 28 vertical:

- 56x56 circle, fill `SuccessBg`, 24px check glyph in `SecureGreen`
- "All consultations complete" — Poppins SemiBold 16, `TextPrimary`, 14 below
- "Nothing needs your attention." — Inter Regular 13, `TextSecondary`, 4 below

**Do not hide the card in this state.** A visible all-clear proves the feature
exists during a demo without deliberately breaking a sync.

### Loading

The existing `WaveformLoader` at 60% scale, centred, with
"Checking your consultations" in Inter Regular 13, `TextSecondary` below it.
Card padding 28 vertical, same height as the empty state so the layout does not
jump.

### Error

- 56x56 circle, fill `WarningBg`, 24px exclamation glyph in `Warning`
- "Couldn't check" — Poppins SemiBold 16
- "Your consultations may need attention." — Inter Regular 13, `TextSecondary`
- Ghost button "Try again" — Inter SemiBold 13.5, `BrandPurpleLight`

Never show the empty state on a failed request. Claiming everything is fine
when it is unknown is the one wrong answer this screen can give.

---

## 5. Footer

24 below the attention card, centred, `TextMuted`:

> 🛡 No patient identifiers are stored

12px Inter Regular with a 13px shield glyph in `SecureGreen`, matching the
notice already on the login screen.

---

## 6. What must not appear

- Consultation history or a list of past visits
- "Consultations today" counters or any statistics
- Patient names, ages, or references — no patient entity exists in the backend
- Search, filters, tabs, or a bottom navigation bar

The screen has exactly two jobs: start a consultation, and surface the ones
that did not finish.

---

## 7. One-paragraph prompt, if generating the design

> A mobile app dashboard screen, 420x880, for a clinical documentation app
> called EMR Assistant. Deep purple gradient header (#2A0A4A to #5B2E9D) with
> rounded bottom corners, containing a small logo, the greeting "Good morning,"
> and "Dr. Wajeeha Kamran" in white, with the date beneath. A white rounded
> card overlaps the bottom edge of the header, headed "Start a consultation"
> with the line "Record the visit. The note writes itself." and a full-width
> purple gradient button labelled "Start consultation" with a microphone icon.
> Below, a small label "Needs attention", then a white rounded card listing two
> rows: an amber waveform icon with "Transcription failed" and a "Retry" pill,
> and a red cloud-with-slash icon with "Not sent to EMR" and a "Retry" pill,
> each with a small grey timestamp beneath the title. Light lavender page
> background (#F4F2F9), Poppins for headings, Inter for body text, generous
> whitespace, soft shadows, clean and calm medical software. No navigation bar,
> no statistics, no patient names.
