# Project: EduPlatform LMS Production Roadmap & Architectural Advisory

## Architecture
- **Web Application Layer**: ASP.NET MVC 5 on .NET Framework 4.8, OWIN SystemWeb authentication, Razor views, Bootstrap 5 / customized dark theme, Rotativa (wkhtmltopdf) PDF export.
- **ORM & Relational Database**: Entity Framework 6.4.4 DbContext (`ApplicationDbContext`), SQL Server Express / SQL Server on Windows / Linux, 35 schema tables, Identity 2.0.
- **AI Microservice**: Python FastAPI on port 8000 (`AIService/`), Google GenAI SDK (`text-embedding-004` and `gemini-2.0-flash` / `gemini-1.5-flash`), Qdrant Vector Store (on-disk SQLite mode migrating to standalone Qdrant container), BM25 Sparse Index, Reciprocal Rank Fusion / Hybrid Reranker, Cosine In-Memory / DiskCache semantic cache, Pydantic v2 schemas.
- **Integration Layer**: `Services/AiServiceClient.cs` via `HttpClient` calling `AIService/api/v1/*` using `X-Internal-API-Key` header.
- **Asset & Media Storage**: Cloudinary (free tier 25 GB) and local `App_Data/uploads` / `Content/uploads`.
- **Target Production Topology (100% Free Tier)**:
  * Edge: Cloudflare Free Tier (Universal SSL, DNS, DDoS mitigation, WAF custom rules, Cloudflare Turnstile, Cloudflare Tunnel).
  * Web & DB: Self-hosted Windows Server / Windows Workstation (IIS + SQL Server Express) exposed securely via `cloudflared` tunnel without inbound port forwarding.
  * AI & Vector Services: Oracle Cloud Infrastructure (OCI) Always Free ARM64 Ampere VM (4 OCPUs, 24 GB RAM, 200 GB NVMe storage) hosting Dockerized FastAPI, standalone Qdrant, and Prometheus/Grafana.
  * Offsite DLP / Backup: Cloudflare R2 (10 GB free tier with $0 egress) synced via `rclone` and Windows Task Scheduler PowerShell scripts.

---

## Feature Inventory (Requirements R1-R8 Mapping)
Every requirement from ORIGINAL_REQUEST.md is cataloged below with its source and milestone mapping:

| # | Feature | Description | Milestone | Source |
|---|---------|-------------|-----------|--------|
| F1 | Master Production Roadmap Document (R1) | Complete, structured, end-to-end `PRODUCTION_ROADMAP.md` covering all 8 areas specifically grounded in LMS codebase | M1 | ORIGINAL_REQUEST §R1 |
| F2 | Modular Service Boundaries & Decoupling (R2) | Refactoring ChatBotController (3,089 LOC) into distinct domain services (IAdvisorIntentService, IOfflineCurriculumService, ISocraticGradingService) | M1 | ORIGINAL_REQUEST §R2, Explorer 1 |
| F3 | Background Job Processing (R2) | Zero-cost Hangfire implementation on SQL Server Express for async PDF ingestion, email notifications, and certificate generation | M1 | ORIGINAL_REQUEST §R2, Explorer 1 |
| F4 | AI Microservice Feature Expansions (R2) | Multi-format document ingestion (PDF, DOCX, TXT), automated rubric generation, adaptive quizzing, and Socratic tutoring | M1 | ORIGINAL_REQUEST §R2, Explorer 2 |
| F5 | API Rate Limiting & Throttling (R3) | Dual-layer throttling: Cloudflare WAF rate rules at edge, AspNetWebApi2 / custom MVC ActionFilter token-bucket, and FastAPI slowapi / Redis-free token bucket | M1 | ORIGINAL_REQUEST §R3, Explorers 1 & 2 |
| F6 | Authentication & Authorization Hardening (R3) | Fix AccountController password reset token leak and omission; fix CertificateController BOLA/IDOR; enable ASP.NET Identity lockout (shouldLockout=true); secure X-Internal-API-Key with constant-time comparison | M1 | ORIGINAL_REQUEST §R3, Explorers 1 & 2 |
| F7 | Vulnerability Protection & Input Validation (R3) | Mitigate SQLi (EF6 parameterized queries), XSS (AntiXssEncoder, CSP headers), CSRF (ValidateAntiForgeryToken enforcement), Prompt Injection (input guardrails, sandwich defense), and SSRF/Path Traversal (file path canonicalization in AIService) | M1 | ORIGINAL_REQUEST §R3, Explorers 1 & 2 |
| F8 | Free Edge & Network Defense (R3) | Cloudflare Free Tier architecture (WAF managed rules, bot fight mode, Turnstile captcha on login/reset, Cloudflare Tunnel) | M1 | ORIGINAL_REQUEST §R3, Explorer 3 |
| F9 | Automated SQL Server Backup & Retention (R4) | Zero-cost PowerShell backup engine (LmsBackup.ps1) with Task Scheduler, WITH COMPRESSION, WITH CHECKSUM, DBCC CHECKDB verification, and local 7-day retention | M1 | ORIGINAL_REQUEST §R4, Explorer 3 |
| F10 | Qdrant Vector Persistence & DR (R4) | Standalone Qdrant containerization, REST API snapshot automation (qdrant_backup.py), and deterministic rebuild script from dbo.AI_DocumentChunks | M1 | ORIGINAL_REQUEST §R4, Explorers 2 & 3 |
| F11 | Offsite Sync & Failover Readiness (R4) | Rclone synchronization to Cloudflare R2 (10 GB free, zero egress) and step-by-step zero-cost disaster recovery runbooks | M1 | ORIGINAL_REQUEST §R4, Explorer 3 |
| F12 | Hybrid RAG Optimization (R5) | Fix embedding dimension mismatch (768 for text-embedding-004), cursor pagination for BM25 synchronization, semantic chunking (400-500 tokens with 10% overlap), and Reciprocal Rank Fusion (RRF) tuning | M1 | ORIGINAL_REQUEST §R5, Explorer 2 |
| F13 | Prompt Engineering & Hallucination Defense (R5) | Structured prompt templates with citation enforcement, temperature tuning (0.2 for retrieval, 0.7 for creative), few-shot exemplar grounding, and confidence thresholding | M1 | ORIGINAL_REQUEST §R5, Explorer 2 |
| F14 | Gemini API Free Tier Quota Management (R5) | Quota optimization (15 RPM, 1M TPM, 1,500 RPD), exponential backoff with full jitter, and deterministic local fallback cascade | M1 | ORIGINAL_REQUEST §R5, Explorer 2 |
| F15 | Evaluation Rubrics & Metrics (R5) | RAG Triad evaluation framework (Context Relevance, Groundedness, Answer Relevance) implementable with free synthetic test harness | M1 | ORIGINAL_REQUEST §R5, Explorer 2 |
| F16 | EF6 Query Optimization & Indexing (R6) | Eliminate Cartesian explosions in CourseService/LearningService, add AsNoTracking(), resolve N+1 via navigation properties, add missing sp_MarkLessonComplete, and deploy composite unique indexes | M1 | ORIGINAL_REQUEST §R6, Explorers 1 & 3 |
| F17 | Connection Pooling & Traffic Optimization (R6) | SQL Server connection pooling string tuning, HTTP connection pooling via static HttpClient / HttpClientFactory, and response compression | M1 | ORIGINAL_REQUEST §R6, Explorers 1 & 3 |
| F18 | Semantic Cache Layer Optimization (R6) | Upgrade ephemeral in-memory cache to persistent zero-cost DiskCache or SQLite-vec with cosine similarity >= 0.95 and course-level invalidation hooks | M1 | ORIGINAL_REQUEST §R6, Explorer 2 |
| F19 | Single vs Multi-Database Scaling Strategy (R6) | Empirical evaluation of 10 GB SQL Server Express limits, offloading strategies (Cloudinary + Qdrant), and zero-cost 2-database scaling topology (LMS_Core + LMS_AI) | M1 | ORIGINAL_REQUEST §R6, Explorer 3 |
| F20 | UI/UX & Responsive Layout Enhancements (R7) | Modern responsive design patterns, CSS modernization, mobile drawer navigation, card layouts, and micro-interactions | M1 | ORIGINAL_REQUEST §R7, Explorer 1 |
| F21 | Accessibility (a11y) & WCAG 2.1 AA Compliance (R7) | Text contrast remediation (#64748B -> #CBD5E1), semantic button refactoring for AI launcher, aria-live polite regions for AI streaming responses, and keyboard focus states | M1 | ORIGINAL_REQUEST §R7, Explorer 1 |
| F22 | 100% Free-Tier Production Deployment Architecture (R8) | Step-by-step deployment blueprint for Windows IIS + SQL Express + OCI Always Free ARM64 (FastAPI + Qdrant) + Cloudflare Tunnel + GitHub Actions CI/CD | M1 | ORIGINAL_REQUEST §R8, Explorer 3 |
| F23 | Maintenance Checklists, Health Checks & Telemetry (R8) | C# /Health endpoint, FastAPI /api/v1/health endpoint, UptimeRobot 5-min ping, Serilog/Loguru rolling structured logging, and operational runbooks | M1 | ORIGINAL_REQUEST §R8, Explorers 1 & 3 |

---

## Milestones
| # | Name | Scope | Dependencies | Status |
|---|------|-------|-------------|--------|
| M1 | Production Roadmap Deliverable (`PRODUCTION_ROADMAP.md`) | Full technical authoring of PRODUCTION_ROADMAP.md covering F1-F23 with actionable code, scripts, configurations, and verification steps | Phase 0 Survey Reports | DONE |

---

## Code Layout
- Deliverable File: `D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md` (Project Root)
- Metadata & Reports: `D:\avi\Project\MVC\LMS\.agents\teamwork\`
- ASP.NET Application Root: `D:\avi\Project\MVC\LMS\`
- AI Microservice Root: `D:\avi\Project\MVC\LMS\AIService\`
- Database Scripts: `D:\avi\Project\MVC\LMS\Database\`

---

## Interface Contracts & Technical Requirements for `PRODUCTION_ROADMAP.md`
- **Format**: Markdown with exhaustive technical depth, code snippets (C#, Python, SQL, PowerShell, YAML), mathematical formulas, architectural diagrams (ASCII / Mermaid), configuration blocks, and operational checklists.
- **Tone & Rigor**: Professional architectural advisory document, eliminating all placeholders, generic hand-waving, or dummy content.
- **Free-Tier Constraint**: Every single tool, framework, cloud service, database pattern, and script MUST be 100% free of cost indefinitely (open source or perpetual free tiers). Zero paid cloud or enterprise licenses allowed.
