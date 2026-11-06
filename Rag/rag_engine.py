"""
EduPlatform LMS - Python RAG (Retrieval-Augmented Generation) Engine
====================================================================
Architecture:
  1. Ingestion: Reads published courses, modules, lessons, instructors & policies from SQL Server.
  2. Indexing: Builds chunks with metadata (CourseId, Category, Price, Level, Link).
  3. Retrieval: Hybrid semantic scoring (token overlap + keyword importance) to rank top chunks.
  4. Augmentation & Generation: Prompts Gemini 1.5 Flash with strict grounding and low temperature (0.2).
"""

import os
import re
import json
import pyodbc
from typing import List, Dict, Any, Optional

try:
    from google import genai
    from google.genai import types
    GENAI_AVAILABLE = True
except ImportError:
    GENAI_AVAILABLE = False


# ==============================================================================
# Configuration & Hyperparameters
# ==============================================================================
DEFAULT_DB_CONNECTION = (
    "DRIVER={ODBC Driver 17 for SQL Server};"
    "SERVER=. ;"
    "DATABASE=LMS;"
    "Trusted_Connection=yes;"
)

DEFAULT_MODEL = "gemini-1.5-flash"

# RAG Hyperparameters:
# - Temperature (0.2): Keeps answers strictly fact-based on retrieved chunks, preventing hallucinations.
# - Top-P (0.95): Nucleus sampling cutoff.
# - Top-K (40): Retains only the most probable token candidates.
TEMPERATURE = 0.2
TOP_P = 0.95
TOP_K = 40
MAX_OUTPUT_TOKENS = 1024


class KnowledgeChunk:
    def __init__(self, chunk_id: str, title: str, category: str, content: str, course_id: Optional[int] = None, url: Optional[str] = None):
        self.chunk_id = chunk_id
        self.title = title
        self.category = category
        self.content = content
        self.course_id = course_id
        self.url = url or (f"/Courses/Details/{course_id}" if course_id else None)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "chunk_id": self.chunk_id,
            "title": self.title,
            "category": self.category,
            "content": self.content,
            "course_id": self.course_id,
            "url": self.url
        }


class RagEngine:
    def __init__(self, api_key: Optional[str] = None, connection_string: Optional[str] = None):
        self.api_key = api_key or os.environ.get("GEMINI_API_KEY")
        self.connection_string = connection_string or os.environ.get("LMS_DB_CONNECTION", DEFAULT_DB_CONNECTION)
        self.chunks: List[KnowledgeChunk] = []
        self.client = None

        if self.api_key and GENAI_AVAILABLE:
            try:
                self.client = genai.Client(api_key=self.api_key)
            except Exception as e:
                print(f"[Warning] Failed to initialize Google GenAI Client: {e}")

    def load_knowledge_from_database(self) -> int:
        """
        Extracts all published courses, syllabus modules, lessons, instructors,
        and platform policies directly from the live SQL Server LMS database.
        """
        self.chunks = []
        try:
            conn = pyodbc.connect(self.connection_string, timeout=10)
            cursor = conn.cursor()

            # 1. Fetch from KnowledgeChunks table if already populated
            cursor.execute("SELECT ChunkId, Title, Category, Content, CourseId, DirectUrl FROM KnowledgeChunks WHERE IsActive = 1")
            rows = cursor.fetchall()
            for r in rows:
                self.chunks.append(KnowledgeChunk(
                    chunk_id=str(r[0]),
                    title=str(r[1] or ""),
                    category=str(r[2] or ""),
                    content=str(r[3] or ""),
                    course_id=r[4],
                    url=r[5]
                ))

            # If KnowledgeChunks was empty, query raw Courses directly
            if not self.chunks:
                cursor.execute("""
                    SELECT c.CourseId, c.Title, c.Description, c.Price, c.Level, c.DurationInHours,
                           cat.CategoryName, u.FullName AS InstructorName
                    FROM Courses c
                    LEFT JOIN Categories cat ON c.CategoryId = cat.CategoryId
                    LEFT JOIN AspNetUsers u ON c.InstructorId = u.Id
                    WHERE c.Status = 'Published'
                """)
                for row in cursor.fetchall():
                    cid, title, desc, price, level, dur, cat, inst = row
                    price_str = "Free" if price == 0 else f"₹{price:,.2f}"
                    content = (
                        f"Course Title: {title}\n"
                        f"Category: {cat or 'General'}\n"
                        f"Level: {level}\n"
                        f"Price: {price_str}\n"
                        f"Duration: {dur} hours\n"
                        f"Lead Instructor: {inst or 'Senior Industry Specialist'}\n"
                        f"Summary: {desc}\n"
                    )
                    self.chunks.append(KnowledgeChunk(
                        chunk_id=f"course-{cid}",
                        title=title,
                        category="Course",
                        content=content,
                        course_id=cid,
                        url=f"/Courses/Details/{cid}"
                    ))

            conn.close()
        except Exception as ex:
            print(f"[Warning] SQL Server connection failed or unavailable: {ex}")
            # Fallback to default in-memory seed chunks
            self._load_fallback_seed()

        return len(self.chunks)

    def _load_fallback_seed(self):
        """Default seed catalog if database connection is unavailable"""
        self.chunks = [
            KnowledgeChunk(
                chunk_id="course-1",
                title="C# Programming Fundamentals",
                category="Programming",
                content="Comprehensive course on C# .NET language, object-oriented principles, LINQ, and asynchronous programming. Price: ₹999.00. Beginner level, 20 hours.",
                course_id=1,
                url="/Courses/Details/1"
            ),
            KnowledgeChunk(
                chunk_id="course-2",
                title="Machine Learning Fundamentals",
                category="Machine Learning",
                content="Core supervised and unsupervised machine learning algorithms, Scikit-learn, regression, classification, and evaluation. Price: ₹1,499.00. Intermediate level, 30 hours.",
                course_id=2,
                url="/Courses/Details/2"
            ),
            KnowledgeChunk(
                chunk_id="course-3",
                title="SQL Server Masterclass",
                category="Database",
                content="Master T-SQL queries, indexing, joins, transactions, performance tuning, and schema design in Microsoft SQL Server. Price: ₹799.00. Beginner level, 15 hours.",
                course_id=3,
                url="/Courses/Details/3"
            ),
            KnowledgeChunk(
                chunk_id="policy-cert",
                title="Certificate Issuance Policy",
                category="Policy",
                content="Learners receive an official verifiable digital certificate with unique verification code and QR code immediately upon completing 100% of curriculum lessons and passing the final mandatory course exam with at least 40% marks."
            )
        ]

    def retrieve_relevant_chunks(self, query: str, top_k: int = 4) -> List[KnowledgeChunk]:
        """
        Retrieves top-K most relevant chunks using hybrid keyword + phrase relevance scoring.
        """
        if not self.chunks:
            self.load_knowledge_from_database()

        keywords = [w.lower() for w in re.findall(r"\b[a-zA-Z0-9#\+\.]{3,}\b", query)]
        if not keywords:
            return self.chunks[:top_k]

        scored: List[tuple] = []
        for ch in self.chunks:
            score = 0.0
            content_lower = ch.content.lower()
            title_lower = ch.title.lower()

            for kw in keywords:
                if kw in title_lower:
                    score += 6.0
                if kw in content_lower:
                    score += 2.0

            # Bonus for exact query phrase match
            if query.lower() in content_lower:
                score += 8.0

            if score > 0:
                scored.append((score, ch))

        scored.sort(key=lambda x: x[0], reverse=True)
        return [item[1] for item in scored[:top_k]]

    def generate_answer(self, user_query: str, chat_history: Optional[List[Dict[str, str]]] = None) -> Dict[str, Any]:
        """
        Performs full RAG cycle:
          1. Retrieve top chunks.
          2. Formulate grounded system prompt.
          3. Generate answer using Gemini 1.5 Flash (or deterministic fallback if key unset).
        """
        retrieved_chunks = self.retrieve_relevant_chunks(user_query, top_k=4)

        # Build Context String
        if retrieved_chunks:
            context_blocks = []
            for i, c in enumerate(retrieved_chunks, 1):
                block = f"[{i}] {c.title} ({c.category}):\n{c.content}"
                if c.url:
                    block += f"\nDirect Link: {c.url}"
                context_blocks.append(block)
            context_str = "\n\n".join(context_blocks)
        else:
            context_str = "No specific course chunks matched the query."

        # Strict Grounding System Prompt
        system_prompt = (
            "You are EduBot, the expert AI Academic Advisor for EduPlatform Learning Management System.\n"
            "CRITICAL INSTRUCTIONS:\n"
            "1. Base your answer EXCLUSIVELY on the verified LMS catalog and platform context provided below.\n"
            "2. If the user asks about a course or topic not in EduPlatform's catalog, politely state that it is not currently offered, suggest closely related topics from the context, and encourage them to check back.\n"
            "3. Format your answers with clear markdown, bullet points, and pricing in INR (₹).\n"
            "4. When mentioning a course that has a Direct Link, invite the learner to check the course details.\n\n"
            f"=== VERIFIED LMS KNOWLEDGE CONTEXT ===\n{context_str}\n======================================"
        )

        # Call Gemini if available
        if self.client and self.api_key:
            try:
                response = self.client.models.generate_content(
                    model=DEFAULT_MODEL,
                    contents=user_query,
                    config=types.GenerateContentConfig(
                        system_instruction=system_prompt,
                        temperature=TEMPERATURE,
                        top_p=TOP_P,
                        top_k=TOP_K,
                        max_output_tokens=MAX_OUTPUT_TOKENS
                    )
                )
                return {
                    "answer": response.text,
                    "retrieved_chunks": [c.to_dict() for c in retrieved_chunks],
                    "model": DEFAULT_MODEL,
                    "temperature": TEMPERATURE,
                    "status": "success"
                }
            except Exception as ex:
                print(f"[Gemini API Call Error]: {ex}")

        # Local Semantic Fallback Engine (when API key is not yet set)
        fallback_answer = self._generate_local_semantic_answer(user_query, retrieved_chunks)
        return {
            "answer": fallback_answer,
            "retrieved_chunks": [c.to_dict() for c in retrieved_chunks],
            "model": "local-semantic-engine",
            "temperature": TEMPERATURE,
            "status": "fallback"
        }

    def _generate_local_semantic_answer(self, query: str, chunks: List[KnowledgeChunk]) -> str:
        """Zero-config local semantic response when API key is pending"""
        if not chunks:
            return (
                "I couldn't find an exact match for that specific topic in our current course catalog. "
                "However, EduPlatform regularly adds new curriculum tracks in Software Engineering, "
                "Data Science, Machine Learning, and Database Management. "
                "Feel free to explore our **Courses** tab or let me know what skills you'd like to learn!"
            )

        top = chunks[0]
        bullet_points = []
        for c in chunks[:3]:
            link_hint = f" ([View Syllabus & Enroll]({c.url}))" if c.url else ""
            bullet_points.append(f"- **{c.title}** ({c.category}): {c.content.splitlines()[0]}{link_hint}")

        courses_summary = "\n".join(bullet_points)
        return (
            f"Based on our verified EduPlatform academic curriculum, here is the most relevant information for your query:\n\n"
            f"{courses_summary}\n\n"
            f"Would you like more details on enrollment, lesson breakdown, or prerequisites for any of these?"
        )


if __name__ == "__main__":
    print("Initializing EduPlatform RAG Engine...")
    engine = RagEngine()
    count = engine.load_knowledge_from_database()
    print(f"Loaded {count} knowledge chunks.")

    sample_query = "What machine learning or python courses are available and how much do they cost?"
    print(f"\nUser Query: {sample_query}")
    result = engine.generate_answer(sample_query)
    print(f"\nModel: {result['model']} (Temp: {result['temperature']})")
    print("\n--- Answer ---")
    print(result["answer"])
