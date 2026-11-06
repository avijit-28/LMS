# EduPlatform LMS — Deep Architecture Survey & Investigation Report

> **Target Codebase:** EduPlatform LMS (ASP.NET MVC 5 + Python FastAPI Microservice)  
> **Working Directory:** `D:\avi\Project\MVC\LMS`  
> **Investigation Date:** November 6, 2026  
> **Status:** Completed Read-Only Architecture Survey  

---

## 1. Executive Summary & Codebase Architecture Map

EduPlatform LMS is a hybrid enterprise Learning Management System combining a mature **ASP.NET MVC 5 monolithic web application** (.NET Framework 4.8) with an **Agentic Python FastAPI microservice** (Python 3.10+, Qdrant Vector Database, Gemini 3.1 Flash Lite, Groq fallback). The system serves three primary user personas: **Administrators**, **Instructors**, and **Students**, with a guest preview experience.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                                  USER CLIENT BROWSER                                   │
│            Bootstrap 5.2.3 | jQuery 3.7.0 | lms-theme.css | EduBot AJAX Widget         │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │ HTTP / Cookie Auth / Form POST / AJAX
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                 ASP.NET MVC 5 MONOLITH (.NET Framework 4.8 / IIS)                      │
│                                                                                        │
│  [Controllers]                                                                         │
│  • AccountController (Auth, Registration, Recovery)                                    │
│  • AdminController (User Approvals, Global Catalogs, Revenue)                          │
│  • InstructorController (Curriculum Studio, PDF Uploader, Grading)                     │
│  • CoursesController (Catalog Search, Enrollment, Checkout)                            │
│  • LearningController (Classroom Player, Progress Tracker)                             │
│  • QuizController & AssignmentController (Assessments & Submissions)                   │
│  • CertificateController (Rotativa PDF Generator, Public Verification)                │
│  • ChatBotController (3,089 LOC: Intent Classifier, Offline Fallback, AI Client)       │
│                                                                                        │
│  [Services Layer]                                                                      │
│  • CourseService, LearningService, QuizService, AssignmentService                      │
│  • DashboardService, PaymentService, StripeService, CertificateService                 │
│  • CloudinaryService (Local Disk Fallback), AiServiceClient (HTTP REST)                │
│  • EnvConfig (Centralized Configuration & .env Ingestion)                              │
│                                                                                        │
│  [Data Access Layer]                                                                   │
│  • Entity Framework 6.4.4 (ApplicationDbContext : IdentityDbContext<ApplicationUser>)  │
│  • Microsoft SQL Server (LMS Database: 24 Core Tables + 8 AI Tables)                   │
└───────────────────────────────────────────┬────────────────────────────────────────────┘
                                            │ HTTP REST (Port 8000)
                                            │ Headers: X-Internal-API-Key, X-Authorized-Courses
                                            ▼
┌────────────────────────────────────────────────────────────────────────────────────────┐
│               PYTHON FASTAPI AI MICROSERVICE (Enterprise RAG Platform)                 │
│                                                                                        │
│  [API Endpoints: /api/v1/]                                                             │
│  • /chat/ask (Sliding-window limiter, Hard RBAC, RAG Pipeline)                         │
│  • /documents/index, /{id} (PDF/DOCX/PPTX Ingestion, Chunking)                        │
│  • /features/ (generate-quiz, summarize, code-explain, study-plan)                     │
│  • /health (Readiness / Liveness Probe)                                                │
│                                                                                        │
│  [Enterprise RAG Pipeline]                                                             │
│  • Semantic Cache (In-Memory Cosine >= 0.95)                                           │
│  • Guardrails (Jailbreak, Homework Solicitation, Credit Card Redaction)                │
│  • Hybrid Search: Qdrant Dense (3072-dim) + In-Memory BM25 Sparse + RRF Fusion         │
│  • Self-RAG Context Verification & Domain Drift Locking                                │
│  • Model Cascading: Gemini 3.1 Flash Lite -> Groq GPT-OSS-120B -> Local Fallback       │
│  • Vector Store: Qdrant (Embedded On-Disk SQLite / Filesystem at qdrant_data/)         │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### Key Architectural Strengths
1. **Clear Semantic Separation**: Business logic is separated into domain services (`CourseService`, `PaymentService`, `LearningService`, etc.).
2. **Environment Configuration Layer**: `EnvConfig.cs` implements an intelligent cascading secret-loader (Process env -> `.env` file -> Web.config).
3. **Advanced AI Guardrails**: `AIService/core/guardrails.py` implements production-grade defense against prompt injection, jailbreaking, homework copying, and credit card leakage.
4. **Resilient AI Failover**: If Gemini API free-tier rate limits or fails, the microservice automatically falls back to Groq (`openai/gpt-oss-120b`), and if the entire Python microservice is down, `ChatBotController.GenerateOfflineAdvisorResponseAsync` serves rich syllabus answers directly from SQL Server.

---

## 2. ASP.NET MVC 5 Architecture & Controller Survey

### 2.1 Technology Stack & Package Versions
Inspected from `packages.config` and `Web.config`:
- **Target Framework**: .NET Framework 4.8 (`net48`)
- **MVC Framework**: Microsoft.AspNet.Mvc `5.2.9`, Microsoft.AspNet.Razor `3.2.9`, Microsoft.AspNet.WebPages `3.2.9`
- **ORM**: EntityFramework `6.4.4`
- **Identity & Security**: Microsoft.AspNet.Identity.Core `2.2.4`, Microsoft.AspNet.Identity.EntityFramework `2.2.4`, Microsoft.AspNet.Identity.Owin `2.2.4`, Microsoft.Owin `4.2.2`, Microsoft.Owin.Security.Cookies `4.2.2`
- **Optimization & Bundling**: Microsoft.AspNet.Web.Optimization `1.1.3`, WebGrease `1.6.0`, Antlr `3.5.0.2`
- **Frontend Assets**: Bootstrap `5.2.3`, jQuery `3.7.0`, jQuery.Validation `1.19.5`, Modernizr `2.8.3`
- **PDF Generation**: Rotativa `1.7.3` (`wkhtmltopdf.exe` and `wkhtmltoimage.exe` in root `Rotativa/`)
- **JSON Serialization**: Newtonsoft.Json `13.0.3`
- **Environment Management**: dotenv.net `3.1.2`

### 2.2 Controller Breakdown & Code Metrics

| Controller | Lines of Code | Primary Responsibilities | Architectural Findings |
|---|---|---|---|
| `ChatBotController.cs` | **3,089** | AI Advisor chat, intent evaluation, conversational grading, offline fallback | **Severe God Object Anti-Pattern**. Violates Single Responsibility Principle. Contains 1,400 LOC of regex intent evaluation and offline syllabus rendering. |
| `InstructorController.cs` | **1,509** | Course/Module/Lesson CRUD, PDF uploading, grading, earnings | High complexity; combines view rendering, direct EF queries, Cloudinary API calls, and raw file uploads. |
| `AdminController.cs` | **969** | Dashboard analytics, user approval workflow, course management, financial ledger | Mixes view orchestration with direct unindexed `Database.SqlQuery` calls and unpaginated table scans. |
| `AccountController.cs` | **847** | Login, Registration, Password recovery, Educator pending workflow | Contains critical password reset authorization flaw and disabled account lockout (`shouldLockout: false`). |
| `CoursesController.cs` | **404** | Catalog filtering, course details, enrollment, Stripe checkout | Processes raw credit card parameters; direct EF calls mixed with service layer calls. |
| `StudentController.cs` | **214** | Student dashboard, enrolled courses, certificates, receipts | Well-isolated; properly validates user authorization for payment receipts. |
| `LearningController.cs` | **85** | Classroom video/PDF player, lesson completion tracking | Clean controller, delegates to `LearningService`, but triggers Cartesian queries via service. |
| `CertificateController.cs` | **96** | View certificate, download PDF via Rotativa, verify credential | **Zero Authorization Check**. Vulnerable to IDOR / Broken Object Level Authorization (BOLA). |
| `QuizController.cs` | **114** | Practice & Final exam taking, submission, scoring | Enforces curriculum 100% completion rule for final exams. |
| `AssignmentController.cs` | **83** | Assignment view and submission | View details endpoint lacks enrollment verification. |
| `NotificationsController.cs` | **54** | Notification inbox and mark-as-read | Clean and scoped to current logged-in user. |
| `HomeController.cs` | **84** | Landing page, About, Contact, Instructors list | Static marketing views. |

### 2.3 Routing, Global Filters, and Application Lifecycle

1. **Routing (`App_Start/RouteConfig.cs`)**:
   - Single default route `{controller}/{action}/{id}` with default `{ controller = "Home", action = "Index", id = UrlParameter.Optional }`.
   - No custom route constraints or attribute routing enabled.
2. **Global Filters (`App_Start/FilterConfig.cs`)**:
   - Registered filters: `AntiForgeryExceptionHandlerAttribute` (order 0) and `HandleErrorAttribute`.
   - `AntiForgeryExceptionHandlerAttribute` catches `HttpAntiForgeryException` and redirects to `~/Account/Login?antiforgery=renew` after signing out the cookie.
   - **Missing Global Filters**:
     - No `RequireHttpsAttribute` (allows cleartext HTTP in production unless handled by reverse proxy).
     - No security headers filter (missing `X-Frame-Options`, `X-Content-Type-Options`, `Content-Security-Policy`, `Referrer-Policy`).
3. **Application Lifecycle & Double Seeding Bug**:
   - `Global.asax.cs` (lines 30-40) calls `LMS.Data.DbSeeder.Seed(db)`.
   - `Startup.cs` (lines 14-26) ALSO calls `LMS.Data.DbSeeder.Seed(context)`.
   - **Impact**: Every application restart executes two redundant seeding passes against SQL Server, risking lock contention and deadlocks on application pool recycles.
4. **Web.config Security & Performance Misconfigurations**:
   - Line 34: `<customErrors mode="Off" />` — **Critical Security Finding**. Displays raw ASP.NET yellow screen of death with full file paths and stack traces to public visitors.
   - Line 32: `<compilation debug="true" targetFramework="4.8" />` — Disables JIT code optimization and increases memory overhead.
   - Line 33 & 39: `maxRequestLength="1048576"` and `maxAllowedContentLength="1073741824"` allow 1 GB file uploads directly to IIS, exposing the application to DoS resource exhaustion.

---

## 3. Entity Framework 6 & Database Optimization Survey (Requirement R6)

### 3.1 DbContext & Mapping Architecture
`LMS.Data.ApplicationDbContext` inherits from `IdentityDbContext<ApplicationUser>`.
- **Database Initializer**: `Database.SetInitializer<ApplicationDbContext>(null);` (Line 18). Code First Migrations are disabled; schema is managed via external SQL scripts (`Database/LMS_Complete_Database.sql` and `Data/ai_enterprise_rag_migration.sql`).
- **Connection String Logic**: Dynamic retrieval from `EnvConfig.DatabaseConnectionString`, falling back to `"DefaultConnection"`.
- **Entity Model Count**: 22 Core LMS DbSets + 8 Enterprise RAG AI DbSets = 30 total DbSets.

### 3.2 Cartesian Explosion in Multi-Level Navigation Includes
In `Services/LearningService.cs` (lines 25-31) and `Services/CourseService.cs` (lines 97-105):
```csharp
// File: Services/LearningService.cs:25-31
var course = _db.Courses
    .Include(c => c.Modules.Select(m => m.Lessons))
    .Include(c => c.Modules.Select(m => m.Quizzes))
    .Include(c => c.Modules.Select(m => m.Assignments))
    .Include(c => c.Quizzes)
    .Include(c => c.Assignments)
    .FirstOrDefault(c => c.CourseId == courseId);
```
**Mechanism & Impact**:
In Entity Framework 6, eagerly loading multiple 1-to-many child collections via `.Include()` causes EF6 to emit a single monolithic SQL statement consisting of 5 `LEFT OUTER JOIN` clauses.
For a course with 10 modules, 30 lessons, 10 quizzes, and 10 assignments, the SQL Server engine returns a **Cartesian product** of thousands of duplicate rows over the TDS protocol. All duplicate course and module data must be transmitted over the network and deduplicated in RAM by the EF6 object materializer.
**Solution**: Split into projected DTO queries or multiple focused queries with `.AsNoTracking()`.

### 3.3 Identified N+1 Query Patterns & Missing Projections

#### Pattern 1: Missing Student Navigation Property in `Enrollment` and `Payment`
- In `Models/Enrollment.cs` (lines 14-15) and `Models/Payment.cs` (lines 14-15):
  `StudentId` is defined as a `string` foreign key without a `virtual ApplicationUser Student` navigation property.
- Consequently, in `StudentController.cs` (line 85, 131) and `PaymentService.cs` (line 84):
  `var student = _db.Users.Find(payment.StudentId);`
  When iterating through collections of payments or enrollments in Admin and Student views, an individual `SELECT * FROM AspNetUsers WHERE Id = @p0` is executed for every single record.
- **Solution**: Add `[ForeignKey("StudentId")] public virtual ApplicationUser Student { get; set; }` to both `Enrollment` and `Payment` models and use eager `.Include(p => p.Student)` or LINQ projections.

#### Pattern 2: Sequential Uncached Queries in `DashboardService.GetAdminDashboard()`
In `Services/DashboardService.cs` (lines 20-85):
When loading `/Admin`, 17 distinct database queries execute sequentially:
1. `_db.Users.Count()`
2. `_db.Roles.Where(...)` for Student
3. `_db.Roles.Where(...)` for Instructor
4. Raw SQL: `SELECT COUNT(*) FROM AspNetUserRoles WHERE RoleId = @rid` (Student)
5. Raw SQL: `SELECT COUNT(*) FROM AspNetUserRoles WHERE RoleId = @rid` (Instructor)
6. `_db.Courses.Count()`
7. `_db.Courses.Count(c => c.Status == "Published")`
8. `_db.Enrollments.Count(e => e.Status != "Cancelled")`
9. `_db.Certificates.Count()`
10. `_db.Categories.Count()`
11. `_db.Users.Count(u => !u.IsApproved ...)`
12. `_db.Users.Count(u => !u.IsApproved ... && u.RequestedRole == "Instructor")`
13. `_db.Payments.Where(...).Sum()`
14. `_db.Enrollments.Include(e => e.Course).Take(5).ToList()`
15. `_db.Users.Take(5).ToList()`
16. `_db.Payments.Include(p => p.Course).Take(5).ToList()`
17. `_db.Categories.Select(...).ToList()`
**Impact**: Generates high round-trip latency (17 distinct network round-trips to SQL Server) on every dashboard refresh.
**Solution**: Aggregate into a single stored procedure or a multi-result-set query using Dapper or EF6 raw SQL batching, combined with `MemoryCache` (e.g. 60-second sliding expiration).

#### Pattern 3: Full Table Scan & In-Memory Matching in `AdminController.Users()`
In `Controllers/AdminController.cs` (lines 39-55):
```csharp
var users = _db.Users.OrderByDescending(u => u.CreatedDate).ToList();
var userRoles = _db.Database.SqlQuery<UserRoleMapping>(
    "SELECT ur.UserId, r.Name AS RoleName FROM AspNetUserRoles ur INNER JOIN AspNetRoles r ON ur.RoleId = r.Id"
).ToList();

var list = users.Select(u => new UserManagementViewModel
{
    Id = u.Id,
    FullName = u.FullName,
    Email = u.Email,
    Role = userRoles.FirstOrDefault(ur => ur.UserId == u.Id)?.RoleName ?? "None",
    ...
}).ToList();
```
**Impact**: Fetches the entire `AspNetUsers` table and the entire `AspNetUserRoles` table into RAM with NO pagination (`.Skip().Take()`). For 10,000 users, this causes high memory spikes and GC pressure.
**Solution**: Implement database-side SQL JOIN with `.Skip((page-1)*pageSize).Take(pageSize)` projection.

#### Pattern 4: In-Memory Aggregate Computation in `CourseService.GetCourseDetail()`
In `Services/CourseService.cs` (lines 114-117, 145):
```csharp
var reviews = _db.CourseReviews.Where(r => r.CourseId == courseId).ToList();
...
decimal avgRating = reviews.Any() ? (decimal)reviews.Average(r => r.Rating) : course.Rating;
```
Loads all review rows into application memory just to compute the average rating, rather than letting SQL Server calculate `AVG(Rating)` via `.Select(r => (double?)r.Rating).Average()`.

### 3.4 Missing Database Transactions in Financial Workflows
In `Services/PaymentService.cs` (`ProcessCoursePayment`, lines 56, 68, 97):
`_db.SaveChanges()` is called **three times** across a single payment transaction:
1. After adding the `Payment` record.
2. After updating `payment.EnrollmentId`.
3. After dispatching buyer and instructor notifications.
**Vulnerability**: No `using (var tx = _db.Database.BeginTransaction())` is used. If an exception occurs between step 1 and step 2, the user is charged and a `Payment` record is committed, but the `Enrollment` record is never created!

---

## 4. Authentication, Authorization & Security Survey (Requirement R3)

### 4.1 Critical Security Vulnerabilities

#### 🚨 Vulnerability 1: Unauthenticated Account Takeover in Password Reset Flow
- **Affected File**: `Controllers/AccountController.cs`
- **Endpoints**: `CheckResetStatus` (lines 408-446) and `SetNewPassword` (lines 448-500)
- **Mechanism**:
  1. `CheckResetStatus(string email)` is `[AllowAnonymous]`, has no CSRF token check, and directly returns the plaintext reset token in the JSON response:
     ```csharp
     return Json(new { ..., token = req.ResetToken ?? "" });
     ```
  2. In `SetNewPassword(PasswordRecoveryFlowViewModel model)`:
     ```csharp
     var req = await _db.PasswordResetRequests
         .Include(r => r.User)
         .Where(r => r.Email == email)
         .OrderByDescending(r => r.RequestDate)
         .FirstOrDefaultAsync();

     if (req == null || (!req.IsApproved && req.Status != "Approved")) { ... }

     var user = req.User ?? await UserManager.FindByEmailAsync(email);
     await UserManager.RemovePasswordAsync(user.Id);
     await UserManager.AddPasswordAsync(user.Id, model.NewPassword);
     ```
  3. **The code NEVER verifies whether `model.ResetToken` matches `req.ResetToken`!**
  4. **Exploitation**: An attacker who knows any user's email can check if an administrator approved their reset request. Once approved, the attacker posts `Email` and `NewPassword` to `/Account/SetNewPassword` and takes over the account without needing any token!
- **Severity**: **Critical (CVSS 9.8)**.

#### 🚨 Vulnerability 2: Broken Object Level Authorization (BOLA / IDOR) in Certificate Viewing & PDF Download
- **Affected File**: `Controllers/CertificateController.cs` (lines 18-45)
- **Code**:
  ```csharp
  // GET: Certificate/ViewCertificate/5
  public ActionResult ViewCertificate(int id)
  {
      var model = _certificateService.GetCertificateViewModel(id);
      if (model == null) return HttpNotFound();
      return View(model);
  }

  // GET: Certificate/DownloadPdf/5
  public ActionResult DownloadPdf(int id) { ... }
  ```
- **Mechanism**: The controller has NO `[Authorize]` attribute and does NOT check whether `model.StudentId == User.Identity.GetUserId()`. Any unauthenticated user or student can iterate integer IDs (`/Certificate/DownloadPdf/1`, `2`, `3`) to view and download any student's graduation certificate.
- **Severity**: **High (CVSS 7.5)**.

#### 🚨 Vulnerability 3: Insecure Direct Handling of Credit Card Information (PCI-DSS Scope)
- **Affected File**: `Controllers/CoursesController.cs` (lines 181, 204-222)
- **Code**:
  ```csharp
  public ActionResult Payment(int courseId, string paymentMethod, string cardNumber = null, string expMonth = null, string expYear = null, string cvc = null)
  ```
- **Mechanism**: Raw credit card numbers and CVCs are accepted as plain parameters in an ASP.NET MVC controller action. Even if routed to Stripe's test API, accepting PANs (Primary Account Numbers) directly on web server endpoints brings the entire infrastructure under PCI-DSS SAQ D scope.
- **Solution**: Shift exclusively to Stripe Elements / Stripe Checkout redirect (which is already implemented in `CreateStripeCheckout` at line 251) so raw card details never touch the LMS server.

#### 🚨 Vulnerability 4: Brute-Force Password Attacks Enabled on Login
- **Affected File**: `Controllers/AccountController.cs` (line 81)
- **Code**:
  ```csharp
  var result = await SignInManager.PasswordSignInAsync(email, model.Password, model.RememberMe, shouldLockout: false);
  ```
- **Mechanism**: `shouldLockout` is hardcoded to `false`. Failed login attempts do not trigger account lockout (`LockoutEndDateUtc`), allowing automated dictionaries to perform infinite brute-force credential stuffing.
- **Solution**: Set `shouldLockout: true`, configure `MaxFailedAccessAttemptsBeforeLockout = 5` and `DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(15)` in `IdentityConfig.cs`.

#### 🚨 Vulnerability 5: Unprotected Static Upload Directory (Static File Handler Bypass)
- **Affected File**: `Services/CloudinaryService.cs` (`SaveFileLocally`, line 188)
- **Mechanism**: When Cloudinary credentials are not configured, course lesson PDFs are stored at `~/Content/uploads/{folder}/{fileName}`. Because IIS maps `Content/` as a static file directory, any visitor knowing or guessing the file URL can download paid course materials without authenticating or enrolling.
- **Solution**: Move sensitive uploads to `App_Data/uploads/` and serve files exclusively through an authorized streaming action (`FileResult`) verifying enrollment.

#### 🚨 Vulnerability 6: Cloudinary API Key Leakage
- **Affected File**: `Controllers/InstructorController.cs` (lines 485-505)
- **Code**:
  ```csharp
  return Json(new { ..., apiKey = _cloudinary.ApiKey, ... });
  ```
- **Mechanism**: `UploadPdfAjax` reflects the internal `_cloudinary.ApiKey` back to the browser in the JSON response.

#### 🚨 Vulnerability 7: CSRF Token Omission on AJAX Endpoints
- Endpoints lacking `[ValidateAntiForgeryToken]`:
  - `ChatBotController.SendMessage`
  - `InstructorController.UploadPdfAjax`
  - `AccountController.CheckResetStatus`
- **Solution**: Add header-based anti-forgery validation (`Request.Headers["__RequestVerificationToken"]`) for all JSON AJAX POST requests.

---

## 5. API Interactions with Python FastAPI Microservice

### 5.1 Communication Architecture
The ASP.NET MVC 5 application communicates with the Python FastAPI microservice via `LMS.Services.AiServiceClient` using a singleton `HttpClient`.

| Microservice Endpoint | HTTP Method | MVC Calling Method | Purpose |
|---|---|---|---|
| `/api/v1/health` | GET | `AiServiceClient.GetHealthAsync` | Readiness & Liveness check |
| `/api/v1/chat/ask` | POST | `AiServiceClient.AskQuestionAsync` | RAG query processing |
| `/api/v1/documents/index` | POST | `AiServiceClient.IndexDocumentAsync` | Multi-format document ingestion |
| `/api/v1/documents/{id}` | DELETE | `AiServiceClient.DeleteDocumentIndexAsync` | Vector & cache purge |
| `/api/v1/features/generate-quiz` | POST | `AiServiceClient.GenerateQuizAsync` | Synthetic practice exam creation |
| `/api/v1/features/summarize` | POST | `AiServiceClient.SummarizeAsync` | Content summarization |
| `/api/v1/features/code-explain` | POST | `AiServiceClient.ExplainCodeAsync` | Socratic code walkthrough |
| `/api/v1/features/study-plan` | POST | `AiServiceClient.GenerateStudyPlanAsync` | Personalized curriculum planner |

### 5.2 Critical Coupling & Microservice Architecture Flaws

#### Flaw 1: Windows Filesystem Coupling in Document Ingestion
In `Services/AiServiceClient.cs` (lines 80-97) and `AIService/api/v1/documents.py` (lines 47-49):
```csharp
// C# passes local Windows disk path:
file_path = "D:\\avi\\Project\\MVC\\LMS\\Content\\uploads\\courses\\1\\lessons\\document.pdf"
```
```python
# Python inspects local filesystem directly:
path = Path(req.file_path)
if not path.exists():
    raise HTTPException(status_code=404, detail=f"File not found on server: {req.file_path}")
```
**Impact**:
This design works ONLY when both ASP.NET MVC and FastAPI run on the same Windows host. If the FastAPI microservice is containerized (Docker Linux) or deployed to a free cloud container platform (e.g. Render, Railway, Oracle Cloud Free Tier), **the Python service will fail immediately** because `D:\...` does not exist on Linux!
**Solution**: Refactor `IndexDocument` to accept `multipart/form-data` stream or download from a secure pre-signed Cloudinary / S3 URL.

#### Flaw 2: Qdrant Embedded Mode Concurrency Lock
In `AIService/vectorstore/qdrant_store.py` (lines 27-28):
```python
self.client = QdrantClient(path=str(storage_path))
```
Qdrant is initialized in **embedded on-disk mode**, saving SQLite index files at `AIService/qdrant_data`.
- **Limitation**: Embedded Qdrant locks its storage folder using OS file locks. Only **one single Python process/worker** can access the storage.
- If Uvicorn runs with multiple workers (`uvicorn main:app --workers 4`), the secondary workers crash on startup with file lock errors!
- **Solution**: Run Qdrant as a standalone container (`docker run -p 6333:6333 qdrant/qdrant`) or use Qdrant Cloud Free Tier (1GB free cluster), configured via `QdrantClient(url=settings.QDRANT_URL, api_key=settings.QDRANT_API_KEY)`.

#### Flaw 3: In-Memory Semantic Caching Divergence
- In `AIService/core/cache.py`:
  Semantic cache entries are stored in an unpersisted Python list in RAM (`self.entries = []`, max 500 items). It is completely lost on microservice restart.
- In SQL Server (`LMS.Data.ApplicationDbContext` line 62):
  There is a dedicated EF6 table `AI_SemanticCache`, but the Python microservice does not read or write to it.
- **Solution**: Persist semantic cache either into the existing SQL Server `AI_SemanticCache` table or an embedded/free Redis/Valkey instance.

#### Flaw 4: Permissive CORS Configuration
In `AIService/main.py` (lines 54-60):
```python
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)
```
Setting `allow_origins=["*"]` with `allow_credentials=True` is an invalid CORS pattern rejected by modern browsers and permits cross-origin requests from any domain if credentials are omitted.

---

## 6. Frontend UI/UX, Design System & Accessibility Survey (Requirement R7)

### 6.1 Design System & Component Library
- **CSS Architecture**: Bootstrap 5.2.3 (`Content/bootstrap.min.css`) combined with `Content/lms-theme.css` (112 KB).
- **Color Palette**:
  - Background: Deep Petrol Dark `#041822`, Surface Card `#062330`
  - Accents: Electric Cyan `#27B8D6`, Vibrant Orange `#FF7A18`, Emerald `#10B981`
  - Typography: Google Fonts (Playfair Display for headings, Plus Jakarta Sans for UI, Inter for body text)
- **JavaScript Engine**: `Scripts/lms-app.js` (22 KB):
  - Canvas confetti engine for quiz completions and certificates.
  - Toast notification system (`LMS.toast()`).
  - Client-side catalog filter and course search.
  - Floating AI widget launcher (`_ChatBotWidget.cshtml`).

### 6.2 Accessibility (WCAG 2.1 AA) Gaps & Findings

| Area | Observed Code Pattern | Accessibility Violation | Remediation |
|---|---|---|---|
| **Color Contrast** | Subtitles and meta text use `#64748B` or `#94A3B8` on `#041822` background. | Contrast ratio is ~3.2:1 (Fails WCAG AA minimum of 4.5:1 for normal text). | Adjust subdued text to `#CBD5E1` (contrast ratio 8.2:1). |
| **Interactive Divs** | Floating launcher: `<div id="edubot-launcher" onclick="toggleEduBot()">` | Missing `role="button"`, `tabindex="0"`, and keyboard `keydown` handler. Keyboard-only users cannot open the AI advisor. | Change to semantic `<button type="button">` or add `role="button"` and `tabindex="0"`. |
| **Form Labels** | Search inputs: `<input class="form-control" placeholder="Search courses...">` | Missing `<label>` and missing `aria-label="Search courses"`. Screen readers fail to announce input purpose. | Add `aria-label="Search courses"` to all search and filter inputs. |
| **Dynamic Regions** | AI chat messages append dynamically into `#edubot-messages`. | Screen readers do not announce incoming AI responses. | Add `aria-live="polite"` and `aria-atomic="false"` to `#edubot-messages`. |
| **PDF Viewer Modals** | Custom modal popups in classroom. | Missing `aria-modal="true"`, focus trap, and Esc key dismissal. | Utilize standard Bootstrap 5 `data-bs-toggle="modal"` with `role="dialog"`. |

---

## 7. Investigation Synthesis & Recommendations Mapping

Below is the consolidated matrix mapping identified issues to concrete, actionable recommendations across Requirements R2, R3, R6, and R7:

| Req | Domain | Issue Identified in Codebase | Recommended Solution |
|---|---|---|---|
| **R2** | Architecture | `ChatBotController.cs` is 3,089 lines; blends intent routing, offline syllabus generation, and grading. | Refactor into `IAdvisorIntentService`, `IOfflineCurriculumService`, and `ISocraticGradingService`. |
| **R2** | Ingestion | Local Windows file path passed to microservice (`req.file_path`). | Switch to multipart/form-data upload or pre-signed URL download for cross-platform/Linux container readiness. |
| **R3** | Security | Unauthenticated account takeover via `CheckResetStatus` and `SetNewPassword`. | Require cryptographic token matching (`model.ResetToken == req.ResetToken`), add anti-forgery token, and hash tokens in database. |
| **R3** | Security | BOLA / IDOR in `CertificateController` (ViewCertificate, DownloadPdf). | Add `[Authorize]` attribute and verify `certificate.StudentId == currentUserId || User.IsInRole("Admin")`. |
| **R3** | Security | Brute-force attacks enabled (`shouldLockout: false` on Login). | Enable account lockout (`shouldLockout: true`) with 5 failed attempts lockout policy. |
| **R3** | Security | Unrestricted static file downloads of paid PDFs (`/Content/uploads/`). | Relocate storage to `App_Data/uploads/` and serve via authorized controller action. |
| **R3** | Security | Rotativa uses obsolete Windows-only `wkhtmltopdf.exe` (SSRF risk). | Replace with Puppeteer-Sharp, QuestPDF (free open source C# PDF library), or standalone headless Chromium container. |
| **R6** | EF6 Optimization | Cartesian explosion in `LearningService.GetLearningState` (5 nested `.Include`s). | Decompose into separate focused queries or direct DTO projections with `.AsNoTracking()`. |
| **R6** | EF6 Optimization | N+1 queries due to missing `ApplicationUser Student` in `Enrollment` and `Payment`. | Add navigation property `[ForeignKey("StudentId")] public virtual ApplicationUser Student` and eager-load. |
| **R6** | EF6 Optimization | 17 sequential un-cached queries in `DashboardService.GetAdminDashboard()`. | Implement multi-result-set query or batching and cache with `MemoryCache` (60s sliding window). |
| **R6** | EF6 Optimization | Full table scan of users and roles in `AdminController.Users()`. | Implement SQL-level JOIN and database pagination (`.Skip().Take()`). |
| **R6** | EF6 Optimization | Multi-save missing transaction in `PaymentService.ProcessCoursePayment`. | Wrap operations in `using (var tx = _db.Database.BeginTransaction())`. |
| **R6** | Performance | Qdrant embedded on-disk mode file lock limits Python to 1 worker. | Decouple Qdrant into standalone Docker container or Qdrant Cloud Free Tier (1GB). |
| **R7** | UI/UX & A11y | Low text contrast on dark theme (`#64748B` on `#041822`). | Upgrade muted text to `#CBD5E1` to achieve WCAG 2.1 AA 4.5:1 contrast compliance. |
| **R7** | UI/UX & A11y | Floating AI widget uses non-semantic `div` with `onclick`. | Replace with `<button type="button">` or add `role="button"`, `tabindex="0"`, and keyboard handlers. |
| **R7** | UI/UX & A11y | Dynamic AI chat stream lacks screen reader announcements. | Add `aria-live="polite"` region to `#edubot-messages`. |
