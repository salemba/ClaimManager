---
stepsCompleted:
  - 1
  - 2
  - 3
  - 4
  - 5
  - 6
  - 7
  - 8
inputDocuments:
  - d:/ws/bmad/_bmad-output/planning-artifacts/prd.md
  - d:/ws/bmad/_bmad-output/planning-artifacts/ux-design-specification.md
  - d:/ws/bmad/_bmad-output/planning-artifacts/product-brief-ClaimManager.md
  - d:/ws/bmad/_bmad-output/brainstorming/brainstorming-session-2026-05-10-17-01-58.md
workflowType: 'architecture'
lastStep: 8
status: 'complete'
completedAt: '2026-05-10'
project_name: 'ClaimManager'
user_name: 'Slouma'
date: '2026-05-10'
---

# Architecture Decision Document

_This document builds collaboratively through step-by-step discovery. Sections are appended as we work through each architectural decision together._

## Project Context Analysis

### Requirements Overview

**Functional Requirements:**
ClaimManager has 52 functional requirements spanning eight capability areas: claim intake and file management, workflow guidance and decision progression, supervisor control and operational oversight, claimant-safe transparency and communication, governance and permissions, enterprise access and integrations, AI-assisted decision support, and search/navigation.

Architecturally, this implies a platform with a strong workflow core rather than a simple CRUD application. The product must support consistent claim state, blocker taxonomy, ownership tracking, next-step guidance, and approval progression while also exposing those same facts safely to supervisors, governance users, and claimant-facing communication flows. The breadth of requirements also indicates several bounded domains that will need clean interfaces even if implemented in one deployable system early on.

**Non-Functional Requirements:**
The architecture is strongly shaped by demanding non-functional requirements: page loads within 3 seconds, key actions and search within 2 seconds, dashboard and workflow freshness within 1 minute, 99.9% uptime, strong auditability, role-based access control, enterprise SSO, TLS 1.3, support for post-quantum requirements where applicable, accessibility across primary workflows, and reliable integration with reconciliation after failures.

These NFRs mean the architecture must prioritize operational trust, diagnosability, and controlled complexity from the beginning. Audit, permissions, data freshness, and integration recovery are core architectural drivers, not implementation details to add later.

**Scale & Complexity:**
ClaimManager is a high-complexity enterprise web platform in the insuretech domain. It combines workflow management, approval handling, dashboards, communication, governance, and enterprise integration in a trust-sensitive environment.

- Primary domain: enterprise full-stack web application
- Complexity level: high to enterprise
- Estimated architectural components: 10 to 12 logical subsystems

### Technical Constraints & Dependencies

The strongest constraints are:
- desktop-first, high-density operational UX with tablet support and limited mobile support for monitoring and lightweight actions
- standalone product positioning for carriers, despite earlier brief language suggesting a sidecar model
- MPA-oriented web experience with near-real-time freshness rather than full real-time collaboration
- dependence on external policy systems, payment systems, document repositories, identity providers/SSO, and messaging systems
- requirement to preserve strict separation between internal workflow truth and claimant-safe external communication
- need for a themeable design system plus custom workflow-specific UI components

### Cross-Cutting Concerns Identified

The main cross-cutting concerns are identity and access control, auditability, workflow-state integrity, claimant-safe communication governance, integration reliability, observability, accessibility, and search/findability.

Several of these concerns intersect directly. For example, claimant-safe explanation depends on workflow-state integrity, permissions, audit logging, and communication governance. Supervisor dashboards depend on workflow freshness, search/filter infrastructure, and observability around stale or compromised data. These concerns will need shared architectural treatment rather than isolated feature-by-feature solutions.

## Starter Template Evaluation

### Primary Technology Domain

Enterprise full-stack web application based on the project requirements and technical preferences.

The selected implementation direction is:
- Backend: ASP.NET Core
- Frontend: React
- Database: PostgreSQL
- Local orchestration: Docker-friendly
- Deployment posture: cloud-agnostic

### Starter Options Considered

**Option 1: Legacy ASP.NET Core React SPA template**
This was rejected. Microsoft's current .NET template documentation marks the built-in `react` template as discontinued since .NET 8, so it is not an acceptable foundation for a current architecture.

**Option 2: Hand-composed ASP.NET Core Web API + Vite React + PostgreSQL**
This remains technically valid and gives maximum control, but it pushes orchestration, service discovery, and multi-service local development wiring onto the team immediately. For this project, that creates unnecessary setup work at the exact point where clarity and consistency matter most.

**Option 3: Official Aspire React starter**
This is the best current fit. Aspire's current template catalog includes `aspire-ts-cs-starter`, described as a Starter App for ASP.NET Core and React using a C# AppHost. It aligns directly with the chosen stack and gives a maintained foundation for a distributed, Docker-friendly, cloud-agnostic application model.

### Selected Starter: aspire-ts-cs-starter

**Rationale for Selection:**
This starter matches the chosen stack without relying on deprecated templates. It gives ClaimManager a modern React frontend, an ASP.NET Core backend, and a first-class orchestration layer through Aspire. That is a strong fit for a product that will need backend APIs, frontend work surfaces, PostgreSQL, observability, local multi-service startup, and eventual integration-heavy deployment patterns.

It also fits the project's architectural shape better than a plain single-project starter because ClaimManager is not a simple web CRUD app. It has workflow logic, dashboards, integrations, claimant-safe communication boundaries, and cross-cutting concerns that benefit from an explicit application topology early.

### Initialization Command

```bash
aspire new aspire-ts-cs-starter --version 13.3.0 --name ClaimManager --output ./ClaimManager --use-redis-cache false
```

If the CLI prompts for URL style, prefer standard `localhost` URLs unless there is a specific reason to use `*.dev.localhost`.

### Architectural Decisions Provided by Starter

**Language & Runtime:**
- ASP.NET Core backend in C#
- React frontend
- C# Aspire AppHost for orchestration
- Current official React baseline is React 19.x
- Current Aspire CLI stable documentation references 13.3.0

**Styling Solution:**
- Plain CSS baseline, not Tailwind
- Suitable as a neutral starting point, but insufficient as the final design-system strategy for ClaimManager
- A themeable enterprise component system should be added intentionally after scaffold creation

**Build Tooling:**
- Vite-based frontend build and dev server
- ASP.NET Core backend build pipeline
- Aspire AppHost for local orchestration, dependency wiring, dashboarding, and service startup coordination

**Testing Framework:**
- The starter establishes backend and frontend structure, but test strategy should be strengthened deliberately
- Additional choices should be made for backend integration testing, frontend component testing, and end-to-end workflow validation

**Code Organization:**
- Separate backend service and frontend app
- AppHost defines topology and service relationships explicitly
- Frontend-to-backend communication is wired through service references and Vite proxy behavior
- This is a better fit than a monolithic SPA-hosted-inside-backend structure for ClaimManager's complexity

**Development Experience:**
- Single orchestration model for multi-service development
- Central dashboard for local logs and resource visibility
- Good fit for Docker-friendly PostgreSQL and future infrastructure additions
- Clean upgrade path to adding PostgreSQL, Redis, observability, and more services without redesigning the repo structure

**PostgreSQL Follow-On:**
PostgreSQL is not the default database scaffolded into this React starter, but Aspire has first-class PostgreSQL support and Docker-based local orchestration. PostgreSQL should be added immediately after starter creation as part of the first implementation story.

**Note:** Project initialization using this starter should be the first implementation story, followed immediately by PostgreSQL integration, authentication baseline, and design-system setup.

## Core Architectural Decisions

### Decision Priority Analysis

**Critical Decisions (Block Implementation):**
- Backend stack: ASP.NET Core with C# on the Aspire-based starter foundation
- Frontend stack: React with Vite
- Primary database: PostgreSQL
- Primary data access approach: EF Core 9
- Authentication baseline: ASP.NET Core Identity on PostgreSQL
- API style: REST + OpenAPI
- Frontend routing: React Router
- Frontend state model: TanStack Query v5 for server state plus Zustand for lightweight client state
- Migration strategy: deployment-time migrations
- Validation strategy: FluentValidation for request validation plus domain invariants in application/domain layers
- Deployment shape: containerized services on a simpler app platform first
- Redis scope: excluded from initial MVP baseline

**Important Decisions (Shape Architecture):**
- Authentication is intentionally transitional: custom auth now, enterprise SSO later
- Authorization will use policy-based authorization and application roles aligned to ClaimManager business roles
- No GraphQL in the MVP architecture; external and internal consumers use REST endpoints
- No runtime schema mutation on application startup
- No Redis dependency unless performance or coordination needs prove it necessary

**Deferred Decisions (Post-MVP):**
- Enterprise OIDC/OAuth2 federation and carrier IdP integration
- Redis-backed caching or coordination
- More advanced read-model optimization if dashboard/query loads justify hybrid EF + Dapper later
- Multi-service decomposition beyond the initial service boundaries established by the starter

### Data Architecture

- Primary relational store: PostgreSQL 18
- ORM and relational access layer: EF Core 9
- Data modeling approach: relational domain model centered on claims, workflow state, approvals, blockers, audit trails, permissions, and communication history
- Query strategy: EF Core first across reads and writes; optimize later only where measured pressure exists
- Migration strategy: deployment-time migrations only
- Caching strategy: no Redis in MVP; use database-backed correctness first, then add targeted caching only after profiling

**Rationale:**
This keeps the initial architecture simpler and more trustworthy for a workflow-heavy enterprise product. It aligns with the auditability and consistency requirements in the PRD while avoiding premature complexity in caching and split data-access patterns.

### Authentication & Security

- Authentication baseline: ASP.NET Core Identity backed by PostgreSQL
- Authentication posture: custom auth for initial product phases, explicitly designed for later enterprise federation
- Authorization model: policy-based authorization with claims/roles mapped to product roles such as adjuster, supervisor, product owner/governance, and IT/security admin
- Password and credential handling: framework-managed Identity flows, not hand-rolled custom credential storage
- API security strategy: authenticated APIs by default, role/policy checks at application boundaries, audit coverage for security-sensitive actions
- Encryption and transport: TLS 1.3 aligned with platform and hosting support
- Internal/external data boundary: claimant-safe explanation output remains segregated from internal workflow detail through explicit authorization and transformation boundaries

**Rationale:**
Keeping custom auth is acceptable only because the implementation uses a mature framework baseline rather than bespoke auth logic. This preserves a migration path to enterprise SSO later while reducing immediate security risk relative to a scratch-built auth system.

### API & Communication Patterns

- Primary API style: REST
- API contract documentation: OpenAPI
- Backend shape: ASP.NET Core service endpoints organized by business capability
- Error handling standard: consistent structured error responses with centralized exception handling and validation error mapping
- Rate limiting strategy: defer strict rate limiting design until external integration and claimant-notification usage patterns are clearer, but keep the architecture ready for middleware-based enforcement
- Service communication pattern: synchronous HTTP within the initial application topology; avoid introducing messaging infrastructure before concrete need
- Explanation and outbound communication pattern: internal workflow state is translated through a dedicated claimant-safe transformation layer before outbound communication is allowed

**Rationale:**
REST + OpenAPI is the most legible enterprise choice for carrier integrations, internal development speed, and later documentation/testing. It also matches the starter baseline and avoids unnecessary API-surface complexity.

### Frontend Architecture

- Routing: React Router
- Server state: TanStack Query v5
- Client state: Zustand for lightweight app/session/UI state only
- Component approach: design-system-led component structure with domain-specific workflow components layered on top
- Interaction model: dashboard-to-claim drill-down with persistent workflow context
- Performance approach: optimize around query behavior, view composition, and progressive disclosure before introducing broader caching complexity
- Styling baseline: starter CSS is temporary only; final architecture expects a themeable enterprise component system

**Rationale:**
TanStack Query handles the product's server-state-heavy UX well, while Zustand keeps local workflow UI state lightweight. This is a better fit than a large global-store architecture for a system where most complexity comes from server truth and workflow transitions.

### Infrastructure & Deployment

- Local orchestration: Aspire AppHost
- Runtime packaging: containerized services
- Initial hosting target: simpler container platform rather than Kubernetes-first
- Database environment: PostgreSQL containerized in development, managed or hosted relational deployment later
- Observability posture: use Aspire orchestration and service defaults as the baseline, then extend with production-grade logging, metrics, and tracing during implementation
- Environment strategy: environment-specific configuration with strict separation of secrets and operational settings
- CI/CD posture: build, test, package containers, run deployment-time migrations, then deploy application services

**Rationale:**
This keeps the architecture Docker-friendly and cloud-agnostic without prematurely forcing a heavier platform model. It is operationally credible while remaining proportionate to MVP scope.

### Decision Impact Analysis

**Implementation Sequence:**
1. Scaffold the solution from the Aspire React/.NET starter
2. Add PostgreSQL integration and EF Core 9 data layer
3. Establish ASP.NET Core Identity with PostgreSQL-backed auth tables
4. Add deployment-time migration workflow
5. Implement REST endpoint structure and OpenAPI exposure
6. Add FluentValidation and domain invariant enforcement
7. Establish React Router, TanStack Query, and Zustand frontend foundations
8. Introduce design system and workflow-specific UI primitives
9. Add observability, audit, and integration boundaries
10. Defer Redis and enterprise federation unless implementation evidence justifies them

**Cross-Component Dependencies:**
- Identity and authorization affect API boundaries, audit design, and claimant-safe communication controls
- EF Core and migration strategy affect deployment pipeline and operational safety
- TanStack Query and REST endpoint design affect dashboard performance and frontend state flow
- Design-system adoption affects component boundaries and frontend composition strategy
- Deferred SSO affects how authentication abstractions should be designed from the beginning so that carrier federation can replace local auth without rewriting authorization logic

## Implementation Patterns & Consistency Rules

### Pattern Categories Defined

**Critical Conflict Points Identified:**
12 areas where AI agents could make incompatible implementation choices if patterns are left unspecified.

### Naming Patterns

**Database Naming Conventions:**
- Tables use `snake_case` and plural nouns.
- Primary keys use `id`.
- Foreign keys use `<referenced_entity>_id`.
- Join tables use combined pluralized entity names where needed.
- Indexes use `idx_<table>_<column_list>`.
- Unique indexes use `ux_<table>_<column_list>`.
- Constraints use explicit names where practical.

**Examples:**
- `claims`
- `claim_blockers`
- `claimant_messages`
- `claim_id`
- `owner_user_id`
- `idx_claims_status_created_at`

**API Naming Conventions:**
- Route paths use plural resource nouns.
- Nested resources are used only where the relationship is truly scoped.
- Route segments use kebab-case only when multiple words are needed.
- Query parameters use `camelCase`.
- Route parameters use `{id}` style in specifications and `id` naming in handlers.

**Examples:**
- `/api/claims`
- `/api/claims/{id}`
- `/api/claims/{id}/timeline`
- `/api/supervisor-dashboard`
- `?ownerUserId=`
- `?approvalStatus=`

**Code Naming Conventions:**
- C# types use `PascalCase`.
- C# methods, local variables, and parameters use `camelCase`.
- React components use `PascalCase`.
- TypeScript variables and functions use `camelCase`.
- File names for React components use `PascalCase`.
- Non-component TypeScript files use `kebab-case` only for route/config-style files; otherwise prefer matching exported type/module intent consistently.
- Interfaces, DTOs, commands, queries, validators, and policies use explicit suffixes.

**Examples:**
- `ClaimSummaryDto`
- `CreateClaimCommand`
- `CreateClaimCommandValidator`
- `ClaimTimelineItem`
- `ClaimStateSummaryPanel.tsx`
- `useClaimDetails.ts`

### Structure Patterns

**Project Organization:**
- Organize backend by feature/capability, not by generic technical layer alone.
- Keep frontend organized by feature area first, with shared UI and infrastructure separated clearly.
- Co-locate tests with the unit under test only where that improves clarity; otherwise keep dedicated test projects for backend and a consistent frontend test structure.
- Shared utilities must live in explicit shared locations, not inside arbitrary feature folders.
- Domain, application, infrastructure, and API boundaries must remain explicit even if developed within a single service.

**Backend Structure Pattern:**
- `Domain` for entities, value objects, domain services, invariants
- `Application` for commands, queries, handlers, DTOs, validators, policies
- `Infrastructure` for EF Core, repositories, external integrations, identity wiring
- `Api` for endpoints, auth wiring, OpenAPI, middleware, transport mapping

**Frontend Structure Pattern:**
- `features/claims`
- `features/dashboard`
- `features/claimant-communication`
- `features/governance`
- `shared/ui`
- `shared/api`
- `shared/state`
- `shared/lib`

**File Structure Patterns:**
- Backend tests in dedicated test projects by layer or capability
- Frontend tests consistently named `*.test.ts` or `*.test.tsx`
- Environment configuration separated by runtime concern
- Static assets grouped by usage, not dumped into a flat root

### Format Patterns

**API Response Formats:**
- Successful reads return resource-shaped JSON directly unless pagination or metadata is needed.
- Paginated/list responses use a consistent envelope.
- Mutating commands return explicit success payloads or `204 No Content` where appropriate.
- Do not wrap every response in a generic `data` envelope unless metadata is required.

**Standard Paginated Response:**
- `items`
- `page`
- `pageSize`
- `totalCount`

**Standard Error Response:**
- Use RFC 7807 Problem Details as the base format.
- Extend with structured domain/application error codes where needed.
- Validation errors must return field-specific detail consistently.

**Examples:**
- `application/problem+json`
- `type`
- `title`
- `status`
- `detail`
- `instance`
- `errorCode`
- `errors`

**Data Exchange Formats:**
- JSON fields use `camelCase`
- Dates and timestamps use ISO 8601 strings in UTC
- Booleans remain true booleans, never `0/1`
- Enum values exposed over APIs should be stable string values, not integer ordinals
- Nullability must be explicit and intentional in DTOs and API contracts

### Communication Patterns

**Event System Patterns:**
- Internal domain or integration events use `PascalCase` event names in code.
- Event payloads should be versionable and explicit.
- Event classes should reflect business meaning, not transport implementation.
- Do not introduce asynchronous event infrastructure unless the use case requires it; but name events consistently from the start.

**Examples:**
- `ClaimCreated`
- `ClaimEscalated`
- `PaymentApprovalRequested`
- `ClaimantMessageApproved`

**State Management Patterns:**
- TanStack Query owns server state.
- Zustand owns lightweight client/UI state only.
- Do not duplicate server data into Zustand unless there is a clear offline or interaction reason.
- Query keys must be centralized and consistently named.
- Mutations must invalidate or update query caches deliberately, not ad hoc.

**Examples:**
- `['claims', claimId]`
- `['claims', 'timeline', claimId]`
- `['dashboard', 'supervisor']`

### Process Patterns

**Error Handling Patterns:**
- Validation failures are handled before domain execution.
- Domain invariant failures return structured business errors, not generic 500 responses.
- Infrastructure failures are logged with context but do not leak internal details to clients.
- Frontend error presentation distinguishes validation, business-rule, authorization, and system failures.

**Loading State Patterns:**
- Use query-library loading states for server data.
- Use local UI state only for transient interaction states.
- Loading indicators should be scoped to the smallest meaningful surface.
- Do not block full pages for partial refetches where localized skeletons or status indicators are sufficient.

**Validation Patterns:**
- Request shape validation uses FluentValidation.
- Business rules are enforced in domain/application layers, not only at the transport boundary.
- No controller or endpoint should contain substantial business-rule validation logic.

### Enforcement Guidelines

**All AI Agents MUST:**
- Use `camelCase` in JSON contracts and `snake_case` in PostgreSQL schema naming.
- Use RFC 7807 Problem Details as the standard API error format.
- Keep backend implementation feature-oriented with explicit domain/application/infrastructure/API boundaries.
- Keep frontend implementation feature-oriented, with server state in TanStack Query and UI state in Zustand only where justified.
- Use ISO 8601 UTC timestamps in all external contracts.
- Add validation through FluentValidation plus domain invariants rather than relying on ad hoc controller checks.
- Preserve the separation between internal workflow truth and claimant-safe external representations.

**Pattern Enforcement:**
- Pull requests and AI-generated changes should be checked against naming, folder, API, and error-format rules.
- Architectural deviations should be documented in the architecture artifact before adoption.
- New cross-cutting patterns must be added centrally, not invented locally in one feature.

### Pattern Examples

**Good Examples:**
- Backend endpoint: `GET /api/claims/{id}/timeline`
- DTO: `ClaimTimelineItemDto`
- Validator: `ApproveClaimPaymentCommandValidator`
- Query key: `['claims', 'details', claimId]`
- Table: `claimant_messages`
- Error response: Problem Details with `errorCode: "claim_blocked"`

**Anti-Patterns:**
- Mixing `snake_case`, `camelCase`, and `PascalCase` randomly in API contracts
- Returning arbitrary `{ success: false, message: "..." }` error objects
- Storing server-fetched entities in Zustand without a clear reason
- Putting domain rules directly into controllers or React components
- Naming tables `Claim`, `Claims`, and `claim_data` inconsistently across features
- Letting claimant-facing DTOs expose internal-only workflow fields

## Project Structure & Boundaries

### Complete Project Directory Structure

```text
ClaimManager/
├── README.md
├── .gitignore
├── .editorconfig
├── .env.example
├── docker-compose.yml
├── aspire.config.json
├── ClaimManager.sln
├── docs/
│   ├── architecture/
│   ├── api/
│   ├── adr/
│   └── operations/
├── .github/
│   └── workflows/
│       ├── ci.yml
│       ├── backend.yml
│       └── frontend.yml
├── ClaimManager.AppHost/
│   ├── ClaimManager.AppHost.csproj
│   └── AppHost.cs
├── ClaimManager.ServiceDefaults/
│   ├── ClaimManager.ServiceDefaults.csproj
│   └── Extensions.cs
├── src/
│   ├── ClaimManager.Api/
│   │   ├── ClaimManager.Api.csproj
│   │   ├── Program.cs
│   │   ├── Configuration/
│   │   │   ├── Authentication/
│   │   │   ├── Authorization/
│   │   │   ├── OpenApi/
│   │   │   ├── ProblemDetails/
│   │   │   └── Serialization/
│   │   ├── Endpoints/
│   │   │   ├── Claims/
│   │   │   ├── Approvals/
│   │   │   ├── Dashboard/
│   │   │   ├── ClaimantCommunication/
│   │   │   ├── Governance/
│   │   │   ├── Audit/
│   │   │   ├── Integrations/
│   │   │   └── Auth/
│   │   ├── Contracts/
│   │   │   ├── Requests/
│   │   │   ├── Responses/
│   │   │   └── Common/
│   │   └── Middleware/
│   │       ├── CorrelationIdMiddleware.cs
│   │       ├── RequestLoggingMiddleware.cs
│   │       └── ClaimantSafetyBoundaryMiddleware.cs
│   ├── ClaimManager.Application/
│   │   ├── ClaimManager.Application.csproj
│   │   ├── Common/
│   │   │   ├── Behaviors/
│   │   │   ├── Interfaces/
│   │   │   ├── Models/
│   │   │   └── Security/
│   │   ├── Claims/
│   │   │   ├── Commands/
│   │   │   ├── Queries/
│   │   │   ├── Dtos/
│   │   │   └── Validators/
│   │   ├── Approvals/
│   │   │   ├── Commands/
│   │   │   ├── Queries/
│   │   │   ├── Dtos/
│   │   │   └── Validators/
│   │   ├── Dashboard/
│   │   │   ├── Queries/
│   │   │   └── Dtos/
│   │   ├── ClaimantCommunication/
│   │   │   ├── Commands/
│   │   │   ├── Queries/
│   │   │   ├── Dtos/
│   │   │   ├── Transformers/
│   │   │   └── Validators/
│   │   ├── Governance/
│   │   │   ├── Commands/
│   │   │   ├── Queries/
│   │   │   └── Dtos/
│   │   ├── Audit/
│   │   │   ├── Queries/
│   │   │   └── Dtos/
│   │   └── Identity/
│   │       ├── Commands/
│   │       ├── Queries/
│   │       └── Dtos/
│   ├── ClaimManager.Domain/
│   │   ├── ClaimManager.Domain.csproj
│   │   ├── Common/
│   │   │   ├── Base/
│   │   │   ├── Errors/
│   │   │   ├── Events/
│   │   │   └── ValueObjects/
│   │   ├── Claims/
│   │   │   ├── Entities/
│   │   │   ├── Enums/
│   │   │   ├── Events/
│   │   │   ├── Services/
│   │   │   └── ValueObjects/
│   │   ├── Approvals/
│   │   │   ├── Entities/
│   │   │   ├── Enums/
│   │   │   └── Policies/
│   │   ├── Blockers/
│   │   │   ├── Entities/
│   │   │   ├── Enums/
│   │   │   └── Policies/
│   │   ├── ClaimantCommunication/
│   │   │   ├── Entities/
│   │   │   ├── Policies/
│   │   │   └── ValueObjects/
│   │   ├── Governance/
│   │   │   ├── Entities/
│   │   │   └── Policies/
│   │   └── Audit/
│   │       └── Entities/
│   ├── ClaimManager.Infrastructure/
│   │   ├── ClaimManager.Infrastructure.csproj
│   │   ├── Persistence/
│   │   │   ├── ClaimManagerDbContext.cs
│   │   │   ├── Configurations/
│   │   │   ├── Migrations/
│   │   │   ├── Repositories/
│   │   │   └── Querying/
│   │   ├── Identity/
│   │   │   ├── ClaimManagerUser.cs
│   │   │   ├── ClaimManagerRole.cs
│   │   │   ├── IdentityDbContextExtensions.cs
│   │   │   └── Services/
│   │   ├── Integrations/
│   │   │   ├── PolicySystem/
│   │   │   ├── PaymentSystem/
│   │   │   ├── DocumentRepository/
│   │   │   ├── Messaging/
│   │   │   └── Sso/
│   │   ├── Observability/
│   │   │   ├── Logging/
│   │   │   ├── Metrics/
│   │   │   └── Tracing/
│   │   ├── OutboundCommunication/
│   │   └── Time/
│   └── ClaimManager.Frontend/
│       ├── package.json
│       ├── tsconfig.json
│       ├── vite.config.ts
│       ├── eslint.config.js
│       ├── index.html
│       ├── public/
│       │   └── assets/
│       └── src/
│           ├── main.tsx
│           ├── app/
│           │   ├── router/
│           │   ├── providers/
│           │   ├── layouts/
│           │   └── styles/
│           ├── features/
│           │   ├── claims/
│           │   │   ├── api/
│           │   │   ├── components/
│           │   │   ├── routes/
│           │   │   ├── state/
│           │   │   └── types/
│           │   ├── approvals/
│           │   ├── dashboard/
│           │   ├── claimant-communication/
│           │   ├── governance/
│           │   ├── audit/
│           │   └── auth/
│           ├── shared/
│           │   ├── api/
│           │   ├── lib/
│           │   ├── state/
│           │   ├── types/
│           │   └── ui/
│           └── test/
│               ├── setup/
│               └── utils/
├── tests/
│   ├── ClaimManager.Api.UnitTests/
│   ├── ClaimManager.Application.UnitTests/
│   ├── ClaimManager.Domain.UnitTests/
│   ├── ClaimManager.Infrastructure.IntegrationTests/
│   ├── ClaimManager.ArchitectureTests/
│   ├── ClaimManager.Api.FunctionalTests/
│   └── ClaimManager.Frontend.Tests/
└── scripts/
    ├── dev/
    ├── ci/
    ├── db/
    └── docker/
```

### Architectural Boundaries

**API Boundaries:**
- `ClaimManager.Api` is the only HTTP API boundary for the main application service.
- Endpoints are grouped by business capability, not by transport primitive.
- Authentication, authorization, OpenAPI, serialization, and Problem Details live only at the API boundary.
- No domain or EF entities cross the API boundary directly.

**Component Boundaries:**
- Frontend feature folders own their routes, feature-specific components, local state helpers, and API adapters.
- Shared UI components must be generic and reusable; workflow-specific components remain in their owning feature unless promoted deliberately.
- Domain-specific workflow UI such as claim summaries, blocker cards, and claimant-safe explanation panels should live in their appropriate feature modules first.

**Service Boundaries:**
- The initial backend remains one primary service for operational simplicity.
- Internal boundaries are enforced through `Domain`, `Application`, `Infrastructure`, and `Api` projects.
- External system integrations are isolated under `Infrastructure/Integrations`.
- Claimant-safe transformation logic is not mixed into generic infrastructure or API mapping code; it has its own application/domain boundary.

**Data Boundaries:**
- `Infrastructure/Persistence` owns EF Core mappings, migrations, and repository/query implementation.
- `Domain` defines business meaning; `Infrastructure` defines storage shape.
- Identity persistence remains isolated from core claims persistence concerns even if stored in the same PostgreSQL database.
- Claimant-facing data models are explicitly separate from internal workflow entities and read models.

### Requirements to Structure Mapping

**Feature/Epic Mapping:**
- Claim intake and claim file management:
  - Backend: `Application/Claims`, `Domain/Claims`, `Infrastructure/Persistence`
  - Frontend: `features/claims`
- Workflow guidance and blocker progression:
  - Backend: `Application/Claims`, `Application/Approvals`, `Domain/Blockers`
  - Frontend: `features/claims`, `features/approvals`
- Supervisor control and dashboards:
  - Backend: `Application/Dashboard`
  - Frontend: `features/dashboard`
- Claimant-safe communication:
  - Backend: `Application/ClaimantCommunication`, `Domain/ClaimantCommunication`
  - Frontend: `features/claimant-communication`
- Governance and permissions:
  - Backend: `Application/Governance`, `Infrastructure/Identity`
  - Frontend: `features/governance`, `features/auth`
- Auditability:
  - Backend: `Application/Audit`, `Domain/Audit`, `Infrastructure/Observability`
  - Frontend: `features/audit`
- Integrations:
  - Backend: `Infrastructure/Integrations`
  - Frontend: usually indirect through feature APIs, not direct vendor coupling

**Cross-Cutting Concerns:**
- Authentication:
  - `Infrastructure/Identity`
  - `Api/Configuration/Authentication`
  - `Frontend/features/auth`
- Authorization:
  - `Application/Common/Security`
  - `Api/Configuration/Authorization`
- Validation:
  - `Application/*/Validators`
- Error handling:
  - `Api/Middleware`
  - `Frontend/shared/api`
- Observability:
  - `Infrastructure/Observability`
  - `ClaimManager.ServiceDefaults`
- Claimant-safe boundary:
  - `Application/ClaimantCommunication/Transformers`
  - `Domain/ClaimantCommunication/Policies`

### Integration Points

**Internal Communication:**
- API endpoints call application-layer commands and queries.
- Application layer depends on domain abstractions and infrastructure interfaces.
- Infrastructure fulfills persistence, identity, and integration contracts.
- Frontend communicates only through HTTP APIs and typed API clients.

**External Integrations:**
- Policy system integration boundary: `Infrastructure/Integrations/PolicySystem`
- Payment system integration boundary: `Infrastructure/Integrations/PaymentSystem`
- Document repository boundary: `Infrastructure/Integrations/DocumentRepository`
- Messaging boundary: `Infrastructure/Integrations/Messaging`
- Future SSO federation boundary: `Infrastructure/Integrations/Sso`

**Data Flow:**
- User interaction enters via frontend feature route
- Frontend calls typed API client
- API endpoint maps request into application command/query
- Application executes domain logic and infrastructure interactions
- Persistence stores internal workflow truth
- Claimant-safe transformation occurs before any outbound claimant communication payload is generated
- API returns DTOs only, never internal entities

### File Organization Patterns

**Configuration Files:**
- Root-level files for repo-wide tooling only
- Service-specific configuration stays inside the service/project that owns it
- Environment examples live at root, but runtime-specific binding is local to each service
- CI/CD configuration stays in `.github/workflows`

**Source Organization:**
- Backend by business capability plus clean architectural boundaries
- Frontend by feature area plus shared infrastructure
- Shared code only promoted when reused by multiple features
- No generic `helpers` dumping ground without clear ownership

**Test Organization:**
- Backend unit tests separated by architectural layer
- Integration and functional tests separated from unit tests
- Frontend tests grouped under frontend ownership, not mixed into backend test projects
- Architecture tests enforce dependency rules and boundary compliance

**Asset Organization:**
- Frontend static assets under `public/assets`
- Feature-owned visual assets may live within feature folders if tightly coupled
- No backend static asset sprawl unless explicitly part of publish strategy

### Development Workflow Integration

**Development Server Structure:**
- Aspire AppHost orchestrates frontend, backend, and local infrastructure
- The frontend runs as a Vite app
- The backend runs as the main ASP.NET Core service
- PostgreSQL is added as an Aspire-managed dependency in development

**Build Process Structure:**
- Backend projects build independently but are coordinated via solution and AppHost
- Frontend builds through Vite
- CI runs backend tests, frontend tests, architecture tests, then packaging
- Deployment-time migrations are produced from the infrastructure persistence layer

**Deployment Structure:**
- Initial deployment uses containerized service packaging
- The backend and frontend can remain separately deployable even if locally orchestrated together
- PostgreSQL remains an external managed dependency in non-local environments
- This structure leaves room for future decomposition without reorganizing the entire repo

## Architecture Validation Results

### Coherence Validation 

**Decision Compatibility:**
- The selected stack is compatible end-to-end: Aspire AppHost can orchestrate the ASP.NET Core backend, React frontend, and PostgreSQL development dependency cleanly.
- EF Core 9 with PostgreSQL 18 is aligned with the chosen persistence strategy.
- React + Vite + TanStack Query + Zustand is consistent with the UX need for responsive operational dashboards and workflow-heavy screens.
- REST + OpenAPI aligns with the current integration posture and the need for explicit claimant-safe boundary contracts.
- Custom authentication now, with future SSO abstraction later, is compatible with the identity and authorization layering already defined.
- No contradictory technology or deployment decisions remain open at the architecture level.

**Pattern Consistency:**
- Naming conventions are internally consistent across database, API, backend code, and frontend code.
- Error handling patterns align with ASP.NET Core Problem Details and the frontend API layer expectations.
- State-management rules clearly separate server state from local UI state.
- Validation and domain invariant rules reinforce the application/domain boundary rather than undermining it.
- Claimant-safe transformation is consistently treated as a dedicated boundary instead of an ad hoc UI or controller concern.

**Structure Alignment:**
- The project structure supports the chosen architecture without forcing early microservice decomposition.
- Backend boundaries map cleanly to the selected implementation patterns.
- Frontend feature-based structure supports the selected UX direction and workflow complexity.
- Integration points are isolated in infrastructure, reducing coupling to external systems.
- The structure leaves room for future scaling or decomposition without invalidating current implementation guidance.

### Requirements Coverage Validation 

**Epic/Feature Coverage:**
- Claim intake, claim workflow, blocker management, approvals, dashboards, claimant communication, governance, auditability, and integrations all have explicit architectural homes.
- Cross-cutting concerns such as auth, authorization, validation, observability, and claimant-safe communication are mapped to concrete locations.
- No major product capability from the PRD or UX specification is missing from the architecture.

**Functional Requirements Coverage:**
- The architecture supports operational claim handling, workflow progression, supervisory oversight, claimant communication, and audit history.
- The architecture supports role-aware interactions and internal/external representation separation.
- The architecture supports future external integrations without forcing those integrations into the core domain model prematurely.

**Non-Functional Requirements Coverage:**
- Security is addressed through Identity, authorization boundaries, auditability, and claimant-safe communication separation.
- Performance is addressed at the architectural level through feature-oriented query design, TanStack Query caching, scoped loading states, and a simple initial deployment topology.
- Accessibility and UX consistency are supported through the frontend shared UI and app/provider/layout structure.
- Containerized, cloud-agnostic deployment is supported through the AppHost-oriented development model and explicit service boundaries.
- Operational traceability is supported through observability and audit structure decisions.

### Implementation Readiness Validation 

**Decision Completeness:**
- Critical architecture decisions are documented with concrete technology selections and versions where relevant.
- Core implementation patterns are defined clearly enough to constrain AI-generated changes.
- Consistency rules are enforceable and include examples.
- Boundary-sensitive areas such as auth, validation, errors, and claimant-safe communication are specified.

**Structure Completeness:**
- The project structure is concrete and implementation-oriented rather than generic.
- Core files, folders, projects, and responsibility boundaries are defined.
- Integration points and ownership boundaries are explicit.
- The structure is detailed enough for multiple AI agents to place code consistently.

**Pattern Completeness:**
- The major conflict points for AI implementation drift have been addressed.
- Naming, structure, communication, data formatting, validation, loading, and error patterns are all covered.
- API and persistence boundary rules are explicit.
- Frontend and backend ownership lines are sufficiently clear for implementation to begin.

### Gap Analysis Results

**Critical Gaps:**
- None identified.

**Important Gaps:**
- None blocking implementation.
- Exact external integration contracts for policy, payment, messaging, and document systems remain implementation-time details, but their architectural boundaries are already defined.

**Nice-to-Have Gaps:**
- Add ADRs for future identity federation migration and any later service decomposition decisions.
- Add a canonical API error-code catalog once initial workflows are implemented.
- Add architecture tests early to enforce dependency direction and boundary rules automatically.

### Validation Issues Addressed

- Confirmed that the move away from the obsolete ASP.NET Core React template to Aspire resolves starter-template risk.
- Confirmed that custom authentication now does not conflict with future SSO abstraction if authorization remains policy-driven.
- Confirmed that the selected frontend state approach avoids duplicated server-state ownership.
- Confirmed that claimant-safe communication is treated as a distinct architectural concern rather than mixed into generic endpoint mapping.

### Architecture Completeness Checklist

**Requirements Analysis**

- [x] Project context thoroughly analyzed
- [x] Scale and complexity assessed
- [x] Technical constraints identified
- [x] Cross-cutting concerns mapped

**Architectural Decisions**

- [x] Critical decisions documented with versions
- [x] Technology stack fully specified
- [x] Integration patterns defined
- [x] Performance considerations addressed

**Implementation Patterns**

- [x] Naming conventions established
- [x] Structure patterns defined
- [x] Communication patterns specified
- [x] Process patterns documented

**Project Structure**

- [x] Complete directory structure defined
- [x] Component boundaries established
- [x] Integration points mapped
- [x] Requirements to structure mapping complete

### Architecture Readiness Assessment

**Overall Status:** READY FOR IMPLEMENTATION

**Confidence Level:** high

**Key Strengths:**
- Strong alignment between product workflow complexity and feature-oriented system structure
- Clear separation between internal claim workflow truth and claimant-safe external communication
- Explicit consistency rules that reduce AI-agent implementation drift
- Practical initial deployment shape that avoids premature fragmentation
- Clean path from current custom auth to later federation if needed

**Areas for Future Enhancement:**
- Formalize ADRs for future identity federation and scaling decisions
- Add automated architecture tests as soon as the solution skeleton exists
- Define external integration contracts and error-code taxonomy during early implementation

### Implementation Handoff

**AI Agent Guidelines:**
- Follow all architectural decisions exactly as documented
- Use implementation patterns consistently across all components
- Respect project structure and boundaries
- Refer to this document for all architectural questions

**First Implementation Priority:**
- Scaffold the ClaimManager solution from the Aspire TypeScript/C# starter shape, establish the solution/project structure, wire PostgreSQL, and create the initial claims vertical slice with the approved boundary and naming conventions.