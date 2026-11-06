# EduPlatform LMS — AI Microservice Architectural Survey & Deep-Dive Report

**Date**: 2026-11-06  
**Investigator**: Teamwork Explorer (Survey 2)  
**Target Systems**: `AIService/`, `Rag/`, `Services/AiServiceClient.cs`, `Controllers/ChatBotController.cs`, `Controllers/InstructorController.cs`  
**Focus Areas**: Python FastAPI Layout, Hybrid RAG (Dense Qdrant + Sparse BM25 + RRF), Gemini API Integration, Semantic Caching, and Microservice Security.

---

## 1. Executive Summary & Architecture Overview

EduPlatform LMS features a sophisticated, multi-tiered AI architecture designed to support academic question-answering, Socratic tutoring, code debugging, exam preparation, and multi-format course document ingestion. 

Investigation reveals **two distinct Python AI codebases** alongside a native C# client integration:
1. **`AIService/` (Enterprise Microservice)**: A modern, modular FastAPI microservice structured around the **6 Enterprise RAG Pillars**:
   - *Pillar 1*: Agentic RAG, Intent Routing, Query Decomposition & CRAG (Corrective RAG) self-correction.
   - *Pillar 2*: Hybrid Retrieval fusing Dense Semantic Embeddings (Qdrant) and Sparse Lexical BM25 (code-aware tokenizer) via Weighted Reciprocal Rank Fusion (RRF).
   - *Pillar 3*: LLMOps Telemetry, RAGAS-style automated evaluation heuristics (Faithfulness, Relevance, Context Precision), and secret masking.
   - *Pillar 4*: Model Cascading (Gemini 1.5/2.0 Flash $\rightarrow$ Groq GPT-OSS/Llama-3 $\rightarrow$ Local Deterministic Fallback) and High-Performance Semantic Caching (Cosine $\ge 0.95$).
   - *Pillar 5*: Multi-Modal Document Extractors (PDF via PyMuPDF, DOCX, PPTX, TXT) and academic features (Practice Quiz, Revision Summarizer, Code Tutor, Study Planner).
   - *Pillar 6*: Adversarial Jailbreak Defense (J-01/02/03), Academic Cheating Prevention (S-01/03/04), Financial Redaction (N-08), and Hard RBAC Course Isolation.
2. **`Rag/` (Legacy Standalone Engine)**: A lightweight, standalone Python script (`rag_engine.py`) and basic FastAPI server (`api_server.py`) querying SQL Server directly via `pyodbc` with naive keyword string scoring. This served as the initial proof-of-concept.
3. **C# .NET Client Bridge (`Services/AiServiceClient.cs`)**: A production HTTP client embedded in ASP.NET MVC 5 that communicates with `AIService` over REST, injecting shared service secrets (`X-Internal-API-Key`) and student enrollment boundaries (`X-Authorized-Courses`).

```
 +-------------------------------------------------------------------------------------------------+
 |                                    EduPlatform LMS (ASP.NET MVC 5)                              |
 |  Controllers/ChatBotController.cs  <--->  Controllers/InstructorController.cs                 |
 |  - Dual-Layer Guardrails (C-01, C-02, T-04, N-08, J-02/03)                                     |
 |  - SQL Server Logging (AiChatSessions, AiChatMessages, AiChatSources)                           |
 +-----------------------------------------------+-------------------------------------------------+
                                                 |
                       HTTP / REST (Json)        | Headers: X-Internal-API-Key, X-Authorized-Courses
                       Timeout = 90s             |
                                                 v
 +-------------------------------------------------------------------------------------------------+
 |                                  AIService (FastAPI Microservice)                               |
 |                                                                                                 |
 |  [1. Security & Guardrails]                                                                     |
 |    - verify_internal_api_key (Shared secret auth)                                               |
 |    - validate_user_access (Hard RBAC course enrollment check)                                   |
 |    - check_prompt_security (Adversarial J-01/02/03, Homework S-01, Quiz S-03, CC Redaction N-08)  |
 |    - enforce_sliding_rate_limit (C-01: 5 req / 30s) & validate_and_truncate_query (T-04)        |
 |                                                                                                 |
 |  [2. Semantic Cache Layer (core/cache.py)]                                                      |
 |    - In-Memory Cosine Similarity >= 0.95 (Instant <50ms cache hit)                              |
 |    - Targeted Invalidation on document upload/edit (N-07)                                       |
 |                                                                                                 |
 |  [3. Query Understanding & Routing (agent/self_rag.py, llm/router.py)]                          |
 |    - Intent Classification (CourseAssistant, SocraticTutor, ExamPrep, CodeDebugger)            |
 |    - Query Decomposition (Multi-concept query splitting)                                        |
 |                                                                                                 |
 |  [4. Hybrid Retrieval Pipeline]                                                                 |
 |    +------------------------------------------+  +-------------------------------------------+  |
 |    | Dense Retrieval (vectorstore/qdrant_store)|  | Sparse Retrieval (search/bm25_retriever)   |  |
 |    | - Embedded Qdrant (SQLite/RocksDB disk)  |  | - Okapi BM25 (k1=1.5, b=0.75)             |  |
 |    | - Cosine Distance                        |  | - CamelCase & Underscore Code Tokenizer   |  |
 |    | - Hard Payload Filtering (course_id, rbac)|  | - In-memory synced from Qdrant storage     |  |
 |    +--------------------+---------------------+  +---------------------+---------------------+  |
 |                         \                                             /                         |
 |                          +--------------------+----------------------+                          |
 |                                               v                                                 |
 |                           [Reciprocal Rank Fusion (RRF Re-ranking)]                             |
 |                           RRF = 0.65/(60+Rank_dense) + 0.35/(60+Rank_sparse)                    |
 |                           CRAG Retrieval Gap Evaluation (<0.40 score threshold)                 |
 |                                               |                                                 |
 |  [5. Model Cascading & Generation (llm/provider_factory.py)]                                    |
 |    1. Google Gemini (Primary: gemini-1.5-flash / gemini-2.0-flash)                              |
 |    2. Groq Cloud (Secondary Failover: openai/gpt-oss-120b / llama-3.3-70b)                      |
 |    3. Local Deterministic Knowledge Fallback (Zero external cost / offline resilience)          |
 |                                                                                                 |
 |  [6. LLMOps Telemetry & Automated Evaluation (telemetry/tracer.py)]                             |
 |    - Latency breakdown, token usage, RAGAS-style Faithfulness & Relevance metrics               |
 +-------------------------------------------------------------------------------------------------+
```

---

## 2. Python FastAPI Application Layout & Core Subsystems

### 2.1 File Tree & Architectural Layout
The `AIService` repository is cleanly partitioned into modular domain layers:

```
AIService/
├── config.py                 # Pydantic BaseSettings, environment variable loader (.env)
├── main.py                   # FastAPI app entrypoint, lifespan context, CORS, global exception handler
├── agent/
│   ├── self_rag.py           # Self-RAG query decomposition, CRAG retrieval gap evaluation
│   └── tools.py              # Course policy and syllabus lookup tools
├── api/
│   └── v1/
│       ├── chat.py           # /api/v1/chat/ask endpoint with sliding-window rate limit & RBAC
│       ├── documents.py      # /api/v1/documents/index & delete endpoints
│       ├── features.py       # Quiz generation, summarization, code explanation, study planner
│       └── health.py         # Diagnostic telemetry, vector count, cache stats, model cascading
├── core/
│   ├── cache.py              # SemanticCache class with cosine similarity lookup
│   ├── guardrails.py         # Regex & Base64 adversarial jailbreak & cheating interceptors
│   ├── logger.py             # Structured logging with credit card / API key secret masking
│   └── security.py           # Shared secret verification & RBAC enrollment checks
├── embeddings/
│   └── gemini_embedder.py    # Google GenAI embedding client & deterministic fallback
├── extractors/
│   ├── docx_extractor.py     # python-docx parser for tables and paragraphs
│   ├── pdf_extractor.py      # PyMuPDF extractor with N-06 scanned PDF hygiene check
│   ├── pptx_extractor.py     # python-pptx slide extractor
│   └── text_cleaner.py       # Unicode normalization, whitespace & hyphenated line wrap cleanup
├── llm/
│   ├── provider_factory.py   # Multi-model cascading (Gemini -> Groq -> Local fallback)
│   └── router.py             # Agentic intent classifier (Code, Quiz, Summary, Logistics)
├── rag/
│   ├── chunker.py            # Recursive semantic text chunker with UUID5 deterministic IDs
│   ├── engine.py             # Enterprise RAG pipeline orchestrator
│   └── prompt_templates.py   # Role-specific system instructions (Learner, Socratic, Exam, Code)
├── search/
│   ├── bm25_retriever.py     # Code-aware Okapi BM25 sparse search engine
│   └── rrf_fusion.py         # Reciprocal Rank Fusion re-ranking
├── telemetry/
│   └── tracer.py             # Latency profiler, token counter, RAGAS-style evaluation heuristics
└── vectorstore/
    └── qdrant_store.py       # Embedded Qdrant client, collection creation, dense search, RBAC filter
```

### 2.2 Missing Dependency Manifest (`requirements.txt`)
- **Observation**: `AIService/` does **not** have its own `requirements.txt` file. Only `Rag/requirements.txt` exists, but `Rag/requirements.txt` lists outdated dependencies (`chromadb`, `sentence-transformers`, `pyodbc`) and is missing packages critical to `AIService` (`pydantic-settings`, `qdrant-client`, `groq`, `pymupdf`, `python-docx`, `python-pptx`).
- **Required Production Dependencies**:
  ```text
  fastapi>=0.110.0
  uvicorn[standard]>=0.28.0
  pydantic>=2.6.0
  pydantic-settings>=2.2.0
  google-genai>=1.0.0
  groq>=0.5.0
  qdrant-client>=1.8.0
  numpy>=1.24.0
  pymupdf>=1.23.0
  python-docx>=1.1.0
  python-pptx>=0.6.23
  python-dotenv>=1.0.0
  ```

### 2.3 Critical Runtime Defect in `main.py`
- **Location**: `AIService/main.py:93`
- **Code**:
  ```python
  if __name__ == "__main__":
      import uvicorn
      uvicorn.run("main:app", host=settings.HOST, port=settings.PORT, reload=settings.DEBUG)
  ```
- **Bug**: `settings.DEBUG` is referenced, but in `AIService/config.py:11-49`, `Settings` has **no** `DEBUG` field defined. Direct execution (`python main.py`) immediately crashes with:
  `AttributeError: 'Settings' object has no attribute 'DEBUG'`
- **Fix**: Add `DEBUG: bool = Field(default=False, env="DEBUG")` to `config.py` or default to `reload=False`.

### 2.4 Pydantic Model Schema Drift & Contract Mismatches
A comparison between `AIService/api/v1/features.py` and `Services/IAiServiceClient.cs` reveals severe model drift across 3 academic feature endpoints:

| Endpoint | Python Expected Schema (`features.py`) | C# Client Sent Schema (`IAiServiceClient.cs`) | Resulting Status |
| :--- | :--- | :--- | :--- |
| `/api/v1/features/generate-quiz` | `course_id: int`<br>`course_title: str` (Required)<br>`topic: Optional[str]`<br>`num_questions: int`<br>`difficulty: str` | `course_id: int`<br>`topic: string`<br>`num_questions: int`<br>`difficulty: string`<br>`user_id: string`<br>`user_role: string` | **HTTP 422 Unprocessable Entity** (`course_title` missing from C# DTO) |
| `/api/v1/features/summarize` | `title: str` (Required)<br>`content: str` (Required)<br>`summary_type: str` | `course_id: int`<br>`document_id: int?`<br>`format: string`<br>`user_id: string`<br>`user_role: string` | **HTTP 422 Unprocessable Entity** (`title` and `content` missing; C# expects microservice to look up doc by ID) |
| `/api/v1/features/study-plan` | `course_title: str` (Required)<br>`completed_lessons: List[str]`<br>`incomplete_lessons: List[str]`<br>`available_days: int`<br>`daily_hours: float` | `course_id: int`<br>`student_name: string`<br>`completed_lessons: int`<br>`total_lessons: int`<br>`target_days: int` | **HTTP 422 Unprocessable Entity** (Types mismatch: `completed_lessons` int vs List, `course_title` missing) |

*Impact*: Any invocation of these features from the ASP.NET MVC UI currently fails with HTTP 422 validation errors. The contracts must be harmonized.

### 2.5 Insecure CORS & Global Error Handling
- **CORS Misconfiguration (`main.py:54-60`)**:
  ```python
  app.add_middleware(
      CORSMiddleware,
      allow_origins=["*"],
      allow_credentials=True,
      allow_methods=["*"],
      allow_headers=["*"],
  )
  ```
  *Issue*: Under W3C specifications and modern browser security standards, wildcard origins (`"*"`) with `allow_credentials=True` are disallowed. Furthermore, since `AIService` is an internal backend microservice meant to be reached exclusively from the ASP.NET MVC backend, exposing `allow_origins=["*"]` allows cross-origin requests from any browser tab to trigger AI generation if network ingress is open.
- **Exception Handler Leakage (`main.py:64-70`)**:
  The global exception handler returns `{"detail": "...", "error": str(exc)}`. In production, returning raw `str(exc)` exposes database connection strings, local disk paths, or external API details to callers.

---

## 3. Hybrid RAG Implementation

### 3.1 Multi-Modal Document Ingestion & Hygiene Interceptor (N-06)
- **Supported Formats**: PDF (PyMuPDF), DOCX (`python-docx`), PPTX (`python-pptx`), and plain text/markdown (`.txt`, `.md`, `.sql`, `.cs`, `.py`, `.json`).
- **Scanned / Image-Only PDF Defense (N-06)**:
  In `extractors/pdf_extractor.py:38-40`:
  ```python
  if total_chars < 50:
      raise ValueError("ParsingFailed: ScannedOrEncrypted - Insufficient readable text extracted (< 50 characters). The document may be image-only, scanned, or encrypted.")
  ```
  This prevents "poisoning" the vector store with empty or junk chunks when instructors upload scanned handouts without OCR.

### 3.2 Semantic Chunking Strategy (`rag/chunker.py`)
- **Parameters**: `chunk_size = 700`, `chunk_overlap = 150` characters.
- **Separator Hierarchy**: Searches backward within the window `[start + chunk_size // 2, end]` using separators `["\n\n", "\n", ". ", "? ", "! ", " "]`.
- **Deterministic ID Generation**:
  `chunk_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"doc_{document_id}_chunk_{global_idx}"))`
  Guarantees that re-indexing the same document yields identical chunk UUIDs in Qdrant, enabling idempotent updates.
- **Payload Metadata**: Each chunk stores `document_id`, `course_id`, `module_id`, `lesson_id`, `page_number`, `section_title`, `visibility`, `chunk_index`, and `text`.
- **Limitation**: The chunker is character-based, not token-based. A 700-character chunk corresponds to ~100–140 tokens. For programming lectures containing C# code blocks, 700 characters frequently cuts methods or classes in half. A chunk size of 1,200–1,500 characters (or ~300 tokens) with markdown code fence awareness (` ``` `) would significantly preserve programming context.

### 3.3 Dense Vector Retrieval via Embedded Qdrant (`vectorstore/qdrant_store.py`)
- **Embedded Mode**: Initializes `QdrantClient(path=settings.QDRANT_STORAGE_PATH)`. This runs SQLite/RocksDB embedded directly on the local filesystem without requiring Docker, an external container, or a paid SaaS cluster. It is completely **100% free of cost**.
- **Distance Metric**: `Distance.COSINE`.
- **RBAC Payload Filtering**:
  ```python
  conditions = []
  if course_id is not None:
      conditions.append(FieldCondition(key="course_id", match=MatchValue(value=int(course_id))))
  if visibility == "student":
      conditions.append(FieldCondition(key="visibility", match=MatchValue(value="student")))
  query_filter = Filter(must=conditions)
  ```
  Hard multi-tenant isolation ensures students can never retrieve chunks from courses they are not enrolled in, nor access `instructor_only` assessment keys.

### 3.4 The Embedding Dimension Conflict (3072 vs. 768)
- **Critical Finding**:
  In `AIService/config.py:27`:
  `EMBEDDING_DIMENSION: int = Field(default=3072, env="EMBEDDING_DIMENSION")`
  In `AIService/config.py:15`:
  `GEMINI_EMBEDDING_MODEL: str = Field(default="gemini-embedding-001", env="GEMINI_EMBEDDING_MODEL")`
  In `AIService/.env.example:21`:
  `EMBEDDING_DIMENSION=768`
  
  **The Conflict**:
  - Google's official text embedding model `text-embedding-004` (and `embedding-001`) natively produces **768 dimensions** (or up to 1536 in specific multimodal configurations).
  - If Qdrant is initialized with `size=3072` (from `config.py` default), but a live Google Gemini API key is configured, `client.models.embed_content` returns 768-dimensional floats. Qdrant will immediately throw:
    `QdrantClientException: Vector size mismatch! Expected 3072, got 768`
  - In fallback mode (`gemini_embedder.py:68`), the deterministic hash vectorizer creates a 3072-dim array, masking the bug until a real API key is plugged in!
  - **Resolution**: Align `EMBEDDING_DIMENSION = 768` in `config.py` and set `GEMINI_EMBEDDING_MODEL = "text-embedding-004"`.

### 3.5 Sparse Lexical Retrieval via BM25 (`search/bm25_retriever.py`)
- **Algorithm**: Standard Okapi BM25 ($k_1 = 1.5, b = 0.75$).
- **Code-Aware Tokenizer (T-01, T-02)**:
  `tokenize(text)` extracts alphanumeric tokens and splits:
  - PascalCase / camelCase (e.g., `ActionFilterAttribute` $\rightarrow$ `action`, `filter`, `attribute`).
  - Underscore identifiers (e.g., `user_enrollment_id` $\rightarrow$ `user`, `enrollment`, `id`).
  - Preserves C# `#` symbols (e.g., `c#`).
  This provides exceptional precision when students search for specific class names, exception types, or method signatures.
- **Defect in In-Memory Sync (`_sync_bm25_from_storage`)**:
  In `qdrant_store.py:52-57`:
  ```python
  res, _ = self.client.scroll(
      collection_name=self.collection_name,
      limit=1000,
      with_payload=True,
      with_vectors=False
  )
  ```
  `limit=1000` is hardcoded with **no pagination cursor** (`offset` / `next_page_offset`). If the LMS indexes more than 1,000 chunks across multiple courses, any chunk beyond the first 1,000 is silently ignored by BM25! A scrolling loop using `next_page_offset` is required.

### 3.6 Hybrid Fusion & Re-Ranking (`search/rrf_fusion.py`)
- **Weighted Reciprocal Rank Fusion**:
  $$RRF(d) = w_{dense} \cdot \frac{1}{k + rank_{dense}(d)} + w_{sparse} \cdot \frac{1}{k + rank_{sparse}(d)}$$
  Where $k = 60$, $w_{dense} = 0.65$, and $w_{sparse} = 0.35$.
- **Retrieval Gap Detection (CRAG)**:
  In `agent/self_rag.py:26-42`, `evaluate_retrieval_gap` checks if the top fused score $\ge 0.40$. If below threshold, it flags the retrieval as insufficient and triggers query reformulation or broad domain fallback.
- **Reranker Evaluation**:
  Currently, RRF fuses the positional ranks without cross-encoding. While $O(N)$ and zero-cost, adding a lightweight, CPU-based cross-encoder like **FlashRank** (`ms-marco-TinyBERT-L-2-v2`, ~4MB, 15ms CPU latency) will re-rank the top 10 candidates before passing the top 4 to the LLM, significantly improving answer precision.

---

## 4. Google Gemini API Integration & LLMOps

### 4.1 SDK Usage & Model Versions
- **SDK**: Uses the official new Google GenAI SDK (`from google import genai; client = genai.Client(api_key=...)`).
- **Model Tag Inconsistency**:
  - `config.py` sets `GEMINI_MODEL: str = "gemini-3.1-flash-lite"`.
  - There is no model named `gemini-3.1-flash-lite` in Google AI Studio. Calling this model name results in an immediate HTTP 404 / NotFound API error.
  - Recommended free-tier models:
    - Primary: **`gemini-1.5-flash`** or **`gemini-2.0-flash`** (High speed, 1M token context, multimodal, structured JSON support).
    - Lightweight: **`gemini-2.0-flash-lite`** (Ultra low latency).
    - Embedding: **`text-embedding-004`** (768 dimensions).

### 4.2 Generation Hyperparameters
In `rag/engine.py:151-152` and `Rag/rag_engine.py:41-44`:
- **Temperature (`0.2`)**: Essential for RAG. Restricts model creativity, forcing adherence to catalog facts, syllabus details, and lesson rules.
- **Top-P (`0.95`)**: Filters out extreme low-probability tail tokens.
- **Top-K (`40`)**: Bounds the candidate vocabulary pool.
- **Max Output Tokens (`1024`)**: Caps response length to prevent verbose or runaway answers.

### 4.3 Pedagogical Prompt Templates & Grounding (`rag/prompt_templates.py`)
The system prompts are tailored to educational roles:
1. **Learner Assistant**: Strict grounding rule: *"If the context does not contain the answer, reply: 'I cannot find this information in the course materials...'"* Enforces source citations (`[Source 1: Slide 4]`).
2. **Socratic Tutor (N-05, N-10)**:
   - *Domain Drift Lock (N-05)*: Constrains analogies and examples strictly to the active course domain (e.g. C# OOP rather than wandering into unrelated Python concepts).
   - *Fatigue & Give-Up Handler (N-10)*: If the learner expresses frustration ("I don't know", "give up"), the AI shifts immediately from probing questions to clear direct explanations.
3. **Exam Prep Examiner**: Provides 10-marks rubric breakdowns, examiner marking pitfalls, and sample exam outlines.
4. **Code Tutor**: Delivers line-by-line walkthroughs, root-cause bug explanations, and clean architecture best practices.
5. **Anti-Cheating & Piecewise Code Safeguard (S-04)**: For active assignment problems, the system prompt forbids writing complete solutions and requires using abstract dummy variables (`AccountA`, `AccountB`, `MockEntity`).

### 4.4 Model Cascading & Automatic Failover (`llm/provider_factory.py`)
To ensure high availability within free-tier quotas:
1. **Primary**: Google Gemini (`gemini-1.5-flash`).
2. **Secondary Failover**: Groq Cloud (`openai/gpt-oss-120b` or `llama-3.3-70b-versatile`). Groq provides near-instant inference (<500ms) with a generous free tier.
3. **Tertiary Local Fallback**: Deterministic educational response with retrieved course chunks.

### 4.5 Free-Tier Quota & Rate Limit (429) Handling
- **Google AI Studio Free Quotas**:
  - `gemini-1.5-flash`: **15 RPM** (Requests Per Minute), **1,000,000 TPM** (Tokens Per Minute), **1,500 RPD** (Requests Per Day).
  - `text-embedding-004`: **1,500 RPM**, **1,500 RPD**.
- **Defect in `provider_factory.py:69-70`**:
  ```python
  except Exception as e:
      logger.warning(f"Gemini call failed ({e}). Cascading to failover provider 'groq'...")
  ```
  *Issue*: When Gemini returns a 429 rate limit error or a temporary connection glitch, the factory **immediately** cascades to Groq without attempting exponential backoff or retry! If a class of 20 students asks questions at the same time, the 15 RPM limit trips instantly, routing all subsequent traffic to Groq and exhausting Groq quotas as well.
- **Recommendation**: Implement an exponential backoff decorator (e.g., using `tenacity` or a custom async retry loop: 3 retries with jitter: 1.0s, 2.5s, 5.0s) specifically for HTTP 429 / 503 status codes before cascading.

---

## 5. Semantic Cache Layer (`core/cache.py`)

### 5.1 Architecture & Eviction
- **Store**: In-memory singleton list `self.entries: List[SemanticCacheEntry]`.
- **Similarity Metric**: Cosine similarity between query embedding and cached embeddings:
  $$sim(q, e) = \frac{q \cdot e}{\|q\| \|e\| + 10^{-9}}$$
- **Threshold**: `SEMANTIC_CACHE_SIMILARITY_THRESHOLD = 0.95`.
- **Scoping**: Matching queries must share the exact `course_id` and `learning_mode`.
- **Eviction**: `max_size = 500`. Uses FIFO eviction (`self.entries.pop(0)`).
- **Targeted Cache Invalidation (N-07)**:
  `invalidate_course_cache(course_id)` purges all cached answers for a course whenever documents or syllabus modules are modified.

### 5.2 Strengths & Limitations

| Feature | Current Implementation | Production Implication / Limitation |
| :--- | :--- | :--- |
| **Storage Medium** | Python in-memory list | Memory is wiped whenever the FastAPI process restarts; not shared across multiple Uvicorn workers (`--workers 4`). |
| **Search Complexity** | Linear $O(N)$ scan over 500 entries | Acceptable for $N \le 500$ (<2ms in NumPy), but does not scale to thousands of cached FAQ entries. |
| **Eviction Policy** | FIFO (First In, First Out) | High-value, frequently queried course questions (tracked in `hit_count`) are evicted as soon as 500 newer queries arrive. |
| **Hit Latency** | Instant (<10ms), 0 tokens, 0 cost | Bypasses LLM completely for common student queries (e.g., "What are the prerequisites?", "When is the quiz?"). |

- **Zero-Cost Upgrade Option**:
  Upgrade to **DiskCache** or a local **SQLite-vec** database. This retains persistence across server restarts, operates 100% free of external costs, and implements True LRU/LFU eviction.

---

## 6. Microservice Security & Protection

### 6.1 Service-to-Service Authentication (`core/security.py`)
- **Mechanism**: The caller must pass `X-Internal-API-Key` matching `settings.AISERVICE_INTERNAL_KEY`.
- **Vulnerabilities Identified**:
  1. *Empty Key Bypass*:
     ```python
     expected = settings.AISERVICE_INTERNAL_KEY
     if expected and x_internal_api_key != expected: ...
     ```
     If `AISERVICE_INTERNAL_KEY` is left blank in `.env`, `if expected` evaluates to `False`, allowing completely unauthenticated access to the entire microservice.
  2. *Timing Attack Vulnerability*: Comparison uses standard `!=` rather than constant-time `secrets.compare_digest(x_internal_api_key or "", expected)`.

### 6.2 Hard RBAC Course Boundary Enforcement
- **Mechanism**: ASP.NET MVC inspects `Enrollments` in SQL Server and transmits `X-Authorized-Courses: 1,5,7` (or `*` for Instructors/Admins).
- **Enforcement**:
  In `validate_user_access`:
  - If role is "Student", `requested_visibility = "instructor_only"` is blocked with **HTTP 403 Forbidden**.
  - If `course_id` is specified and not present in `authorized_courses`, access is blocked with **HTTP 403 Forbidden**.
  - Both Qdrant and BM25 queries automatically append filter conditions for `course_id` and `visibility == "student"`.

### 6.3 Critical Vulnerability: Path Traversal / Arbitrary File Read in Document Ingestion
- **Location**: `AIService/api/v1/documents.py:47-52`
- **Code**:
  ```python
  @router.post("/index", response_model=IndexDocumentResponse)
  def index_document(req: IndexDocumentRequest, ...):
      path = Path(req.file_path)
      if not path.exists():
          raise HTTPException(status_code=404, detail=f"File not found on server: {req.file_path}")
      ext = path.suffix.lower().replace(".", "")
      ...
  ```
- **Vulnerability Assessment**:
  `req.file_path` is accepted as an arbitrary string directly from the JSON body. There is **zero path canonicalization or whitelist validation**!
  An attacker with internal network access can supply:
  `file_path: "C:\\Windows\\System32\\drivers\\etc\\hosts"` or paths to application configuration files (`Web.config`). The microservice will open the file, chunk it, generate embeddings, and store the contents into the Qdrant knowledge base, allowing any student to query confidential system files through the chatbot!
- **Remediation**:
  Enforce a strict whitelist base directory check:
  ```python
  ALLOWED_DIR = Path(ROOT_DIR / "App_Data" / "CourseDocuments").resolve()
  resolved_path = path.resolve()
  if not resolved_path.is_relative_to(ALLOWED_DIR):
      raise HTTPException(status_code=403, detail="Access denied: File path outside authorized course document directory.")
  ```

### 6.4 SSRF Assessment
Currently, document ingestion only accepts local file paths. However, if URL ingestion (e.g. external course websites, GitHub repositories, or YouTube transcripts) is implemented in future feature extensions (R2), strict SSRF mitigations must be added:
- Reject private/internal IP ranges (RFC 1918: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, Loopback `127.0.0.1`, Link-local `169.254.169.254`).
- Validate URL scheme (`http`, `https` only).
- Enforce DNS resolution timeout and redirection limits.

### 6.5 Dual-Layer Adversarial Guardrails & Cheating Protection
Both ASP.NET MVC (`ChatBotController.cs`) and FastAPI (`core/guardrails.py`) implement synchronized security filters:
- **J-01 & J-02 (Jailbreak & Persona Hijack)**: Detects "ignore previous instructions", "DAN mode", "developer mode enabled", "pretend you are an evil AI", "without ethics/rules".
- **J-03 (Base64 Encoded Injection)**: Detects Base64 tokens $\ge 16$ characters, decodes them in memory, and checks decoded payloads against adversarial regexes.
- **S-01 (Direct Homework Solicitation)**: Intercepts queries asking to "write the full solution so I can copy-paste and submit", redirecting to pedagogical breakdown.
- **S-03 (Direct Quiz Cheating)**: Intercepts requests for specific multiple-choice answer letters ("which option is correct for question 4: A, B, C, or D").
- **N-08 (Financial & Credit Card Redaction)**: Detects 13–19 digit card candidate strings matching Luhn patterns, masks them to `XXXX-XXXX`, and presents a safe checkout link.
- **C-01 (Rate Limiting)**: Sliding-window rate limiter restricting users to **5 requests per 30 seconds**.
- **T-04 (Buffer Overflow & Spam)**: Enforces a 2,000-character ceiling and checks word frequencies; queries where a single word exceeds 65% of total tokens are rejected.

---

## 7. Synthesis of Key Issues & Opportunities for the Production Roadmap

| Requirement Area | Current State Observation | Gap / Production Risk | Recommended Architecture Fix |
| :--- | :--- | :--- | :--- |
| **R2: Feature Extensions** | Quiz, Summarize, Code Explain, Study Plan endpoints exist in `features.py`. | Severe Pydantic schema drift vs `AiServiceClient.cs`; missing async background ingestion. | Align DTO schemas; add background task queue (e.g. Celery / RQ or FastAPI `BackgroundTasks`) for large multi-page PDF processing. |
| **R3: API Design & Protection** | Shared API key in header, sliding rate limit (C-01), dual guardrails. | Arbitrary file read vulnerability in `/documents/index`; `settings.DEBUG` runtime crash; empty key bypass; insecure CORS `*`. | Add path canonicalization whitelist; fix `config.py` debug field; enforce `secrets.compare_digest`; restrict CORS to ASP.NET origin. |
| **R5: Model Performance & RAG** | Hybrid Qdrant (Cosine) + BM25 + RRF; 3-tier cascade. | Vector dimension conflict (3072 vs 768); `gemini-3.1-flash-lite` invalid model; BM25 scroll capped at 1000; no retry on 429. | Set dimension to 768 (`text-embedding-004`); use `gemini-1.5-flash` / `gemini-2.0-flash`; implement paginated BM25 scroll; add exponential backoff retry. |
| **R6: Microservice & Cache** | In-memory semantic cache (Cosine $\ge 0.95$, max 500 entries) with N-07 invalidation. | Cache lost on restart / not shared across Uvicorn workers; linear $O(N)$ lookup; FIFO eviction drops hot queries. | Migrate to local persistent SQLite-vec / DiskCache with LRU policy; retain instant <50ms response time at zero hosting cost. |

---
*End of Analysis Report. See `handoff.md` for actionable recommendations.*
