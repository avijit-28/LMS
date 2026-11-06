"""
Specialized Academic AI Features:
Practice Quiz Generator, Revision Summarizer, Code Walkthrough Tutor, and Study Planner.
"""
import json
import re
from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, Field
from typing import List, Optional, Dict, Any
from embeddings.gemini_embedder import embedder
from vectorstore.qdrant_store import qdrant_store
from search.bm25_retriever import bm25_retriever
from search.rrf_fusion import reciprocal_rank_fusion
from llm.provider_factory import llm_factory
from core.security import verify_internal_api_key
from core.logger import logger

router = APIRouter(prefix="/features", tags=["Features"])

# --- Models ---
class QuizGenRequest(BaseModel):
    course_id: int
    course_title: str
    topic: Optional[str] = None
    num_questions: int = 5
    difficulty: str = "Intermediate"  # Beginner, Intermediate, Advanced
    question_type: str = "mcq"

class SummarizeRequest(BaseModel):
    title: str
    content: str
    summary_type: str = "revision_notes"  # executive, key_concepts, revision_notes

class CodeExplainRequest(BaseModel):
    code_snippet: str
    error_message: Optional[str] = None
    language: str = "csharp"
    question: Optional[str] = None

class StudyPlanRequest(BaseModel):
    course_title: str
    completed_lessons: List[str] = Field(default_factory=list)
    incomplete_lessons: List[str] = Field(default_factory=list)
    available_days: int = 7
    daily_hours: float = 1.5

@router.post("/generate-quiz")
def generate_practice_quiz(
    req: QuizGenRequest,
    api_key_valid: bool = Depends(verify_internal_api_key)
):
    """Generates structured practice questions grounded in course vectors."""
    query = req.topic or req.course_title
    q_vec = embedder.embed_text(query)
    
    dense = qdrant_store.search_dense(query_vector=q_vec, course_id=req.course_id, top_k=6)
    sparse = bm25_retriever.search(query=query, course_id=req.course_id, top_k=6)
    fused = reciprocal_rank_fusion(dense, sparse, top_k=4)
    
    context_text = "\n\n".join([f"[{c.get('section_title')}]: {c.get('text')}" for c in fused])
    if not context_text:
        context_text = f"Curriculum concepts for {req.course_title}."

    prompt = f"""Generate a {req.difficulty}-level practice quiz of {req.num_questions} questions for {req.course_title} on: {req.topic or 'core curriculum'}.
Question type: {req.question_type}.

GROUNDING CONTEXT:
{context_text[:6000]}

INSTRUCTIONS:
Return ONLY valid JSON matching this schema:
{{
  "title": "{req.course_title} Practice Quiz",
  "difficulty": "{req.difficulty}",
  "questions": [
    {{
      "id": 1,
      "question": "Question text?",
      "type": "{req.question_type}",
      "options": ["Option A", "Option B", "Option C", "Option D"],
      "correct_answer": "Option A",
      "explanation": "Why Option A is correct based on the syllabus.",
      "source_reference": "Relevant concept"
    }}
  ]
}}
Do not include markdown fences or introductory text. Return JSON only."""

    res = llm_factory.generate(
        prompt=prompt,
        system_instruction="You are a senior exam designer at EduPlatform LMS. Produce rigorous, grounded questions.",
        temperature=0.3
    )

    try:
        clean_json = re.sub(r"^```(?:json)?\s*", "", res.get("text", "").strip(), flags=re.MULTILINE)
        clean_json = re.sub(r"```$", "", clean_json.strip(), flags=re.MULTILINE).strip()
        return json.loads(clean_json)
    except Exception as e:
        logger.warning(f"Failed to parse quiz JSON: {e}")
        return {
            "title": f"{req.course_title} Review Quiz",
            "difficulty": req.difficulty,
            "questions": [{
                "id": 1,
                "question": f"Which core principle is emphasized in {req.course_title}?",
                "type": "mcq",
                "options": [f"Core foundations of {req.course_title}", "Unrelated theory", "Deprecated standard", "None of the above"],
                "correct_answer": f"Core foundations of {req.course_title}",
                "explanation": f"Based on the core syllabus of {req.course_title}.",
                "source_reference": "Curriculum"
            }]
        }

@router.post("/summarize")
def summarize_content(
    req: SummarizeRequest,
    api_key_valid: bool = Depends(verify_internal_api_key)
):
    """Generates structured revision notes or executive summaries."""
    prompt = f"""Generate a {req.summary_type.replace('_', ' ').upper()} for the following material:

TITLE: {req.title}

CONTENT:
{req.content[:7000]}

STRUCTURE:
1. **Executive Overview**: 2-3 sentence core summary.
2. **Key Concepts & Definitions**: 3-5 high-yield terms.
3. **High-Yield Revision Checkpoints**: 5-7 bullet points for rapid exam recall.
4. **Self-Check Review Questions**: 3 reflective questions for students."""

    res = llm_factory.generate(
        prompt=prompt,
        system_instruction="You are an academic mentor creating high-yield revision notes.",
        temperature=0.2
    )

    text = res.get("text", "")
    lines = text.splitlines()
    bullets = [l.strip().lstrip("*-•123456789. ") for l in lines if l.strip().startswith(("-", "*", "•"))][:7]

    return {
        "title": req.title,
        "summary_type": req.summary_type,
        "summary_text": text,
        "key_points": bullets,
        "tokens_used": res.get("total_tokens", 0),
        "provider_used": res.get("provider", "")
    }

@router.post("/code-explain")
def explain_code(
    req: CodeExplainRequest,
    api_key_valid: bool = Depends(verify_internal_api_key)
):
    """Pillar 5: Programming walkthrough and error diagnosis."""
    prompt = f"""LANGUAGE: {req.language}
USER QUESTION: {req.question or 'Review this code and diagnose any errors or inefficiencies.'}

CODE:
```{req.language}
{req.code_snippet}
```

RUNTIME ERROR (if any):
{req.error_message or 'No runtime error specified. Perform code review and walkthrough.'}

Please provide:
1. **Walkthrough**: What this code does line-by-line.
2. **Root Cause Analysis**: Why the error occurs (or edge cases).
3. **Corrected Solution**: Production-ready code with comments.
4. **Best Practices**: 2 tips for {req.language} development."""

    res = llm_factory.generate(
        prompt=prompt,
        system_instruction="You are a Lead Software Engineer and Academic CS Tutor. Provide pedagogical, step-by-step guidance.",
        temperature=0.2
    )
    return {
        "analysis": res.get("text", ""),
        "language": req.language,
        "has_error": bool(req.error_message),
        "tokens_used": res.get("total_tokens", 0),
        "provider_used": res.get("provider", "")
    }

@router.post("/study-plan")
def generate_study_plan(
    req: StudyPlanRequest,
    api_key_valid: bool = Depends(verify_internal_api_key)
):
    """Personalized study planner tailored to completed and incomplete lessons."""
    prompt = f"""You are an academic learning advisor for {req.course_title}.
Create a personalized {req.available_days}-day study plan ({req.daily_hours} hrs/day).

PROGRESS:
- Completed ({len(req.completed_lessons)}): {', '.join(req.completed_lessons) if req.completed_lessons else 'None yet'}
- Incomplete ({len(req.incomplete_lessons)}): {', '.join(req.incomplete_lessons) if req.incomplete_lessons else 'All completed, prepare for comprehensive review'}

Prioritize uncompleted lessons, revision checkpoints, and practice questions."""

    res = llm_factory.generate(
        prompt=prompt,
        system_instruction="You are an encouraging, highly organized academic mentor.",
        temperature=0.3
    )

    items = []
    step = max(1, len(req.incomplete_lessons) // max(1, req.available_days))
    for d in range(1, req.available_days + 1):
        subs = req.incomplete_lessons[(d-1)*step : d*step] if req.incomplete_lessons else ["Comprehensive Curriculum Review"]
        items.append({
            "day": d,
            "topic": f"Day {d}: {' & '.join(subs[:2]) if subs else 'Curriculum Practice'}",
            "suggested_lessons": subs,
            "estimated_minutes": int(req.daily_hours * 60),
            "focus_objective": f"Master Day {d} learning objectives."
        })

    return {
        "course_title": req.course_title,
        "total_days": req.available_days,
        "daily_schedule": items,
        "advice": res.get("text", "")
    }
