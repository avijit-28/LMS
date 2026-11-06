# EduPlatform LMS - RAG ChatBot Architecture & Integration Guide

Welcome to the **EduPlatform LMS RAG (Retrieval-Augmented Generation) ChatBot System**.

This document explains the architecture, how new courses are automatically indexed, what happens when a course is not found, which API key is needed, the purpose of each hyperparameter (temperature, top_p, top_k), and how both the native **C# .NET** and modular **Python** implementations operate.

---

## 1. What is RAG? Is this a True RAG Chatbot?

**Yes, this is an enterprise-grade RAG (Retrieval-Augmented Generation) Chatbot.**

Traditional chatbots often "hallucinate" (make up courses, incorrect fees, or false policies) because they rely solely on pre-trained memory. **RAG solves this by following a 3-step pipeline:**

1. **Retrieval (Semantic & Keyword Search)**:
   - The system inspects your SQL Server database (`LMS`) and extracts published courses, syllabi, modules, lessons, instructors, exams, and platform rules into atomic **`KnowledgeChunks`**.
   - When a user asks a question, the retriever computes similarity scores (hybrid token matching + phrase presence) to fetch the **Top 4 most relevant chunks**.
2. **Augmentation (Context Injection)**:
   - The retrieved chunks, along with the student's personal enrollment and exam records (if logged in), are injected into the LLM's system prompt as verified facts.
3. **Generation (Grounded LLM)**:
   - The LLM (**Google Gemini 1.5 Flash**) processes the user question **strictly grounded** on the injected context. It is explicitly instructed never to invent courses that do not exist.

---

## 2. Automatic Course Discovery (Future Courses)

> **Question**: *In future if any new course is added, will the chatbot automatically give full details of the course?*

**YES! 100% Automated.**

### How it Works:
1. Whenever an Instructor or Administrator:
   - **Creates a Course** (`InstructorController.CreateCourse`)
   - **Publishes or Edits a Course** (`InstructorController.EditCourse` / `AdminController.UpdateCourseStatus`)
   - **Adds Syllabus Modules or Lessons** (`InstructorController.CreateModule` / `CreateLesson`)
2. The controller triggers **`TriggerRagIndexRefresh()`** asynchronously in the background.
3. The RAG engine reads the new course title, category, description, price, module breakdown, and lesson syllabus, generates new `KnowledgeChunk` records in the database, and updates the active search index.
4. **Result**: Within seconds of publishing a course, students and visitors asking the chatbot about it will receive accurate, complete details including direct links (`/Courses/Details/{id}`).

---

## 3. What Happens When a Topic or Course is Not Found?

> **Question**: *If anything cannot be found, how does the chatbot provide a relevant answer?*

The RAG engine implements a **Dual-Layer Fallback Strategy**:

1. **Fuzzy Catalog Matching**:
   - The hybrid retrieval engine scores partial keywords against all categories (e.g. Data Science, Machine Learning, Programming, Database).
   - Even if the exact course is absent, it identifies the closest matching curriculum track.
2. **Grounded Politeness Directive**:
   - The system instruction strictly dictates:
     > *"If the user asks about a course or topic not in EduPlatform's catalog, politely state that it is not currently offered, suggest closely related topics that ARE offered, and encourage them to check back as new courses are frequently added."*
   - The chatbot will never give a blunt "I don't know" or fabricate a fake course. It gracefully guides the student to available alternatives.

---

## 4. API Keys & Models

> **Question**: *Which API key is required, or do you have the API or model key?*

### The Model:
- **`gemini-1.5-flash`** by Google DeepMind.
- It is lightning-fast, highly accurate for context retrieval, and has a 1-million-token context window.

### Where to Get the API Key:
1. Visit **[Google AI Studio](https://aistudio.google.com/app/apikey)**.
2. Sign in with your Google account.
3. Click **"Create API Key"** (It is **100% Free** with generous free-tier limits: 15 requests per minute, 1,500 requests per day).
4. Copy the API key (starts with `AIzaSy...`).

### Where to Configure the Key in the LMS:
In `Web.config`:
```xml
<appSettings>
  <!-- Replace with your Google AI Studio Gemini API Key -->
  <add key="GeminiApiKey" value="AIzaSyYourActualKeyHere" />
</appSettings>
```

### Zero-Config Built-in Fallback:
If no API key is provided, the chatbot **will NOT crash or display errors**. It automatically activates its built-in **Local Semantic Fallback Engine**, which extracts facts directly from the retrieved database chunks and formats structured markdown responses!

---

## 5. Hyperparameters Explained (Temperature, Top-P, Top-K)

> **Question**: *What about the temperature and other parameters?*

| Parameter | Recommended Value | Why This Value for RAG? |
| :--- | :--- | :--- |
| **Temperature** | **`0.2`** (Range: 0.0 – 2.0) | **Controls randomness/creativity.** For RAG, a **low temperature (0.1 - 0.3)** is essential. It forces the model to be deterministic and adhere strictly to catalog facts (prices, lesson counts, exam rules). High temperature (0.8 - 1.0) leads to hallucinations. |
| **Top-P (Nucleus Sampling)** | **`0.95`** (Range: 0.0 – 1.0) | Filters out low-probability tail tokens, keeping 95% of the most probable candidate words for natural English phrasing. |
| **Top-K** | **`40`** (Range: 1 – 100) | Caps the candidate vocabulary pool to the top 40 words at each step, preventing rare or erratic token choices. |
| **Max Output Tokens** | **`1024`** | Restricts answer length so responses remain concise, snappy, and quick to read. |
| **Chunk Size & Overlap** | **500 chars / 50 overlap** | Ensures each chunk captures a coherent syllabus unit (e.g. course summary or exam policy) without truncating key details. |

---

## 6. Structure of the `Rag/` Directory

```
D:\All\.net\MVC\LMS\Rag\
│
├── README.md              # Complete RAG architecture documentation (this file)
├── requirements.txt       # Python dependencies (google-genai, pyodbc, fastapi, uvicorn)
├── rag_engine.py          # Standalone Python RAG implementation querying SQL Server
└── api_server.py          # Optional FastAPI microservice exposing /api/chat and /api/reindex
```

### How to Run the Python RAG Engine:
```bash
# 1. Install dependencies
pip install -r requirements.txt

# 2. Run the command-line RAG test
python rag_engine.py

# 3. (Optional) Run the FastAPI microservice
python api_server.py
# Server starts at http://localhost:8000 (Interactive docs at http://localhost:8000/docs)
```

---

## 7. Dual C# .NET & Python Support

EduPlatform LMS provides the best of both worlds:
1. **Native C# Implementation (`Services/RagKnowledgeService.cs` & `GeminiChatService.cs`)**:
   - Zero external process overhead.
   - Deeply integrated into ASP.NET MVC with identity claims, student enrollment tracking, and interactive UI widgets.
2. **Python Implementation (`Rag/rag_engine.py` & `api_server.py`)**:
   - Ready for experimentation with LangChain, ChromaDB, HuggingFace embeddings, or Python AI pipelines.
