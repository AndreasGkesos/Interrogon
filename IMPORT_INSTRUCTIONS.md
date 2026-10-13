# Importing questions into Interrogon

This document explains how to bulk-import new questions into Interrogon using an AI agent (Claude Code, or any other agent capable of reading files, reasoning about their contents, and writing to a LiteDB database), and contains the exact instructions to hand that agent to do the job correctly and consistently.

## What this is for

Interrogon's database doesn't have a bulk-import feature in the app UI — adding questions one at a time through the "Add question" form works fine for occasional additions, but isn't practical for importing a batch of interview prep material, old notes, or chat transcripts that contain many questions at once.

Instead, the workflow is:

1. Drop your raw source material into the `ImportData/` folder — this can be markdown notes, chat exports, plain text, anything with recognizable question-and-answer content in it.
2. Hand an AI agent the instructions below (the section titled **"Agent instructions"**) along with access to this repo.
3. The agent reads through everything in `ImportData/`, compares it against what's already in the database, and adds new questions directly to the LiteDB file — assigning a Type, Category, and Tags to each one.
4. The agent writes a short report of what it did, so you can review anything it flagged rather than decided on its own (possible duplicates, quality issues, ambiguous categorization).

## Why an agent does this instead of a script

A simple import script can't do the judgment calls this requires: splitting a messy document into individual questions, recognizing that "EF Core" and "Entity Framework Core" are the same tag, telling a duplicate apart from a related-but-distinct question, or noticing that a code snippet looks broken. An AI agent can read for meaning instead of just pattern-matching text, which is what this process depends on.

## How to run an import

1. Put new source files into `ImportData/`.
2. Open a session with your agent of choice in this repo.
3. Paste the entire "Agent instructions" section below into the conversation, then ask it to run the import.
4. When it's done, open `ImportData/reports/import-<date>.md` and review anything flagged — likely duplicates, content quality issues, and ambiguous category/tag calls all need a human decision, the agent deliberately won't make those calls for you.
5. Re-running an import later (e.g. after adding more files to `ImportData/`) is safe — the agent checks against the current database contents each time, so previously imported questions won't be duplicated.

---

## Agent instructions

*(Everything below this line is meant to be copied as-is and given to the agent doing the import.)*

You are importing new questions into Interrogon's LiteDB database from the contents of `ImportData/`. Follow this process exactly, and do not skip the reporting step — this import may be one of several run over time by different sessions that don't share memory of earlier decisions, so the report is the only record of what happened and why.

### Before you start

- Confirm which LiteDB file you are about to write to — the real app data path (`%LOCALAPPDATA%\Interrogon\`) or a local dev-time copy — before touching anything. Do not assume; ask if it isn't obvious from context.
- **You do not need this source repo's `Interrogon.Core` project to do this.** LiteDB is schema-less — open the database with LiteDB's untyped `GetCollection("questions")` / `BsonDocument` API (not `GetCollection<Question>`). This matters because an import may be run against a machine that only has the installed app (e.g. a friend given the MSI and this instructions file), with no access to this repo at all — the two scenarios below cover both cases; pick whichever matches what you actually have access to:
  - **If you have this source repo available**: you may reference `Interrogon.Core.csproj` directly and use its typed `Question`/`Category`/`Tag`/`QuestionType` model classes instead of raw `BsonDocument`s, the way prior import runs in this repo's history did (a small scratch console project referencing `Interrogon.Core`, built and run with `dotnet run`) — this is a convenience, not a requirement, and either approach produces an identical database on disk.
  - **If you do NOT have this source repo available** (only the installed app and this file): use the untyped `BsonDocument` approach throughout. If the database already has at least one question/category/tag, read an existing document to confirm the exact field names, types, and enum string values directly from the live data — that's the more reliable source of truth when it's available. If the database is brand new and empty (freshly installed, zero questions/categories/tags), there is nothing yet to read — LiteDB doesn't create a collection until something is first written to it — so fall back to this exact shape instead, since it's what the real app's `Interrogon.Core` model classes actually produce:
    - **`questions` collection**, one `BsonDocument` per question:
      - `_id`: a new `Guid`
      - `Title`: string
      - `Answer`: string (markdown; fenced code blocks use standard ` ```csharp ` style fences, since that's what the app's renderer parses)
      - `Type`: string — exactly one of `"TheoryQA"`, `"WorkExperience"`, `"CodeReview"`, `"AlgorithmProblem"`, `"SystemDesign"` (note: the Algorithms type's underlying enum name is `AlgorithmProblem`, not `"Algorithms"`)
      - `CategoryIds`: `BsonArray` of `Guid`s (a `BsonArray` of `BsonValue`-wrapped Guids), referencing `categories._id` — must contain at least one entry, per Step 2/3 category-required guidance elsewhere in this document
      - `TagIds`: `BsonArray` of `Guid`s, same shape, referencing `tags._id` — may be empty
      - `IsFavorite`: bool, always `false` for a newly imported question
      - `CreatedAt`: `DateTime` (UTC)
      - `LastViewedAt`: omit the field entirely (not `null` — LiteDB documents from the real app simply don't have this key until the app sets it)
      - `Notes`: omit the field entirely unless there's a genuine note to add (see Step 5/"What not to do" — don't invent notes)
      - `ImportKey`: string, in the `"{sourceFileName}#{slug-of-question}"` format used throughout this document, for future duplicate-detection re-runs
    - **`categories` collection**: `_id` (`Guid`) + `Name` (string). Nothing else.
    - **`tags` collection**: `_id` (`Guid`) + `Name` (string). Nothing else.
    - Create `categories`/`tags` documents first (checking by `Name` for an existing match even within the same import batch, since there's no persisted data yet to have deduplicated against), then reference their generated `_id`s in the `CategoryIds`/`TagIds` arrays when inserting questions.
- Read the existing database contents first: all current questions, categories, and tags. You need this to check for duplicates and to match categories/tags correctly. Do not process new content blind.
- Process everything currently sitting in `ImportData/` in one run, unless told otherwise.

### Step 1 — Extract individual questions

- Each source document may contain many questions. Split them into individual, self-contained units: one question + its answer (or code + explanation, for Code Review / Algorithms / System Design content).
- Keep the original wording of the question and answer intact at this stage — do not rephrase, summarize, or "clean up" language yet. This step is about splitting, not editing.
- If a document mixes question types (e.g. a chat transcript with both a theory discussion and a code-review exercise in the same conversation), split them into separate questions rather than forcing them into one.
- If something in the source isn't actually a question-and-answer pair (stray notes, meta-commentary, unrelated content), leave it out rather than forcing it into the model.
- **Personal experience narrative is not the same as "not a Q&A pair."** A source may contain autobiographical content tied to a specific employer/interview (e.g. "at Rabot I worked on integrations with...", "you actually did this at Dot") that wasn't phrased as a question in the original document. This is legitimate content for the **Work Experience Type** (see Step 2) — interviewers ask "tell me about a project where you did X" or "what was your experience with Y" constantly, and this content is the answer to that implicit question. Don't skip it as narrative/noise:
  - Reconstruct the general, reusable question it's actually answering (e.g. "Describe a project where you led integration work," not "tell me about Rabot").
  - Keep the answer as originally written, including the specific real details (company context, what was actually built) — the concrete specificity is the value of this Type, don't generalize or genericize it away.
  - Assign it Type `Work Experience`, plus whatever Category/Tags fit the technical content involved (e.g. Kafka category, Integrations tag) if relevant.
  - This only applies to content that's answering a real, askable interview question about your background/experience. Pure meta-commentary about the interview process itself, scheduling, or unrelated tangents still gets left out per the rule above.

### Step 2 — Assign a Type

Pick exactly one Type per question, based on the shape of its content, not where it came from:

| Type | What it looks like |
|---|---|
| Theory Q&A | General theoretical/conceptual knowledge, not tied to something you personally did. Prose answer, no code artifact under review. |
| Work Experience | A question answered by drawing on real experience — a project you worked on, a decision you made, a behavioral/culture-fit question about how you actually acted. Prose answer, but grounded in your own specifics rather than general theory. |
| Code Review | A code snippet that was reviewed for bugs/issues, plus the issue found and/or the fix. Includes short "predict the output / what happens when this runs" language-mechanics snippets, not just full-class debugging exercises — both shapes are "here's code, reason about its real behavior." |
| Algorithms | A problem statement plus a coded solution (LeetCode-style or similar). |
| System Design | An open-ended, trade-off-driven prompt (e.g. monolith vs. microservices, scaling a system) with structured notes/considerations rather than one definitive answer. Applies whether the trade-off is hypothetical or drawn from a real decision — the deciding factor is the open-ended-reasoning shape of the answer, not whether it's personal. |

**Theory Q&A vs. Work Experience** is the distinction to get right most often: ask whether the answer is general knowledge anyone could give, or whether it only makes sense because you personally did/decided/experienced something. "What is CQRS?" is Theory Q&A. "How did you use CQRS on the inventory reservation project?" is Work Experience, even though both are prose and both might mention CQRS.

If a question is genuinely ambiguous between two types otherwise, pick the one that matches how the *answer* is structured (code-first vs. prose-first vs. open-ended notes), not how the question is phrased.

### Step 3 — Assign Category and Tags

- A question can have multiple Categories and multiple Tags. Don't force a single pick if more than one genuinely applies.
- Before creating a new Category or Tag, check the existing list for a close-enough match (e.g. "EF Core" vs. "Entity Framework Core", "JWT" vs. "JSON Web Tokens", "Culture Interview" vs. "Culture Fit") and reuse it. Treat obvious synonyms, differing capitalization, and singular/plural variants as the same thing.
- Only create a new Category or Tag when nothing existing reasonably covers the concept.
- Category is for the broad topic/source area (C#/.NET, SQL, Security, Culture Interview, Kafka, etc.) — purely topical, not about whether the content is theoretical or experience-based (that's the Type, see Step 2). Tags are for finer-grained keywords that cut across categories (async, JWT, CORS, EF Core, etc.). Don't create a Category for something that's really tag-level granularity.
- Culture Interview questions will often end up as Type `Work Experience` (since answering them draws on real behavior), Category `Culture Interview` — the two dimensions combine independently, don't skip assigning one because the other feels like it "covers" it.
- If a Category/Tag match is ambiguous — could reasonably go either way, or you're inventing something close to an existing one but not identical — don't silently decide. Note it in the import report (Step 6) so a human can review it.

### Step 4 — Check for duplicates

- **This step applies only to Type `Theory Q&A`, `Work Experience`, and `System Design`.** For Type `Code Review` and `Algorithms`, skip duplicate checking entirely — always add the question, even if it looks similar to or overlaps with an existing one. Code Review and Algorithm questions naturally resemble each other in shape (a struct-mutation bug, an off-by-one, a two-sum variant) without actually being the same question, and forcing a duplicate judgment call on them does more harm than good. Determine the Type first (Step 2), then decide whether this step applies.
- For the Types it does apply to: before adding a question, compare it against what's already in the database: same or near-identical wording, or a different phrasing of the same underlying question.
- If a likely duplicate is found: do not add the new one. Leave the existing question untouched in the database.
- Record the skipped candidate in the import report along with the existing question it matched, so a human can review it and decide (keep existing, replace, merge, or add both anyway).
- When in doubt about whether something is a true duplicate or just a related-but-distinct question, err toward flagging it rather than silently adding or silently skipping it.

### Step 5 — Check content quality

- Import answers as originally written. Don't rewrite, "improve," or silently correct anything by default.
- If something looks like an obvious mistake — a typo, outdated or incorrect terminology, a garbled/incomplete code snippet, an answer that contradicts itself or contradicts well-established fact — flag it rather than fixing it silently:
  - If the issue is minor and the content is still usable as-is (e.g. a typo that doesn't change meaning), add the question normally but note the issue in the import report.
  - If the issue is severe enough that adding it as-is would be actively misleading (e.g. a factually wrong answer, a code snippet that doesn't compile/run and isn't clearly meant to illustrate a bug), hold it out of the database entirely and flag it in the report for manual review instead.
- Never guess at a "corrected" version and substitute it in without flagging. The report should always make clear when and why something was held back or added with a caveat.

### Step 6 — Write the import report

Produce one report per run at `ImportData/reports/import-<date>.md`, containing:

- **Summary** — count of questions added, broken down by Type.
- **New Categories/Tags created this run** — with a one-line reason if it wasn't an obvious gap (e.g. why an existing near-match wasn't reused).
- **Skipped likely-duplicates** — for each: the candidate question text, the existing question it matched, and why it was flagged as a duplicate.
- **Flagged content issues** — for each: the question, what looked off, whether it was still added (with the caveat) or held back entirely, and why.
- **Ambiguous Category/Tag decisions** — anything from Step 3 that could have gone another way.

Keep entries concise. This report exists to be skimmed and acted on quickly, not read as prose.

### Step 7 — Write to the database

- Only questions that passed Steps 4 and 5 (or were added with a flagged-but-acceptable caveat per Step 5) get written to LiteDB.
- Write Category and Tag records as needed (reusing existing ones per Step 3), then the Question record referencing them.
- Don't touch `LastViewedAt` or `Favorite` on newly imported questions — leave them unset/false. Those are set by user interaction in the app, not by import.
- Re-running this process against a document that was already imported should not create duplicate entries for content that made it in previously. Step 4's duplicate check against the existing database is what prevents this, so make sure you're actually checking against current DB state, not just against other items in the same batch.

### What not to do

- Don't silently skip a duplicate without recording it in the report. The person needs to know it happened.
- Don't silently rewrite an answer's content, even to fix an obvious error.
- Don't create a new Category/Tag without first checking for a reasonable existing match.
- Don't add anything that isn't a genuine question-and-answer (or question-and-code) pair just to avoid leaving source content unused.
- Don't guess at which LiteDB file to write to. Confirm the target path first.

---

## Roadmaps

*(This section is separate from question import above: give it to the agent only when building or changing roadmaps.)*

Roadmaps are ordered study paths through questions that are already in the database. They are built and maintained by an agent, never in the app UI. The app shows them under **Roadmaps** in the sidebar and records "studied" ticks per roadmap.

### Data shape

- **`roadmaps` collection** — one document per roadmap: `_id` (`Guid`), `Name` (string), `Description` (string), `Steps` (`BsonArray` of documents `{ QuestionId: Guid, Stage: string? }`).
- **`roadmap_progress` collection** — written by the app, never by the agent: `_id`, `RoadmapId`, `QuestionId`, `StudiedAt`. Do not touch it when editing roadmaps; ticks are matched by `QuestionId`, so they survive every edit below.
- Steps reference questions by `_id`, never by title. Resolve titles to ids when building a step; if a title matches zero or several questions, stop and report it rather than guessing. (Stored titles can contain line breaks, so compare with whitespace normalized.)

### The order is decided and kept

- A roadmap's order is a deliberate decision made with the user, not something derived from level, tag, or title. It is stored as the order of the `Steps` array; a step's position is its index + 1. The app displays it exactly as stored.
- `Stage` is optional. A divider is shown wherever it changes between consecutive steps, so each stage's steps must be contiguous.

### Creating a roadmap

Creating a brand-new roadmap is the only time the whole `Steps` list is written in one go. Agree the order and stages with the user first, resolve every title to exactly one question, then insert the document. Never create a second roadmap with a name that already exists.

### Changing an existing roadmap

Never replace the whole list of an existing roadmap. Use only these operations, **one at a time**, agreeing each position with the user first:

1. **Insert** a question at position N — everything from N onward shifts down by one (a plain array insert at index N-1). Give it a `Stage`: by default the stage of its neighbours, confirmed with the user.
2. **Remove** a question — the steps after it shift up by one. Leave `roadmap_progress` alone.
3. **Move** a question to position N — remove then insert, as one edit.

After each edit, show the user the steps around the change and the resulting positions before moving to the next one.

### Backup first

Before the first write of a roadmap session, copy the database file to a timestamped sibling (e.g. `interrogon.db.bak-yyyyMMdd-HHmmss`), exactly as for question imports, and confirm the app is closed. One backup per session is enough; do not make one per edit.

