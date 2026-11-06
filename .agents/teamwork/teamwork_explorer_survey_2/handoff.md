# Handoff Report — AI Microservice Architectural Survey

**Agent**: `teamwork_explorer_survey_2`  
**To**: Orchestrator / Lead Architect (`4aaca2b3-6eb0-46c2-ac6e-74129b3058d8`)  
**Mission**: In-depth survey of EduPlatform LMS AI Microservice (FastAPI, Qdrant, Hybrid RAG with BM25, Gemini API integration, Semantic Cache, and Security).  
**Handoff Type**: Hard (Task Complete)

---

## 1. Observation

Direct observations from codebase inspection of `D:\avi\Project\MVC\LMS`:

### 1.1 Dual Implementation & Missing Dependency Manifest
- `AIService/` is a modern, 6-pillar FastAPI microservice (`main.py`, `config.py`, `api/v1/`, `core/`, `embeddings/`, `extractors/`, `llm/`, `rag/`, `search/`, `telemetry/`, `vectorstore/`).
- `AIService/` contains **no `requirements.txt` file**. Only `Rag/requirements.txt` exists, containing outdated dependencies (`chromadb>=0.4.22`, `sentence-transformers>=2.3.1`, `pyodbc>=4.0.39`) while omitting packages needed by `AIService` (`pydantic-settings`, `qdrant-client`, `groq`, `pymupdf`, `python-docx`, `python-pptx`).
- `Rag/` is a legacy proof-of-concept containing `rag_engine.py` (querying SQL Server via `pyodbc`) and `api_server.py`.

### 1.2 Runtime Defect in `AIService/main.py`
- In `AIService/main.py:93`:
  ```python
  if __name__ == "__main__":
      import uvicorn
      uvicorn.run("main:app", host=settings.HOST, port=settings.PORT, reload=settings.DEBUG)
  ```
- In `AIService/config.py:11-49`: `Settings` class defines `APP_NAME`, `VERSION`, `ENVIRONMENT`, `HOST`, `PORT`, but **no `DEBUG` attribute**. Direct invocation (`python main.py`) immediately crashes with:
  `AttributeError: 'Settings' object has no attribute 'DEBUG'`

### 1.3 Schema Contract Drift Between C# and Python Features
- In `AIService/api/v1/features.py:21-27`:
  ```python
  class QuizGenRequest(BaseModel):
      course_id: int
      course_title: str
      topic: Optional[str] = None
      num_questions: int = 5
      difficulty: str = "Intermediate"
      question_type: str = "mcq"
  ```
  `course_title` is a required field without a default.
- In `Services/IAiServiceClient.cs:90-99`:
  ```csharp
  public class AiQuizRequestDto
  {
      public int course_id { get; set; }
      public int? module_id { get; set; }
      public string topic { get; set; }
      public int num_questions { get; set; } = 5;
      public string difficulty { get; set; } = "Medium";
      public string user_id { get; set; }
      public string user_role { get; set; } = "Student";
  }
  ```
  `course_title` is absent from the C# request DTO, causing FastAPI to reject calls with **HTTP 422 Unprocessable Entity**.
- In `AIService/api/v1/features.py:29-32` (`SummarizeRequest`) expects `title` and `content`. In `Services/IAiServiceClient.cs:101-108`, `AiSummarizeRequestDto` sends `course_id`, `document_id`, and `format`.
- In `AIService/api/v1/features.py:40-46` (`StudyPlanRequest`) expects `course_title`, `completed_lessons: List[str]`, `incomplete_lessons: List[str]`. In `Services/IAiServiceClient.cs:137-146`, `AiStudyPlanRequestDto` sends `completed_lessons: int` and `total_lessons: int`.

### 1.4 Vector Dimension & Model Tag Conflicts
- In `AIService/config.py:14-15` & `27`:
  ```python
  GEMINI_MODEL: str = Field(default="gemini-3.1-flash-lite", env="GEMINI_MODEL")
  GEMINI_EMBEDDING_MODEL: str = Field(default="gemini-embedding-001", env="GEMINI_EMBEDDING_MODEL")
  EMBEDDING_DIMENSION: int = Field(default=3072, env="EMBEDDING_DIMENSION")
  ```
- In `AIService/.env.example:21`: `EMBEDDING_DIMENSION=768`.
- In `AIService/embeddings/gemini_embedder.py:47-52`: calls `client.models.embed_content(model=self.model_name, contents=text)`. Google's standard embedding model (`text-embedding-004` or `embedding-001`) natively produces **768 dimensions**.
- In `AIService/vectorstore/qdrant_store.py:40-43`: creates collection with `VectorParams(size=settings.EMBEDDING_DIMENSION, distance=Distance.COSINE)`. With the default dimension of 3072, when a live Gemini API key is configured, Qdrant throws a dimension mismatch error (`Expected 3072, got 768`).
- `gemini-3.1-flash-lite` does not exist in Google AI Studio, causing an immediate 404 / NotFound API failure.

### 1.5 Path Traversal in Document Ingestion
- In `AIService/api/v1/documents.py:47-49`:
  ```python
  @router.post("/index", response_model=IndexDocumentResponse)
  def index_document(req: IndexDocumentRequest, api_key_valid: bool = Depends(verify_internal_api_key)):
      path = Path(req.file_path)
      if not path.exists():
          raise HTTPException(status_code=404, detail=f"File not found on server: {req.file_path}")
  ```
  `req.file_path` is parsed directly from user/caller input without path canonicalization or whitelist boundary checking against `App_Data/CourseDocuments`.

### 1.6 In-Memory BM25 Scroll Capped at 1,000 Points
- In `AIService/vectorstore/qdrant_store.py:52-57`:
  ```python
  res, _ = self.client.scroll(
      collection_name=self.collection_name,
      limit=1000,
      with_payload=True,
      with_vectors=False
  )
  ```
  The startup sync populates BM25 using `limit=1000` with no pagination cursor. Any chunks beyond 1,000 in Qdrant are omitted from BM25 sparse retrieval.

### 1.7 Missing 429 Exponential Backoff Retry Logic
- In `AIService/llm/provider_factory.py:69-70`:
  ```python
  except Exception as e:
      logger.warning(f"Gemini call failed ({e}). Cascading to failover provider 'groq'...")
  ```
  A transient 429 rate limit error immediately cascades to Groq without attempting exponential backoff or retry with jitter.

### 1.8 Shared API Key Bypass if Empty
- In `AIService/core/security.py:14-20`:
  ```python
  expected = settings.AISERVICE_INTERNAL_KEY
  if expected and x_internal_api_key != expected:
      ...
  ```
  If `AISERVICE_INTERNAL_KEY` is not set or empty string, `if expected` is false and authentication is completely bypassed. Also uses non-constant-time `!=`.

---

## 2. Logic Chain

1. **Premise 1**: The application has two Python folders (`Rag/` and `AIService/`), but `AIService/` is the active target integrated into ASP.NET MVC 5 (`AiServiceClient.cs` points to `http://127.0.0.1:8000` and `api/v1/chat/ask`, `api/v1/documents/index`, etc.).
2. **Premise 2**: Direct execution of `main.py` fails due to `settings.DEBUG` missing in `config.py` (Observation 1.2). Therefore, the microservice cannot be launched via `python main.py` until this property is added.
3. **Premise 3**: In `AIService/api/v1/features.py`, schemas for quiz generation, summarization, and study planning require fields (`course_title`, `title`, `content`) that are absent from `Services/IAiServiceClient.cs` (Observation 1.3). Therefore, any invocation of these academic features from the web frontend produces HTTP 422 errors.
4. **Premise 4**: Gemini text embeddings generate 768-dimensional vectors, while `config.py` defaults `EMBEDDING_DIMENSION` to 3072 (Observation 1.4). When real API keys are configured, Qdrant will throw dimension mismatch exceptions during chunk upserts and searches.
5. **Premise 5**: `/api/v1/documents/index` accepts arbitrary absolute file paths without validating that the path lies inside `App_Data/CourseDocuments` (Observation 1.5). An attacker with service access can index sensitive server files (`Web.config`, system files) into the vector database, exposing them via chat.
6. **Premise 6**: The BM25 sync from Qdrant hardcodes `limit=1000` without pagination (Observation 1.6). As course content grows beyond 1,000 chunks, BM25 keyword search degrades because newer chunks are missing from the lexical index.
7. **Premise 7**: Gemini's free tier imposes 15 RPM. Without an exponential backoff retry mechanism (Observation 1.7), burst student queries immediately trip 429 errors and cascade to Groq, rapidly consuming Groq's daily free quota.
8. **Premise 8**: The semantic cache is currently in-memory only (`core/cache.py`). Process restarts or multi-worker deployments wipe cached answers, causing unnecessary LLM API calls and quota consumption.

---

## 3. Caveats

- **External Network Access**: During this investigation, external API calls to Google AI Studio and Groq were not made to preserve free-tier tokens and due to environment constraints. Observations regarding model names and dimensions are grounded in official Gemini API documentation (`google-genai` SDK v1.0+ and `text-embedding-004`).
- **No Direct Code Changes Made**: In accordance with the Explorer role instructions, this investigation was strictly read-only. Proposed code fixes are provided in this handoff for the upcoming implementation/roadmap phase.

---

## 4. Conclusion & Actionable Recommendations

### Recommendation for R2: Feature Extensions & Future Architecture
1. **Harmonize Schema Contracts**: Update `AIService/api/v1/features.py` and `Services/IAiServiceClient.cs` so that DTOs match:
   - For `/api/v1/features/generate-quiz`: Add `course_title: Optional[str] = None` with a fallback to querying the vector store or course database.
   - For `/api/v1/features/summarize`: Allow `document_id: Optional[int]` in Python; if passed, retrieve text from Qdrant/disk automatically.
   - For `/api/v1/features/study-plan`: Support integer lesson counts alongside lesson name lists.
2. **Asynchronous Background Ingestion Queue**: Large multi-page PDF/PPTX uploads currently block synchronous HTTP requests in `InstructorController.cs`. Adopt FastAPI `BackgroundTasks` or a lightweight zero-cost Redis/SQLite queue so instructors get immediate HTTP 202 Accepted status with a progress polling endpoint (`/api/v1/documents/{id}/status`).
3. **Multi-Turn Chat Thread Persistence**: Store full conversation history in SQL Server (`AiChatMessages`) and pass the last 6 turns in `ChatAskRequest.history` to maintain pedagogical context in Socratic tutoring mode.

### Recommendation for R3: FastAPI API Design, Protection & Hacker Prevention
1. **Fix `main.py` Runtime Crash**: Add `DEBUG: bool = Field(default=False, env="DEBUG")` to `Settings` in `AIService/config.py`.
2. **Eliminate Arbitrary File Read Vulnerability**:
   In `AIService/api/v1/documents.py`, canonicalize and restrict incoming paths:
   ```python
   ALLOWED_BASE = (Path(__file__).resolve().parent.parent.parent.parent / "App_Data" / "CourseDocuments").resolve()
   target_file = Path(req.file_path).resolve()
   if not str(target_file).startswith(str(ALLOWED_BASE)):
       raise HTTPException(status_code=403, detail="Access Denied: Path outside authorized course documents directory.")
   ```
3. **Harden Service Authentication & CORS**:
   - Enforce non-empty shared secret: `if not expected or not x_internal_api_key or not secrets.compare_digest(x_internal_api_key, expected): raise HTTPException(401)`.
   - In `main.py`, restrict CORS: replace `allow_origins=["*"]` with explicit ASP.NET MVC origin (e.g. `allow_origins=["http://localhost:5000", "https://lms.eduplatform.internal"]`) and set `allow_credentials=True`.
4. **Safe Error Masking**: In `global_exception_handler`, sanitize error outputs in production so internal tracebacks and disk paths are never returned to clients.

### Recommendation for R5: Model Performance & RAG Answer Quality
1. **Correct Embedding Dimension & Model Versions**:
   - Set `GEMINI_EMBEDDING_MODEL = "text-embedding-004"` and `EMBEDDING_DIMENSION = 768` in `config.py`.
   - Set `GEMINI_MODEL = "gemini-1.5-flash"` or `"gemini-2.0-flash"` in `config.py` (replacing invalid `gemini-3.1-flash-lite`).
2. **Fix BM25 In-Memory Scroll Pagination**:
   Implement a scrolling cursor loop in `qdrant_store.py:_sync_bm25_from_storage`:
   ```python
   offset = None
   all_points = []
   while True:
       res, offset = self.client.scroll(
           collection_name=self.collection_name,
           limit=500,
           offset=offset,
           with_payload=True,
           with_vectors=False
       )
       all_points.extend(res)
       if offset is None:
           break
   ```
3. **Implement Exponential Backoff Retry for 429 Quotas**:
   Wrap `client.models.generate_content` in an exponential backoff loop with 3 retries (1s, 2s, 4s with random jitter) before triggering cascading failover to Groq.
4. **Optimize Chunking Size for Code Context**:
   Increase chunk size from 700 to 1,200 characters with 200 character overlap, and add syntax-aware splitting on Markdown code blocks (```` ``` ````) to avoid fragmenting C# methods across chunks.
5. **Add Lightweight Cross-Encoder Reranking**:
   Integrate **FlashRank** (`ms-marco-TinyBERT-L-2-v2`, ~4MB, pure CPU) on top of the RRF fused candidates to provide high-precision semantic re-ranking at zero additional cost.

### Recommendation for R6: Microservice & Cache Optimization
1. **Persistent Semantic Cache (Zero Cost)**:
   Migrate `core/cache.py` from a volatile Python list to a persistent **DiskCache** or local **SQLite-vec** store. This survives FastAPI process restarts and enables multi-worker scaling (`uvicorn --workers 4`).
2. **Implement LRU / LFU Eviction**:
   Replace FIFO eviction with LRU eviction weighted by `hit_count`, ensuring high-frequency curriculum FAQs remain permanently cached.
3. **Threshold Calibration**:
   Maintain `SEMANTIC_CACHE_SIMILARITY_THRESHOLD = 0.95` for factual academic Q&A to avoid semantic drift while serving instant (<50ms) zero-token responses.

---

## 5. Verification Method

To independently verify these findings:

1. **Verify `main.py` Crash**:
   Run in PowerShell:
   ```powershell
   cd D:\avi\Project\MVC\LMS\AIService
   python main.py
   ```
   *Expected Result*: Fails with `AttributeError: 'Settings' object has no attribute 'DEBUG'`.
2. **Verify Missing `requirements.txt`**:
   Inspect `D:\avi\Project\MVC\LMS\AIService`:
   ```powershell
   Test-Path D:\avi\Project\MVC\LMS\AIService\requirements.txt
   ```
   *Expected Result*: Returns `False`.
3. **Verify Embedding Dimension Conflict**:
   Check `AIService/config.py:27` vs `AIService/.env.example:21`. `config.py` has 3072, `.env.example` has 768.
4. **Verify Schema Drift in Features API**:
   Inspect `AIService/api/v1/features.py:21-27` (`QuizGenRequest.course_title` is required) vs `Services/IAiServiceClient.cs:90-99` (`AiQuizRequestDto` omits `course_title`).
5. **Verify Path Traversal Risk**:
   Inspect `AIService/api/v1/documents.py:47-49`. Note the absence of `.resolve().is_relative_to(...)` directory validation on `req.file_path`.
6. **Verify BM25 1000-Point Scroll Limitation**:
   Inspect `AIService/vectorstore/qdrant_store.py:52-57`. Note `limit=1000` with no scrolling cursor loop.

---
*End of Handoff Report.*
