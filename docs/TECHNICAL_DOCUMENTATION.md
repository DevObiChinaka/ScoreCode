# ScoreCode — Technical Documentation

> A reference document intended to support the writing of an academic paper / dissertation.
> It describes the system's motivation, architecture, data model, algorithms, security model,
> engineering methodology, evaluation approach, and limitations. Terminology is kept precise
> and academic; the document is written to be *cited from*, not marketed.

**System:** ScoreCode — a multi-tenant, secure programming-examination and code-evaluation platform
**Target platform:** .NET 10 · Windows desktop client + ASP.NET Core services + PostgreSQL
**Original deployment context:** Department of Computing Sciences, Admiralty University of Nigeria
**Document status:** reflects the implemented system, including honest limitations.

---

## 1. Abstract

ScoreCode is a platform for authoring, delivering, and grading **programming examinations** in
which candidates write and run source code under controlled conditions. It combines (a) a
**locked-down desktop examination client** that provides an offline-capable code editor and a
local, resource-limited code-execution sandbox; (b) a **central web API** that owns authentication,
authorization, tenant isolation, and the examination/grading domain logic; and (c) a **web
administration portal** for institutional and platform administration. The system was extended
from a single-institution tool into a **multi-tenant Software-as-a-Service (SaaS)** platform in
which independent institutions self-register, are vetted by a platform operator, and thereafter
administer their own lecturers and candidates in strict data isolation. Grading is performed
manually by lecturers and can be **assisted by a Large Language Model (LLM)** through a
provider-agnostic abstraction. This document specifies the system in sufficient detail to support
scholarly evaluation and reproduction of the design.

---

## 2. Problem statement and motivation

Traditional examination of programming ability suffers from two competing pressures:

1. **Authenticity of assessment.** Programming is a *doing* skill; assessment should let candidates
   write, compile, and run code, not merely answer questions about code on paper.
2. **Integrity of assessment.** Allowing candidates a full computing environment simultaneously
   grants them access to the internet, local files, messaging, and other applications — the very
   affordances that enable malpractice.

ScoreCode addresses this tension with a *restricted examination environment* on the candidate's
machine (application-level lockdown, disabled task-switching, local-only execution) coupled with a
*server-authoritative* record of sessions and submissions. A secondary motivation is
**scalability and reuse**: rather than a single-institution deployment, the platform supports many
institutions on shared infrastructure with cryptographically- and query-enforced tenant isolation,
lowering the marginal cost of adoption. A third motivation is **grading throughput**: programming
answers are laborious to mark, so the platform provides optional LLM-assisted grading suggestions
that a human lecturer reviews and approves.

---

## 3. Aim and objectives

**Aim.** To design and implement a secure, multi-tenant platform for the delivery and evaluation of
programming examinations.

**Objectives.**
1. Provide an examination client that supports code authoring, execution, and submission under a
   restricted, integrity-preserving environment.
2. Enforce a per-candidate examination window and a server-authoritative session/submission record.
3. Support many institutions on shared infrastructure with strong tenant data isolation.
4. Provide role-appropriate administration (platform operator, institution administrator, lecturer,
   candidate) with least-privilege access.
5. Reduce grading effort through optional, human-supervised, LLM-assisted grading.
6. Preserve candidate anonymity during grading to mitigate favouritism bias.

---

## 4. Background and related approaches

The design draws on three established categories of prior systems:

- **Automated programming judges / assessment systems** (e.g., online judges, autograders) — focus
  on executing submissions against test cases. ScoreCode differs by centring on *supervised
  examinations* and *human-in-the-loop grading* rather than fully automated verdicts.
- **Digital examination / lockdown-browser systems** — restrict the candidate's environment during
  assessment. ScoreCode implements application-level lockdown on the desktop rather than a browser
  extension, and pairs it with local code execution.
- **Multi-tenant SaaS architectures** — the platform uses the widely-documented *shared-database,
  shared-schema, discriminator-column* tenancy pattern with framework-level (ORM) query filtering.

A literature review for the paper should position ScoreCode against these categories and note that
its contribution is the *integration* of supervised examination, local sandboxed execution,
human-supervised LLM grading, and row-level multi-tenancy in a single coherent system.

---

## 5. System architecture

### 5.1 Solution structure (logical components)

The system is a .NET solution of five projects:

| Project | Responsibility |
|---|---|
| `ScoreCode.Api` | Central ASP.NET Core Web API: authentication, authorization, tenancy, exam/grading domain logic, AI grading, background scheduling. The single owner of database credentials and business rules. |
| `ScoreCode.Client` | WPF desktop examination client (candidate + lecturer). A **thin client** of the API; hosts only local concerns (editor, execution, autosave, lockdown). |
| `ScoreCode.Web` | Blazor Server administration portal (platform + institution administrators). A second thin client of the API. |
| `ScoreCode.Database` | Entity Framework Core `DbContext`, entity model, migrations, and the tenant-isolation query filters. |
| `ScoreCode.Shared` | Data-transfer objects (DTOs), enumerations, and constants shared by all layers. |

### 5.2 Deployment model

The platform follows a **central-server, thin-client** topology:

```
                     +---------------------------+
                     |   PostgreSQL (central)    |   only the server holds DB credentials
                     +-------------+-------------+
                                   |
                     +-------------+-------------+
                     |     ScoreCode.Api          |   hosted centrally
                     | (auth, tenancy, domain)    |
                     +----+------------------+----+
              HTTP + JWT  |                  |  HTTP + JWT
             +------------+-----+     +------+-------------+
             | Web portal        |     | Desktop client    |
             | (Blazor Server)   |     | (WPF, lockdown,   |
             | platform/inst.    |     | local execution)  |
             | administration    |     |                   |
             +-------------------+     +-------------------+
```

A key architectural decision (see §14.2) is that **clients never hold database credentials**. All
persistence flows through the API over authenticated HTTP; the desktop client does not embed the
database connection string, which would otherwise expose the shared multi-tenant database to every
candidate machine.

### 5.3 Technology stack

- **Runtime/language:** .NET 10, C#.
- **API:** ASP.NET Core (controllers), JWT bearer authentication, hosted background service.
- **Persistence:** Entity Framework Core (code-first, automatic migration on startup) over
  **PostgreSQL** (Npgsql provider).
- **Desktop client:** Windows Presentation Foundation (WPF); Microsoft WebView2 hosting the
  Monaco code editor; Windows Job Objects and low-level hooks via P/Invoke; SQLite (Microsoft.Data.Sqlite)
  for local autosave and a crash-safe submission queue; DPAPI for at-rest session-token protection.
- **Web portal:** Blazor Server (interactive server render mode).
- **Password hashing:** BCrypt.
- **LLM integration:** provider-agnostic HTTP clients (Google Gemini API and any OpenAI-compatible
  chat-completions endpoint, e.g. Groq, OpenRouter, or a local Ollama server).
- **Local dev orchestration:** Docker Compose (PostgreSQL) and PowerShell launch/stop scripts.

---

## 6. Multi-tenancy design

### 6.1 Tenancy model

ScoreCode uses the **shared-database, shared-schema** model with a **discriminator column**
(`InstitutionId`) on tenant-owned tables. This was chosen over schema-per-tenant or
database-per-tenant because it (a) fits a single centrally-hosted API and database, (b) minimises
operational overhead, and (c) allows tenant scoping to be enforced uniformly by the ORM.

An `Institution` is the tenant. `InstitutionId` (nullable) is present on the principal
tenant-owned aggregates — `User`, `Exam`, `Session`, `Submission` — and is denormalised onto
`Session`/`Submission` for direct filtering. Child records (`Question`, `ExamStudent`, `Grade`,
`ActivityLog`, `RefreshToken`) are scoped transitively through their owning parent.

### 6.2 Tenant resolution and isolation

Isolation is enforced at **two** levels ("defense in depth"):

1. **ORM global query filters.** The `DbContext` applies a global query filter to every
   tenant-owned entity of the form *"row belongs to the current tenant, or there is no current
   tenant."* The current tenant is supplied per request by an injected `ITenantProvider`.
2. **Explicit ownership checks** in service methods (e.g., an exam is loaded by
   `(examId, lecturerId)`), providing redundancy independent of the ORM.

The `ITenantProvider` derives the tenant from the authenticated JSON Web Token:

- **Platform administrator or unauthenticated request** (login, registration, background jobs):
  tenant = *null* → no scoping (the platform operator can see across tenants; anonymous endpoints
  such as login must search globally because identifiers are globally unique).
- **Tenant user** (institution admin, lecturer, candidate): tenant = the `institutionId` claim.
- **Tenant user with a missing claim** (defensive case): tenant = `Guid.Empty`, which matches no
  row — isolation therefore **fails safe (closed)** rather than open.

Because global filters can be bypassed by primary-key lookups (`Find`), administrative mutations use
filter-aware queries (`FirstOrDefault(u => u.Id == id)`) so that, e.g., an institution administrator
cannot deactivate a user in a different institution by guessing an identifier. Global-uniqueness
checks (matric/staff identifiers) explicitly *ignore* query filters to enforce platform-wide
uniqueness.

### 6.3 Institution lifecycle

```
[Apply] --public--> Pending --platform admin--> Approved --> (Suspended <-> Approved)
                        \--platform admin--> Rejected
```

- **Apply** (public, anonymous): an institution submits name, type, identifier prefix, and contact
  details; status becomes `Pending`.
- **Approve** (platform admin): status becomes `Approved`; the system **provisions the institution's
  first administrator account** with a prefix-derived, globally-unique identifier and a randomly
  generated temporary password (returned once to the operator). Credential delivery is behind an
  `IEmailService` seam (currently a logging stub; a real transport can be substituted).
- **Reject / Suspend / Reactivate**: administrative status transitions. Only `Approved` institutions
  appear in the candidate registration list; suspension removes an institution from registration
  without deleting its data.

---

## 7. Identity, authentication, and authorization

### 7.1 Roles and permissions

| Role | Scope | Capabilities |
|---|---|---|
| **Platform Administrator** | Global (no tenant) | Approve/reject/suspend institutions; cross-tenant monitoring. |
| **Institution Administrator** | One institution | Manage lecturers and candidates (create, activate/deactivate) within the institution. |
| **Lecturer** | One institution | Author exams, assign candidates, view/grade submissions, request AI grading suggestions, run candidate code. |
| **Candidate (Student)** | One institution | Take assigned exams, write/run/submit code, view released results. |

(A legacy "Admin" role is treated as equivalent to Platform Administrator for backward
compatibility with the seeded operator account.)

### 7.2 Authentication and session management

- **Password storage:** BCrypt with per-password salt.
- **Access tokens:** short-lived JSON Web Tokens (15-minute expiry) carrying subject (user id),
  role, identifier, and — for tenant users — an `institutionId` claim consumed by the tenant
  provider.
- **Refresh tokens:** long-lived (7 days), cryptographically random, persisted server-side, and
  **rotated on use** (the presented token is revoked and a new one issued), limiting replay.
- **Desktop session persistence:** tokens are stored on the client encrypted at rest with the
  Windows Data Protection API (DPAPI), scoped to the current OS user.
- **Startup seeding:** a platform administrator account is seeded on first run if none exists.

### 7.3 Candidate anonymity

By design, candidates are identified **only by matriculation number**; no personal name is
collected. Matric numbers are made globally unique by an institution-specific prefix, permitting a
single login identifier space while preserving anonymity. This is a deliberate control to reduce
**favouritism bias** during grading (a lecturer sees only the matric number).

---

## 8. Data model

### 8.1 Principal entities

- **Institution** — tenant. Attributes: name, type, prefix, contact, status, timestamps.
- **User** — platform/institution admin, lecturer, or candidate. Attributes: identifier
  (matric/staff), password hash, role, optional programme/level/department, `InstitutionId`
  (null only for the platform administrator), active flag.
- **Exam** — owned by a lecturer and an institution. Attributes: title, course code, scheduled time,
  duration, allowed languages (JSON), editor-assistance flag, grading mode, status, publication/close
  timestamps, results-released flag.
- **Question** — belongs to an exam. Attributes: order, text, marking guide, marks, language.
- **ExamStudent** — assignment join (exam ↔ candidate), optional extra time.
- **Session** — a candidate's attempt at an exam. Attributes: device fingerprint, per-session nonce,
  start/end times, status, heartbeat, `InstitutionId`.
- **Submission** — a candidate's finalised answer to a question within a session. Attributes: code,
  submission/finalisation timestamps, integrity hash, upload status, `InstitutionId`.
- **Grade** — one-to-one with a submission. Attributes: score, max score, feedback, grader,
  override metadata, timestamp.
- **RefreshToken** — persisted refresh token with revocation/expiry.
- **ActivityLog** — session-scoped integrity/audit events (see §9.4, §13).

### 8.2 Status lifecycles (state machines)

- **Exam:** `Draft → Active → Closed`. Publication (manual or scheduled) moves `Draft → Active`
  directly; there is intentionally no separate "Published" state. Results are released as a separate,
  guarded action.
- **Session:** `Active → Completed`.
- **Institution:** see §6.3.

Status values are stored as strings but centralised as named constants to prevent typographical
drift; enumerations exist for type-safety at the boundary.

---

## 9. Examination lifecycle and the secure examination environment

### 9.1 Authoring and publication

A lecturer authors an exam as a `Draft` (metadata, allowed languages, editor-assistance flag,
questions with marking guides and marks). Publication makes it `Active`. A **hosted background
service** additionally auto-publishes exams at their scheduled time, so an exam becomes available
without manual intervention.

### 9.2 Time-gating (start window)

A candidate may **begin** an exam only within a bounded window
`[ScheduledAt, ScheduledAt + graceMinutes]` (a fixed grace period). Before the scheduled time the
exam is "not open"; after the grace period a new attempt is refused, while an already-active session
may still be resumed. Once started, the candidate retains the full configured duration; the
server records `EndsAt = StartedAt + duration`. Time-gating is enforced **server-side** so a
tampered client clock cannot extend availability.

### 9.3 Restricted examination client (integrity controls)

During an active examination the desktop client applies application-level restrictions:

- **Restricted window:** a borderless, maximised, always-on-top window (no minimise/resize chrome).
- **Task-switch suppression:** a **low-level keyboard hook** (`WH_KEYBOARD_LL`) intercepts and
  swallows OS task-switching and window-management shortcuts (Alt+Tab, the Windows keys, Ctrl+Esc,
  Alt+Esc, Alt+F4) system-wide for the duration of the session.
- **Focus-loss detection:** the client observes window deactivation; if focus moves to another
  application (mouse to a second monitor, secure attention sequence, etc.), the client returns
  itself to the foreground, displays a visible warning, and **records the event** as an audit entry.
  Deactivations to the client's own dialogs are distinguished (by owning process) and ignored.
- **Editor restrictions:** copy/paste and the editor context menu are disabled inside the code
  editor.

These controls are released deliberately at submission so the candidate can, if necessary, interact
with the operating system to remediate a network fault before retrying the upload. (§14.3 discusses
the residual threat model honestly.)

### 9.4 Integrity/audit logging

Each restriction breach (focus loss, blocked shortcut) is recorded as an `ActivityLog` entry
associated with the candidate's session (event type, detail, timestamp). Blocked-shortcut telemetry
is rate-limited to avoid flooding; focus-loss events are always recorded. These logs feed the
administrative monitoring view and provide an evidentiary trail for post-hoc review.

### 9.5 Code editor

The editor is **Monaco** (the editor component underlying a well-known IDE), bundled **offline**
and hosted in an embedded WebView2 control served from a local virtual host (so fonts and assets
load without network access). It provides syntax highlighting, per-language completion providers
(function/snippet suggestions), and an **editor-assistance toggle** configured per exam by the
lecturer; when disabled, completion providers return nothing even under manual invocation, so the
setting is a genuine on/off control rather than a cosmetic one. Full editor state (code, caret,
scroll) is preserved across question tabs.

### 9.6 Autosave and crash-safe submission

- **Autosave:** the client persists per-question code to a local **SQLite** database at a fixed
  interval (dirty-flag driven), enabling recovery after a crash or restart mid-exam.
- **Crash-safe submission queue:** at submission time the client first writes the complete
  submission payload to a local SQLite queue, *then* attempts the API upload, retrying on failure.
  On the next launch the client offers to resubmit any pending payload. This "persist-then-send"
  ordering ensures a candidate's work is never lost to a transient network or process failure.

### 9.7 Submission integrity hash

For each answer the client computes `SHA-256(nonce ∥ studentId ∥ questionId ∥ finalizedAt ∥ code)`,
where `nonce` is a per-session server-issued value. The server recomputes and compares the hash on
receipt, rejecting mismatches. **Honest analysis:** because the client controls all hash inputs and
the nonce is transmitted to the client, this mechanism detects *accidental corruption in transit and
tampering by an unmodified client*, but it is **not** a defence against a modified client — it is an
integrity check, not an authenticity/anti-tamper guarantee. This should be stated as such in the
paper (see §14.3).

---

## 10. Local code execution and sandboxing

The client executes candidate code **locally** (no code leaves the machine), supporting Python,
Java, C++, and JavaScript; HTML and CSS are *previewed* live in a WebView2 surface rather than
executed. Execution properties:

- **Runtime resolution.** Interpreter/compiler paths are resolved either from a bundled runtime set
  (marker-file detected) or from a freshly-read machine/user `PATH` (from the registry, so runtimes
  installed after the client launched are still discovered).
- **Resource confinement.** Each execution runs under a **Windows Job Object** enforcing a CPU-time
  limit (5 s), a memory limit (256 MB), and a wall-clock timeout (10 s); output is captured and
  capped (10 KB). The process tree is terminated on timeout or limit breach, and the job's exit
  reason (timeout vs. memory) is surfaced to the user.
- **Isolation scope.** Each run uses a fresh temporary working directory that is deleted afterwards.

**Honest analysis:** the sandbox constrains *CPU, memory, wall-clock, and output*, but does **not**
impose network or filesystem isolation; a determined candidate could, in principle, execute code
with side effects. The paper should scope the sandbox claim to *resource confinement*, not full
containment (see §14.3).

---

## 11. Grading subsystem

### 11.1 Manual grading

Lecturers grade per submission with a score (validated against the question's maximum) and optional
feedback, via a dedicated grading interface that shows the question, marking guide, the candidate's
code (read-only in Monaco), and a control to *run* the code. Saved grades are recorded as
lecturer-authored.

### 11.2 AI-assisted grading (human-supervised)

Grading can be **assisted** by an LLM through a provider-agnostic seam (`IAiGradingService`):

- **Trigger:** on-demand. The lecturer requests a suggestion per submission; the system sends the
  question, marking guide, maximum marks, language, and candidate code to the configured model with
  a strict examiner prompt requesting a whole-number score and short justification as JSON.
- **Providers.** Two implementations exist, selected by configuration:
  1. **Google Gemini** (`generateContent`).
  2. **Any OpenAI-compatible chat-completions endpoint** — e.g. **Groq** (free tier), OpenRouter,
     OpenAI, or a **local Ollama/LM Studio** server (no API key for localhost).
  A shared prompt-and-parse helper, tolerant of extraneous text around the JSON, is used by both.
- **Human-in-the-loop.** The suggestion **populates** the score and feedback fields for the lecturer
  to review, edit, and save; the AI never writes an authoritative grade. Saved grades are therefore
  recorded as human-authored ("Manual"), reflecting genuine accountability.
- **Failure handling.** Quota (HTTP 429) and authentication (401/403) failures are surfaced as clear,
  actionable messages; a missing key disables AI assistance without affecting manual grading.

This design keeps the human as the accountable grader while reducing effort, and avoids lock-in to a
single LLM vendor — a point worth emphasising in the paper's contribution.

### 11.3 Results release and scoring semantics

- **Guarded release.** Releasing results to candidates is refused if any submission remains
  ungraded, preventing a candidate from seeing an ungraded answer as a genuine zero.
- **Skipped-question semantics.** A candidate's result is computed over **all** exam questions, not
  only submitted ones; an unanswered question contributes `0 / marks`, so the denominator reflects
  the true exam total and skipping is properly penalised.

---

## 12. Administration and monitoring

- **Web administration portal** (Blazor Server): platform administrators view an institutions
  overview (counts, pending queue) and manage the institution lifecycle (approve/reject/
  suspend/reactivate, with one-time credential display on approval). Institution administrators view
  overview statistics and manage their lecturers and candidates (create, activate/deactivate),
  entirely tenant-scoped.
- **Monitoring** (administrative): active sessions and recent integrity events are surfaced from the
  `ActivityLog` and `Session` records, giving operators a near-real-time view of ongoing examinations
  and any restriction breaches.

---

## 13. Security model and threat analysis

### 13.1 Assets and adversaries

- **Assets:** examination content and marking guides; candidate submissions and grades; per-tenant
  data confidentiality; the integrity of the examination process.
- **Adversaries:** (i) a **candidate** on their own machine seeking to cheat; (ii) a **cross-tenant**
  actor (a legitimate user of one institution) seeking another institution's data; (iii) a **network**
  adversary observing/altering traffic.

### 13.2 Controls (summary)

| Threat | Control |
|---|---|
| Cross-tenant data access | JWT-derived tenant claim + ORM global query filters (fail-closed) + explicit ownership checks. |
| Credential compromise | BCrypt hashing; short-lived access tokens; rotated refresh tokens; DPAPI-protected client storage. |
| Clock tampering to extend exams | Server-authoritative time-gating and per-session end time. |
| Casual task-switching / app-switching during exams | Low-level keyboard hook, focus-loss detection + audit, restricted window. |
| Accidental loss of candidate work | Persist-then-send submission queue; periodic autosave (both local SQLite). |
| Uncontrolled resource use by candidate code | Windows Job Object CPU/memory/wall-clock limits; output cap. |
| Ungraded/partial results shown to candidates | Guarded results release; all-questions scoring. |

### 13.3 Known limitations and threats to validity (state these in the paper)

A rigorous paper must acknowledge the residual risks:

1. **Client-side lockdown is not tamper-proof.** The candidate owns the machine. Low-level hooks and
   an always-on-top window deter casual switching but cannot stop a determined adversary who runs a
   second device, uses a virtual machine, or modifies the client. The secure attention sequence
   (Ctrl+Alt+Del) cannot be intercepted by any user-mode hook. Robust deployments therefore
   recommend OS-level single-application restriction on dedicated exam machines as a *deployment*
   control, not an application feature.
2. **Submission hashing is an integrity check, not authenticity.** See §9.7 — a modified client can
   forge valid hashes.
3. **Execution sandbox confines resources, not I/O.** See §10 — no network/filesystem isolation.
4. **AI grading is advisory and provider-dependent.** Suggestions may be wrong; the human remains
   accountable. Availability depends on an external service and its quota/policy.
5. **Verification is lightweight.** The system is verified by compilation and manual/scripted API
   smoke tests, not by an automated test suite (see §15) — a threat to reproducibility that the
   paper should note as future work.

---

## 14. Notable design decisions (with rationale)

### 14.1 Row-level multi-tenancy over database-per-tenant
Chosen for operational simplicity and uniform ORM-level enforcement on a single central database;
appropriate for the expected scale and the self-hosted-API model.

### 14.2 Thin client over self-hosting the API on candidate machines
An earlier design had the desktop client spawn its own API against a local database. When the data
store became **central and shared**, continuing to embed database credentials in the client would
have exposed the entire multi-tenant database to every candidate machine — an unacceptable exposure
for an integrity-critical, adversarial-candidate system. The client was therefore refactored into a
**thin HTTP client**; only the central server holds credentials and enforces isolation. (This also
*reduced* client code.)

### 14.3 Human-supervised, provider-agnostic AI grading
Keeps accountability with the lecturer, avoids vendor lock-in, and — by supporting OpenAI-compatible
and local endpoints — permits **zero-cost** operation (free-tier or local models), which matters for
resource-constrained institutional deployment.

### 14.4 Candidate anonymity by construction
Collecting no candidate name (only a prefixed matric number) removes an entire class of grading bias
at the data-model level rather than by policy.

---

## 15. Software-engineering methodology

- **Incremental, phased delivery.** The multi-tenant conversion was executed in verifiable phases —
  (0) version-control baseline and secret hygiene, (1) additive tenant data model with backfill,
  (2) tenant isolation and institution lifecycle, (3) thin-client cutover, (4) web portal — each
  ending in a compiling, committed checkpoint, so the system remained runnable throughout.
- **Configuration and secret management.** Runtime secrets (signing key, database password, LLM API
  key) are kept out of version control in an environment-specific, git-ignored configuration file,
  with a committed template; the API fails fast with a clear message if the signing key is absent.
- **Database evolution.** EF Core code-first migrations; a startup routine applies pending migrations
  and performs idempotent data backfill (e.g., assigning pre-tenancy rows to a default institution).
- **Build/run orchestration.** A single script starts the database (Docker), the API, the web portal,
  and the desktop client in the correct order (with readiness waits), and a companion script cleanly
  terminates stray processes — reducing environment friction and the port/lock issues typical of
  multi-process desktop+server systems.
- **Verification approach.** Correctness was validated by (a) full-solution compilation and (b)
  scripted end-to-end API smoke tests against a live database (e.g., institution apply → approve →
  provisioned-admin login; cross-tenant isolation checks confirming an institution administrator sees
  only its own users while the platform operator sees all). This is pragmatic but not a substitute
  for an automated regression suite.

---

## 16. Suggested evaluation methodology (for the paper)

To evaluate ScoreCode empirically, the paper could combine:

1. **Security/isolation evaluation.** Enumerate tenant-boundary test cases and demonstrate
   fail-closed behaviour (a tenant user cannot read/mutate another tenant's records via id-guessing;
   anonymous endpoints behave correctly). Report as a coverage matrix.
2. **Integrity-control evaluation.** Empirically test which task-switching vectors are suppressed and
   which are not (documenting the honest residual set), and verify audit-log fidelity under induced
   focus-loss.
3. **Execution-sandbox evaluation.** Verify enforcement of CPU/memory/wall-clock/output limits with
   adversarial programs (infinite loops, allocation bombs, verbose output).
4. **AI-grading evaluation.** On a labelled corpus of graded submissions, measure agreement between
   AI suggestions and lecturer grades (e.g., mean absolute error, Cohen's/weighted kappa, correlation),
   and report human-review time saved. Compare providers/models. Emphasise the human-in-the-loop
   framing.
5. **Usability evaluation.** Lightweight task-based study or questionnaire (e.g., SUS) with lecturers
   and candidates.
6. **Performance/scalability (optional).** API throughput/latency under concurrent sessions;
   query-filter overhead.

---

## 17. Limitations

- Windows-only examination client (WPF, Job Objects, DPAPI, low-level hooks are Windows-specific).
- Client-side integrity controls are deterrents, not guarantees (§13.3).
- No automated test suite; verification is manual/scripted.
- Email delivery of provisioned credentials is stubbed (seam present).
- Real-time notifications (e.g., live exam events) are scaffolded but not the focus of the current
  system.

---

## 18. Future work

- OS-level lockdown integration and/or a hardened deployment guide for dedicated exam machines.
- Cryptographically stronger submission provenance (e.g., server-issued signed tokens, attestation).
- Network/filesystem isolation for code execution (containers/sandboxing runtimes).
- Automated grading against test cases as an additional grading mode, complementing human + AI.
- Automated test suite (unit + integration) and CI.
- Cross-platform examination client.
- Empirical evaluation of AI-grading agreement across models and disciplines.

---

## 19. Claimed contributions (framing for the paper)

1. An **integrated architecture** combining supervised programming examinations, offline-capable
   local code editing and resource-confined execution, human-supervised LLM grading, and row-level
   multi-tenancy in one coherent, deployable system.
2. A **fail-closed, ORM-enforced tenant-isolation** design with a JWT-derived tenant context and
   redundant ownership checks, presented with an explicit threat model.
3. A **provider-agnostic, human-in-the-loop AI grading** design that avoids vendor lock-in and admits
   zero-cost (free-tier/local) operation while preserving lecturer accountability.
4. A **candidate-anonymity-by-construction** data model that mitigates grading bias structurally.
5. An **honest security analysis** distinguishing deterrent controls from guarantees — itself a
   contribution to the responsible reporting of examination-integrity systems.

---

## Appendix A — API endpoint catalogue (representative)

| Area | Method & path | Auth | Purpose |
|---|---|---|---|
| Auth | `POST /api/v1/auth/register` | anon | Candidate self-registration (chosen institution). |
| Auth | `POST /api/v1/auth/login` | anon | Issue access + refresh tokens. |
| Auth | `POST /api/v1/auth/refresh` | anon | Rotate refresh token. |
| Auth | `GET  /api/v1/auth/me` | any | Own profile (role-specific). |
| Auth | `POST /api/v1/auth/change-password` | any | Change password. |
| Institutions | `POST /api/v1/institutions/apply` | anon | Submit institution application. |
| Institutions | `GET  /api/v1/institutions/options` | anon | Approved institutions (registration list). |
| Institutions | `GET  /api/v1/institutions` | platform | List by status. |
| Institutions | `POST /api/v1/institutions/{id}/approve` | platform | Approve + provision admin. |
| Institutions | `POST /api/v1/institutions/{id}/reject` | platform | Reject. |
| Institutions | `POST /api/v1/institutions/{id}/suspend|reactivate` | platform | Suspend / reactivate. |
| Users | `POST /api/v1/users` | inst-admin | Create lecturer (tenant-stamped). |
| Users | `GET  /api/v1/users?role=` | admin | List users (tenant-scoped). |
| Users | `PATCH /api/v1/users/{id}/activate|deactivate` | admin | Toggle active. |
| Users | `PATCH /api/v1/users/{id}/profile` | admin | Edit candidate immutable fields. |
| Exams (lecturer) | `POST/GET/PATCH/DELETE /api/v1/lecturer/exams...` | lecturer | Author/manage exams, questions, assignments, submissions. |
| Exams (lecturer) | `POST /api/v1/lecturer/submissions/{id}/grade` | lecturer | Save a grade. |
| Exams (lecturer) | `POST /api/v1/lecturer/submissions/{id}/ai-suggest` | lecturer | Request an AI grading suggestion. |
| Exams (lecturer) | `POST /api/v1/lecturer/exams/{id}/release-results` | lecturer | Guarded results release. |
| Exams (candidate) | `GET  /api/v1/exams/student` | candidate | Assigned exams. |
| Exams (candidate) | `POST /api/v1/exams/{id}/start` | candidate | Start (time-gated) → session + nonce + questions. |
| Exams (candidate) | `POST /api/v1/exams/{id}/submit` | candidate | Submit answers (hash-checked). |
| Exams (candidate) | `POST /api/v1/exams/{id}/violation` | candidate | Record an integrity event. |
| Exams (candidate) | `GET  /api/v1/exams/results` | candidate | Released results. |
| Monitoring | `GET  /api/v1/monitoring/overview` | platform | Active sessions + recent integrity events. |

## Appendix B — Key parameters (defaults)

| Parameter | Default |
|---|---|
| Access-token lifetime | 15 minutes |
| Refresh-token lifetime | 7 days (rotated on use) |
| Exam start grace period | fixed window after scheduled time |
| Execution CPU limit | 5 seconds |
| Execution memory limit | 256 MB |
| Execution wall-clock timeout | 10 seconds |
| Execution output cap | 10 KB |
| Autosave interval | 5 seconds |
| Supported execution languages | Python, Java, C++, JavaScript (HTML/CSS previewed) |
| Default AI provider | OpenAI-compatible (free-tier / local capable); Gemini also supported |

## Appendix C — Glossary

- **Tenant / Institution** — an independently-administered organisation on the shared platform.
- **Discriminator column** — the `InstitutionId` used to scope shared-schema rows to a tenant.
- **Global query filter** — an ORM predicate applied automatically to all queries of an entity type.
- **Fail-closed** — on ambiguity, deny access (here: match no rows) rather than allow.
- **Restricted examination environment** — the application-level lockdown applied to the candidate
  client during an active session.
- **Human-in-the-loop grading** — automated suggestions that a human reviews and approves.
- **Nonce** — a per-session server value bound into the submission integrity hash.

---

*This document reflects the system as implemented, including its limitations. For any quantitative
claim in the paper, prefer measured results over the qualitative descriptions here.*
