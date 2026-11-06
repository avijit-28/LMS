# Handoff Report: Adversarial Challenge 2 (AI Microservice, Hybrid RAG, Deployment & Observability)

**Agent**: Challenger 2 (`teamwork_challenger_2`)  
**Target Deliverable**: `D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md`  
**Verdict**: **REQUEST_CHANGES**  
**Timestamp**: 2026-11-06T10:15:00Z  

---

## 1. Observation

Direct observations from `PRODUCTION_ROADMAP.md`, test harness executions, and database schemas:

1. **Jitter Backoff Algorithm**:
   In `PRODUCTION_ROADMAP.md` lines 1210-1230:
   `base_delay = 1.0`, `max_delay = 8.0`, `max_retries = 3`.
   Formula: `sleep_time = random.uniform(0, min(max_delay, base_delay * (2 ** attempt)))`.
   *Empirical Execution*: `node test_jitter_backoff.js`
   `Min total backoff before Groq cascade: 0.10s | Max: 2.96s | Avg: 1.49s`.
   Classroom burst (25 students in 5s): 15 succeeded on Gemini; 10 hit 429; 0 recovered on retry; 10 cascaded to Groq. Groq consumed 6,000 / 6,000 TPM in 4 queries; remaining 6 queries threw HTTP 429 (24% of students suffered complete failure).

2. **Timeout Cascade vs. Cloudflare Proxy Ceiling**:
   In `PRODUCTION_ROADMAP.md` lines 1207-1233 (3 retries with default 30s socket timeout) + Groq fallback (15s).
   *Empirical Execution*: `node test_timeout_cascade.js`
   Cumulative Gemini degradation duration: 91.5s; total FastAPI latency: 106.5s.
   Cloudflare Free Tier immutable proxy timeout: 100.0s. Result: Client receives HTTP 524 Gateway Timeout, while origin server continues executing zombie tasks for 6.5s.

3. **RRF Score Blindness & Schema Mismatch**:
   In `PRODUCTION_ROADMAP.md` lines 1151-1178:
   `docs[doc_id] = hit.payload` (dense hits) vs `docs[doc_id] = hit` (sparse hits).
   *Empirical Execution*: `node test_rrf_harness.js`
   Doc 102 (noise: cosine 0.18, BM25 0.05) scored 0.016129; Doc 101 (DI: cosine 0.96) scored 0.010656 (+51% inversion).
   Schema keys for sparse hits: `[id, score, text, course_id]`; keys for dense hits: `[document_id, course_id, chunk_index, text]`. Missing `document_id` triggers `KeyError: 'document_id'` downstream.

4. **SQL Server Express 10 GB Capacity Exhaustion**:
   In `Database/LMS_Complete_Database.sql` line 614: `[ChunkText] NVARCHAR(MAX) NOT NULL`.
   In `PRODUCTION_ROADMAP.md` line 1578: claims only chat logs grow.
   *Empirical Execution*: `node test_sql_express_capacity.js`
   Under 500 courses with 10 textbooks of 350 pages (5,000 documents, 2,011,420 chunks): `dbo.AI_DocumentChunks` alone consumes 9,504.88 MB (9.28 GB). Total database MDF file reaches 10,074.88 MB (98.4% of 10 GB limit), with only 165.12 MB before write failure (SQL Server Error 1105).

5. **Health Probe Secret & Topology Leakage**:
   In `PRODUCTION_ROADMAP.md` lines 1787-1818:
   `catch (Exception ex) { dbUp = false; dbError = ex.Message; }` exposed in unauthenticated `/Health`.
   *Empirical Execution*: `node test_health_leakage.js`
   Simulated connection errors leak `PROD-SQL-01\SQLEXPRESS`, `sa`, and `192.168.1.15`.
   FastAPI `/api/v1/health` publicly exposes `gemini-1.5-flash` and `llama-3.3-70b-versatile` without authentication.

6. **Cloudflare Bot Fight Mode & UptimeRobot Keyword Assertions**:
   In `PRODUCTION_ROADMAP.md` lines 704 and 1852: Bot Fight Mode is enabled; UptimeRobot monitors `/Health` asserting `"Healthy"`. When AI is down but DB is up, line 1808 returns HTTP 200 with `"status": "Degraded"`, causing keyword assertion failure and false-positive P1 outage alerts.

---

## 2. Logic Chain

1. From Observation 1, because the average backoff retry interval is 1.49 seconds (max 2.96s), retrying against a 60-second rolling quota window will never find the window reset. Therefore, all burst overflow spills over to Groq. Groq's 6,000 TPM limit accommodates only 4 RAG queries of ~1,500 tokens before rejecting further traffic. Thus, a modest classroom burst causes a 24% failure rate for students.
2. From Observation 2, because 3 retries with standard socket timeouts exceed 90 seconds, and Groq fallback takes 15 seconds, the total request time (106.5s) exceeds Cloudflare's non-configurable 100-second proxy limit. This severs the user connection with HTTP 524, causing user-facing downtime and orphaned background threads.
3. From Observation 3, because Cormack RRF ignores score magnitudes, rank accumulation across weak matches in both dense and sparse modalities reliably beats a high-scoring single modality match, elevating irrelevant noise to the top candidate. Furthermore, because dense and sparse dictionaries use different primary keys (`document_id` vs `id`), downstream consumers suffer runtime crashes.
4. From Observation 4, because `dbo.AI_DocumentChunks` stores `NVARCHAR(MAX)` for every chunk, large-scale curriculum indexing (textbooks and manuals) inflates the single database to 9.84 GB. Deferring the multi-database architecture until after 10 GB is reached creates an imminent operational risk where the entire LMS locks up.
5. From Observation 5, because `[AllowAnonymous]` is placed on `/Health` and raw `ex.Message` is rendered into JSON, database connection outages expose internal instance names, hostnames, and credentials to public crawlers.
6. From Observation 6, because UptimeRobot monitors datacenter IPs which Cloudflare Bot Fight Mode challenges, and asserts the literal string `"Healthy"` on an endpoint that legitimately emits `"Degraded"`, operators will be subjected to chronic false P1 alarms.

---

## 3. Caveats

- **FlashRank CPU Inference**: Tested within budget (~30ms on 4 ARM cores and ~60MB RAM). The concern is not runtime execution speed, but container packaging (missing `libgomp1` and runtime model downloading from Hugging Face to ephemeral `/tmp`).
- **OCI Always Free Availability**: Varies widely by geographical data center. In unconstrained secondary regions, ARM64 instances can be provisioned quickly; however, in primary hubs, capacity exhaustion is chronic.
- **Scale Assumptions**: The 9.84 GB MDF sizing calculation assumes an active academic repository of 5,000 documents (10 books per course across 500 courses). Smaller institutions with only 500 total documents will stay well below the 10 GB limit (~1.35 GB).

---

## 4. Conclusion

**Verdict: REQUEST_CHANGES**

`PRODUCTION_ROADMAP.md` is technically impressive and publication-ready in its breadth, but it contains **4 Critical and 4 High severity vulnerabilities** that must be resolved before final approval:
1. Fix AI microservice rate limiting with in-process token-bucket queuing and 45s maximum cumulative latency budgets.
2. Add relevance score gating (cosine $\ge 0.60$, BM25 $\ge 1.0$) and schema standardization to the RRF fusion function.
3. Update the database sizing model in Section 6.6 to account for `dbo.AI_DocumentChunks` and prescribe immediate adoption of the 2-database architecture (`LMS_Core` + `LMS_AI`) for textbook workloads.
4. Document the OCI 7-day idle reclamation policy (keepalive cron / PAYG $0 upgrade) and zero-cost Windows self-hosted Docker fallback.
5. Document IIS SSE streaming settings (`BufferOutput = false`, `Idle Time-out = 0`) and WebSocket heartbeats.
6. Sanitize `/Health` error outputs and isolate detailed telemetry behind `X-Internal-API-Key`.
7. Decouple UptimeRobot degradation assertions from fatal outage alerts.

---

## 5. Verification Method

To independently reproduce and verify these findings:

1. **Gemini & Groq Rate Limit Simulation**:
   ```powershell
   node C:\Users\avijitpakhira\.gemini\antigravity\scratch\challenger2_harness\test_jitter_backoff.js
   ```
   *Expected Output*: Max backoff $< 3.0$s; 100% of overflow hits Groq; Groq exhausts 6,000 TPM limit after 4 queries.

2. **Timeout Cascade Simulation**:
   ```powershell
   node C:\Users\avijitpakhira\.gemini\antigravity\scratch\challenger2_harness\test_timeout_cascade.js
   ```
   *Expected Output*: Total latency $= 106.5$s $> 100$s Cloudflare proxy timeout $\rightarrow$ HTTP 524.

3. **RRF Relevance Inversion & Schema Defect**:
   ```powershell
   node C:\Users\avijitpakhira\.gemini\antigravity\scratch\challenger2_harness\test_rrf_harness.js
   ```
   *Expected Output*: Doc 102 (noise) outranks Doc 101 by 51.3%; Doc 201 lacks `document_id`.

4. **SQL Server Express 10 GB Capacity Exhaustion**:
   ```powershell
   node C:\Users\avijitpakhira\.gemini\antigravity\scratch\challenger2_harness\test_sql_express_capacity.js
   ```
   *Expected Output*: Scenario D consumes 10,074.88 MB (98.4% of 10,240 MB limit).

5. **Health Check Secret Leakage**:
   ```powershell
   node C:\Users\avijitpakhira\.gemini\antigravity\scratch\challenger2_harness\test_health_leakage.js
   ```
   *Expected Output*: Verbatim instance names, hostnames, and model fingerprints exposed in JSON.

6. **Inspection Files**:
   - `D:\avi\Project\MVC\LMS\.agents\teamwork\teamwork_challenger_2\challenge.md`
   - `D:\avi\Project\MVC\LMS\PRODUCTION_ROADMAP.md` (Sections 5.3, 5.5, 6.6, 8.1, 8.3, 8.4)
