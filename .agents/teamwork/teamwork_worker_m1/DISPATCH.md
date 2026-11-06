## 2026-11-06T09:55:52Z
You are the Lead Implementation Worker for the EduPlatform LMS Architectural Advisory & Production Roadmap project.

Your Working Directory: D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_worker_m1
Project Root: D:\avi\Project\MVC\LMS
Deliverable Target File: D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

MANDATORY FIRST STEPS:
1. Read D:\avi\Project\MVC\LMS\.agents\teamwork\ORIGINAL_REQUEST.md
2. Read D:\avi\Project\MVC\LMS\.agents\teamwork\PROJECT.md
3. Read the three Explorer survey handoff reports:
   - D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_explorer_survey_1\handoff.md (ASP.NET MVC 5, EF6, Security, UI/UX)
   - D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_explorer_survey_2\handoff.md (AI Microservice, FastAPI, Hybrid RAG, Qdrant, Gemini API)
   - D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_explorer_survey_3\handoff.md (Database, DLP, Backup, Free-Tier Hosting Topology)

YOUR MISSION:
Write the complete, comprehensive, highly detailed, production-grade master deliverable file:
`D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md`

STRICT CONSTRAINTS & REQUIREMENTS:
- Every recommended tool, service, database pattern, cloud tier, and script MUST be 100% free of cost indefinitely (open source or perpetual free tier). Absolutely NO paid subscriptions, paid cloud backups, or enterprise software.
- The document must be specifically tailored to the real EduPlatform LMS codebase (ASP.NET MVC 5, .NET Framework 4.8, EF6, SQL Server, Python FastAPI, Qdrant, Gemini API). Ground all code snippets, DDL scripts, C# classes, and configurations in real file paths and class names observed by the explorers.
- Provide production-grade, copy-paste ready implementations for all critical fixes and automation scripts (not pseudo-code or placeholders):
  * Security fixes: C# code for AccountController password reset token validation and token stripping, CertificateController BOLA/IDOR authorization check, AccountController lockout parameter, Web.config security settings, FastAPI path traversal canonicalization, and constant-time API key verification.
  * EF6 and Database: Complete DDL for missing stored procedure `dbo.sp_MarkLessonComplete`, DDL for composite unique indexes on `Enrollments` and `LessonProgress`, C# code for `.AsNoTracking()` and navigation properties, connection string pool configuration.
  * DLP and Backup: Complete PowerShell backup script (`LmsBackup.ps1`) for SQL Server Express with `WITH COMPRESSION`, `WITH CHECKSUM`, `DBCC CHECKDB`, and local retention pruning; Task Scheduler setup; complete Python script (`qdrant_backup.py`) for Qdrant snapshots; deterministic vector rebuild script from `dbo.AI_DocumentChunks`; and `rclone` sync to Cloudflare R2 (10 GB free).
  * AI Microservice & RAG: Embedding dimension fix (768 for text-embedding-004), model tag update, BM25 scroll pagination fix, Reciprocal Rank Fusion (RRF) tuning, grounded prompt template with inline citations, exponential backoff with full jitter for Gemini 429 errors, persistent semantic caching with DiskCache, and RAG Triad evaluation script.
  * UI/UX & a11y: Exact CSS contrast updates (#64748B -> #CBD5E1), semantic `<button>` refactoring for AI launcher in `_ChatBotWidget.cshtml`, `aria-live="polite"` region for streaming messages.
  * Free Production Deployment: Full architecture blueprint for Cloudflare Free Tier (Universal SSL, WAF, Turnstile, Cloudflare Tunnel) + Self-Hosted Windows IIS & SQL Express + Oracle Cloud Always Free ARM64 VM (FastAPI + Qdrant in Docker) + GitHub Actions CI/CD workflow YAML.
  * Health & Monitoring: C# `HealthController.cs` implementation, FastAPI `/api/v1/health` implementation, UptimeRobot setup, and Serilog/Loguru rolling file logging.

DELIVERABLE OUTPUT:
- Write `D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md`. Ensure it is exhaustive, complete, and contains all 8 required sections.
- Keep progress updated in `D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_worker_m1\progress.md`.
- Write your final completion handoff to `D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_worker_m1\handoff.md`.
- Send a completion message back to the orchestrator when finished.
