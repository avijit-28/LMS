## 2026-11-06T10:27:21Z
[Message] timestamp=2026-11-06T10:27:21Z sender=4aaca2b3-6eb0-46c2-ac6e-74129b3058d8 priority=MESSAGE_PRIORITY_HIGH content=You are the Lead Implementation Worker for Iteration 2 of the EduPlatform LMS Architectural Advisory & Production Roadmap project.

Your Working Directory: D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_worker_m1_it2
Target Deliverable to Update: D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md
Original Request: D:\avi\Project\MVC\LMS\.agents\teamwork\ORIGINAL_REQUEST.md
Scope Document: D:\avi\Project\MVC\LMS\.agents\teamwork\PROJECT.md
Gate Status: D:\avi\Project\MVC\LMS\.agents\teamwork\orchestrator_1\GATE_STATUS.md

REPORTS TO READ FIRST:
- D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_explorer_it2_1\handoff.md (Controller, Security & Rotativa PDF Fixes)
- D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_explorer_it2_2\handoff.md (AI Microservice, Rate Limiting & Hybrid RAG Fixes)
- D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_explorer_it2_3\handoff.md (Database DDL, DLP, Cloudflare Edge, OCI Eviction & Health Fixes)

MANDATORY INTEGRITY WARNING:
DO NOT CHEAT. All implementations must be genuine. DO NOT hardcode test results, create dummy/facade implementations, or circumvent the intended task. A teamwork_auditor will independently verify your work. Integrity violations WILL be detected and your work WILL be rejected.

YOUR MISSION:
Update `D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md` directly, seamlessly integrating all the empirical patch strategies into the deliverable:

1. **Section 3.1 & 3.2 (AccountController & CertificateController Remediations)**:
   - In `AccountController.SetNewPassword`: Ground LINQ query in `PasswordResetRequest` entity schema (`Status != "Completed" && TokenExpiry > DateTime.UtcNow`), retire tokens with `req.Status = "Completed"`, adopt atomic `UserManager.ResetPasswordAsync`, and return `ForgotPassword` view on failure.
   - In token validation: Provide true constant-time token comparison with `CryptographicOperations.FixedTimeEquals` on UTF-8 byte arrays (with fallback bitwise accumulator).
   - Address reset token delivery: Specify Hangfire background email job dispatch (`IEmailService`) via free SMTP / SendGrid/Resend free tier ($0 cost) when approved by admin, plus admin portal direct retrieval fallback.
   - In `CertificateController`: Align service calls with actual `CertificateService` contracts (`GetCertificateViewModel`), revert from `ActionAsPdf` to `Rotativa.ViewAsPdf("CertificatePdf", model)` for 100% in-memory rendering via stdin (preventing unauthenticated loopback 302 login redirects), and enforce course instructor ownership (`cert.Course.InstructorId == currentUserId`) across both View and Download actions.

2. **Section 3.6 (Cloudflare Edge & Rate Limiting)**:
   - Acknowledge Cloudflare Free's 1-rule limit: Allocate the single edge rule to `/Account/Login` (5 req / 10s period).
   - Prescribe in-process `BoundedCloudflareRateLimitAttribute` with `CF-Connecting-IP` inspection and 20,000-key capacity bounds for `/ChatBot/SendMessage`.

3. **Section 4.1 & 4.2 (DLP, Backup Automation & Qdrant)**:
   - Update `LmsBackup.ps1` with T-SQL bracket escaping (`Replace("]", "]]")`), pre-flight `SELECT 1` connectivity probe, and `$env:TEMP` emergency fallback log path.
   - Update `qdrant_backup.py` with `.tmp` streaming download, `tarfile.is_tarfile()` verification, and atomic rename.

4. **Section 5 (AI Microservice, Rate Limits & Hybrid RAG)**:
   - Add in-process sliding-window rate limiter (15 RPM / 60s window with >= 3.8s pacing) and rolling 60s memory TPM tracker (5,200 safety ceiling) for Groq.
   - Enforce aggregate 25.0s client timeout budget in FastAPI (Gemini per-call 8.0s timeout, max 2 attempts; Groq 6.0s timeout) and set ASP.NET HttpClient timeout to 30.0s (guaranteeing completion well within Cloudflare's 100s limit).
   - Add dual relevance score gating (dense cosine >= 0.40, BM25 >= 1.0) to eliminate weak dual noise inversion, and normalize payload dictionary access (`id` vs `document_id`) in `fuse_and_rerank`.
   - Replace Python's randomized `hash()` with deterministic 16-char `hashlib.sha256` keys surviving Uvicorn restarts, and optimize course lookups with course-level index.

5. **Section 6 (Database, DDL & 10GB Express Limit)**:
   - Update `dbo.sp_MarkLessonComplete` with active enrollment verification guard (`IF NOT EXISTS (SELECT 1 FROM dbo.Enrollments ...)`), protect against 0% regression on `@TotalLessons = 0`, and align completion percentage with curriculum components.
   - Address SQL Server Express 10 GB limit under textbook RAG: document chunk text compression / hash-only storage and the 2-database split (`LMS_Core` + `LMS_AI`).

6. **Section 8 (Free Production Deployment, Health Probes & Telemetry)**:
   - Document OCI Always Free ARM64 eviction policy (<20% CPU/RAM for 7 days), provide `oci-keepalive.service` daemon, and document PAYG $0 upgrade exemption + self-hosted Docker fallback.
   - Configure Cloudflare Tunnel / IIS SSE buffering (`Response.BufferOutput = false`, `Response.Headers.Add("X-Accel-Buffering", "no")`, `Response.Flush()`), WebSocket 30s ping, and IIS `AlwaysRunning`.
   - Sanitize unauthenticated `/Health` endpoint to prevent leaking `ex.Message` or SQL instance names (`SQLEXPRESS`), gate diagnostics behind `X-Health-Key`, and configure Cloudflare WAF skip rules for UptimeRobot secret headers.

DELIVERABLES:
- Update `D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md` in place.
- Write handoff to `D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_worker_m1_it2\handoff.md`.
- Send completion message back to orchestrator.
