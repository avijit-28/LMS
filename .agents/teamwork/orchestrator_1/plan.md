# Orchestration Plan: EduPlatform LMS Architectural Advisory & Production Roadmap

## Objective
Produce a complete, technically rigorous, and 100% free-tier/open-source `PRODUCTION_ROADMAP.md` tailored specifically to the EduPlatform LMS existing architecture (ASP.NET MVC 5, .NET Framework 4.8, EF6, SQL Server, Python FastAPI, Qdrant, Gemini API).

## Phases

### Phase 0: Technical Stack Survey (3 Parallel Explorers)
- **Explorer 1 (ASP.NET MVC & EF6 Architecture)**: Inspect web application architecture, MVC controllers, routing, EF6 DbContext, models, authentication/identity, configuration, database access patterns, and security middlewares.
- **Explorer 2 (AI Microservice & RAG Architecture)**: Inspect Python FastAPI service, Hybrid RAG implementation (BM25, Qdrant vectors), Gemini API integration, caching, endpoints, prompt templates, and data schemas.
- **Explorer 3 (Infrastructure, Deployment, Database & Scripts)**: Inspect database migration scripts, SQL Server schemas/stored procedures, docker/container configs, environment variables, scripts, and existing deployment assets.

### Phase 1: Survey Synthesis & Feature Inventory (PROJECT.md)
- Aggregate all 3 explorer reports into `.agents/teamwork/PROJECT.md`.
- Formulate Architecture description, Feature Inventory (R1-R8 mapping), and Section Contracts for `PRODUCTION_ROADMAP.md`.

### Phase 2: Implementation (Worker Dispatch)
- Dispatch `teamwork_worker` armed with domain knowledge and explorer findings to author `D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md` adhering strictly to all requirements R1-R8.
- Worker must write complete, actionable code snippets, scripts, configurations, and step-by-step guides without enterprise/paid dependencies.

### Phase 3: Multi-Agent Review & Challenge (2 Reviewers, 2 Challengers)
- Dispatch 2 independent Reviewers to verify completeness, correctness, free-tier compliance, and architectural depth against R1-R8.
- Dispatch 2 Challengers to stress-test the architectural recommendations (e.g. rate-limiting corner cases, EF6 N+1, vector DB backup scripts, free-tier limits, security vectors).

### Phase 4: Forensic Audit & Gate
- Dispatch `teamwork_auditor` to perform forensic integrity check on `PRODUCTION_ROADMAP.md`.
- Ensure no dummy content, placeholder text, hand-waving, or paid services are proposed.
- Evaluate gate status in `GATE_STATUS.md`.

### Phase 5: Synthesis & Reporting
- Synthesize all findings and report final completion back to parent agent via `send_message`.
