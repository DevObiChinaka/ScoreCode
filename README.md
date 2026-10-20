# ScoreCode

**A programming examination and code-evaluation platform built as an undergraduate software engineering project.**

ScoreCode explores how institutions could create and manage programming exams, let students write and submit code through a desktop application, and review results through a web-based administration portal.

The project brings together a Windows desktop client, a central API, a PostgreSQL database, and a Blazor administration portal. It was developed as an undergraduate project, so it represents a practical implementation of the ideas and trade-offs described below, not a claim of production-grade examination security.

## What it does

- **Exam management:** create and manage exams, questions, schedules, supported programming languages, and publication status.
- **Desktop exam experience:** students can work on programming questions in a WPF application with an embedded code editor.
- **Local code execution:** the client can run supported languages using installed runtimes, with process resource limits applied where supported.
- **Submission handling:** answers are associated with an exam session and submitted to the API.
- **Accounts and roles:** authentication and role-based access separate student, lecturer, and administrative workflows.
- **Institution-aware data:** the backend models institutions and applies tenant-aware data filtering.
- **Monitoring and results:** exam activity and submissions can be reviewed through the platform's administrative workflows.
- **Optional AI-assisted grading:** grading can be configured with Gemini or an OpenAI-compatible provider. AI output should be treated as assistance, not infallible judgement.

> **Security note:** local execution and desktop monitoring have practical limits. Resource limits are not equivalent to a hardened sandbox, and a client application cannot be treated as a fully trusted environment. ScoreCode should be evaluated as an academic project, not deployed for high-stakes exams without further security work and testing.

## Architecture

```text
┌────────────────────────┐        ┌────────────────────────┐
│ ScoreCode Desktop      │        │ ScoreCode Web Portal   │
│ WPF / C#               │        │ Blazor Server          │
│ Student exam workflow  │        │ Administration         │
└────────────┬───────────┘        └────────────┬───────────┘
             │                                 │
             └──────────────┬──────────────────┘
                            │ HTTP API
                  ┌─────────▼─────────┐
                  │ ScoreCode API     │
                  │ ASP.NET Core      │
                  │ Auth, exams,      │
                  │ submissions,      │
                  │ results, grading  │
                  └─────────┬─────────┘
                            │ EF Core
                  ┌─────────▼─────────┐
                  │ PostgreSQL        │
                  └───────────────────┘
```

The desktop client is responsible for the candidate-facing coding experience and local execution. The API is the central service for accounts, exam records, sessions, and submissions. The web portal communicates with the API for administrative tasks.

## Technology

| Area | Technologies |
|---|---|
| Desktop client | C#, WPF, .NET 10 |
| Code editor | Monaco Editor |
| Backend | ASP.NET Core Web API |
| Web administration | Blazor Server |
| Data access | Entity Framework Core |
| Database | PostgreSQL |
| Authentication | JWT access tokens, refresh-token workflow, BCrypt password hashing |
| Live communication | SignalR |
| AI grading integrations | Gemini and OpenAI-compatible APIs |
| Version control | Git and GitHub |

## Repository layout

The solution is divided into projects so that the desktop experience, API, shared contracts, data layer, and web portal can be developed separately.

- `ScoreCode.Client/` — Windows desktop application.
- `ScoreCode.Api/` — ASP.NET Core API and application services.
- `ScoreCode.Web/` — Blazor Server administration portal.
- `ScoreCode.Database/` — EF Core database context, entities, and persistence configuration.
- `ScoreCode.Shared/` — shared DTOs and contracts.
- `docs/` — project and technical documentation.
- `SPEC.md` — original project specification.

## Running the project

ScoreCode is a multi-project .NET solution and requires a compatible Windows development environment for the WPF client.

### Prerequisites

- .NET 10 SDK
- PostgreSQL
- A Windows environment for the WPF desktop client
- The language runtimes or compilers required for the programming languages you want to execute
- API configuration for the database connection and JWT signing key
- Optional provider credentials for AI grading or SMTP email

### Configuration

1. Clone the repository and open the solution in a compatible .NET IDE or use the .NET CLI.
2. Create local development configuration for the API based on `ScoreCode.Api/appsettings.Development.example.json`.
3. Configure a PostgreSQL connection string and a strong, private JWT signing key. Do not commit real credentials or production secrets.
4. Configure the initial administrator using explicit local configuration before starting the API.
5. Start the API, then launch the web portal and desktop client as needed.
6. Install and configure the runtimes required for local code execution.

The API applies pending EF Core migrations at startup. Review the development configuration and database setup before running it against any database containing important data.

> Exact launch profiles and local setup can vary by development environment. This README describes the main setup requirements; it does not claim that the repository has been verified on a clean machine.

## Project focus

The project gave me a way to explore several software engineering concerns in one system:

- separating client applications from a central API;
- modelling exam sessions and submission workflows;
- designing role-aware, institution-aware application behaviour;
- integrating a relational database through EF Core;
- working with external AI services behind a provider abstraction; and
- considering the trade-offs between usability, integrity, and security in a desktop examination environment.

## Known limitations

This is an undergraduate project and still has areas that would need more work before real institutional deployment.

- **Local code execution:** process resource limits do not provide complete operating-system isolation. Stronger sandboxing and security testing would be needed before running untrusted code in high-stakes settings.
- **Client trust:** client-side controls and integrity hashes can support auditing, but they do not prove that a candidate's machine or application is uncompromised.
- **AI grading:** AI-generated assessments may be inconsistent or incorrect and need human review, especially when grades affect students.
- **Operational hardening:** production use would require a deliberate review of secrets, deployment configuration, abuse controls, monitoring, backup and recovery, and authorization boundaries.
- **Verification:** build and test status should be established in the target environment rather than inferred from the presence of source code.

These limitations are part of the engineering context of the project, not claims that the system solves every problem in secure computer-based examinations.

## Current status

ScoreCode is a work-in-progress academic software project. The repository contains the implementation and supporting documentation; features and deployment behaviour should be verified against the current source and environment.

## Feedback

Suggestions and constructive feedback are welcome, particularly on the architecture, examination workflow, usability, and areas that would benefit from further testing.
