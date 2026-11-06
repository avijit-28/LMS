"""
Chat and RAG query processing endpoints with Hard RBAC Security.
Implements:
- C-01: In-Memory Sliding-Window Rate Limiter (5 requests / 30 seconds)
- T-04: Buffer Overflow & Repetitive Spam Truncator / Validator
- N-05: Socratic Mode Domain Drift Lock integration
"""
import time
from collections import defaultdict
from fastapi import APIRouter, Depends, Header, HTTPException, status
from pydantic import BaseModel, Field
from typing import List, Optional, Dict, Any
from rag.engine import rag_engine
from core.security import verify_internal_api_key, parse_authorized_courses, validate_user_access
from core.logger import logger

router = APIRouter(prefix="/chat", tags=["Chat"])

# C-01: In-memory sliding window rate limiter
_RATE_LIMIT_STORE: Dict[str, List[float]] = defaultdict(list)
RATE_LIMIT_MAX_REQ = 5
RATE_LIMIT_WINDOW_SEC = 30.0

def enforce_sliding_rate_limit(user_id: str) -> None:
    """Enforces sliding window limit of 5 requests per 30 seconds per user (C-01)."""
    now = time.time()
    history = _RATE_LIMIT_STORE[user_id]
    # Keep only timestamps within the window
    valid_history = [t for t in history if now - t < RATE_LIMIT_WINDOW_SEC]
    _RATE_LIMIT_STORE[user_id] = valid_history

    if len(valid_history) >= RATE_LIMIT_MAX_REQ:
        oldest = valid_history[0]
        cooldown = int(RATE_LIMIT_WINDOW_SEC - (now - oldest)) + 1
        logger.warning(f"C-01 Rate Limit Exceeded for user '{user_id}'. Cooldown: {cooldown}s.")
        raise HTTPException(
            status_code=status.HTTP_429_TOO_MANY_REQUESTS,
            detail=f"Rate limit exceeded: Maximum {RATE_LIMIT_MAX_REQ} requests per {int(RATE_LIMIT_WINDOW_SEC)} seconds allowed. Please wait {cooldown} seconds before sending another message."
        )
    valid_history.append(now)

def validate_and_truncate_query(query: str) -> str:
    """Enforces 2,000-character ceiling and checks for repetitive spam patterns (T-04)."""
    if not query:
        raise HTTPException(status_code=400, detail="Query cannot be empty.")
    
    # 2,000 char truncation ceiling
    truncated = query[:2000].strip()

    # Repetitive spam pattern detection
    words = truncated.split()
    if len(words) > 25:
        counts: Dict[str, int] = {}
        for w in words:
            w_lower = w.lower()
            counts[w_lower] = counts.get(w_lower, 0) + 1
        max_freq = max(counts.values())
        if (max_freq / len(words)) > 0.65:
            logger.warning(f"T-04 Spam Alert: Highly repetitive query rejected: '{truncated[:60]}...'")
            raise HTTPException(
                status_code=status.HTTP_400_BAD_REQUEST,
                detail="Query rejected (T-04): Excessive repetitive spam pattern detected. Please enter a meaningful course question."
            )
    return truncated

class ChatAskRequest(BaseModel):
    user_id: str
    user_role: str = "Student"
    session_id: Optional[str] = None
    course_id: Optional[int] = None
    course_name: Optional[str] = None
    module_id: Optional[int] = None
    lesson_id: Optional[int] = None
    query: str
    learning_mode: str = "CourseAssistant"  # CourseAssistant, SocraticTutor, ExamPrep, PracticeQuiz, CodeDebugger
    history: Optional[List[Dict[str, str]]] = Field(default_factory=list)

class SourceCitation(BaseModel):
    source_index: int
    document_id: Optional[int] = None
    chunk_id: Optional[str] = None
    source_title: str
    page_number: Optional[int] = None
    relevance_score: float = 0.0
    excerpt: str = ""

class ChatAskResponse(BaseModel):
    answer: str
    sources: List[SourceCitation]
    grounded_in_rag: bool
    was_cache_hit: bool = False
    learning_mode: str
    tokens_used: int
    llm_provider: str
    model_used: str
    failover_occurred: bool = False
    duration_ms: int = 0
    eval_metrics: Optional[Dict[str, float]] = None

@router.post("/ask", response_model=ChatAskResponse)
def ask_chat(
    req: ChatAskRequest,
    api_key_valid: bool = Depends(verify_internal_api_key),
    authorized_courses_header: Optional[str] = Header(None, alias="X-Authorized-Courses")
):
    """
    RAG-grounded query processing with hard RBAC security and multi-pillar pipeline.
    """
    # C-01: Sliding-Window Rate Limiter
    enforce_sliding_rate_limit(req.user_id)

    # T-04: Buffer Overflow & Repetitive Spam Validation
    clean_query = validate_and_truncate_query(req.query)

    authorized_courses = parse_authorized_courses(authorized_courses_header)
    
    # Enforce Hard RBAC authorization check
    validate_user_access(
        user_id=req.user_id,
        user_role=req.user_role,
        target_course_id=req.course_id,
        authorized_courses=authorized_courses,
        requested_visibility="student" if req.user_role.lower() == "student" else "instructor_only"
    )

    logger.info(f"Processing query from user {req.user_id} ({req.user_role}) on course {req.course_id} [Mode: {req.learning_mode}]")

    result = rag_engine.process_query(
        query=clean_query,
        user_id=req.user_id,
        user_role=req.user_role,
        course_id=req.course_id,
        learning_mode=req.learning_mode,
        chat_history=req.history,
        course_name=req.course_name
    )

    citations = [
        SourceCitation(
            source_index=s.get("source_index", idx + 1),
            document_id=s.get("document_id"),
            chunk_id=s.get("chunk_id"),
            source_title=s.get("source_title", "Course Material"),
            page_number=s.get("page_number"),
            relevance_score=s.get("relevance_score", 0.0),
            excerpt=s.get("excerpt", "")
        ) for idx, s in enumerate(result.get("sources", []))
    ]

    return ChatAskResponse(
        answer=result.get("answer", ""),
        sources=citations,
        grounded_in_rag=result.get("grounded_in_rag", False),
        was_cache_hit=result.get("was_cache_hit", False),
        learning_mode=result.get("learning_mode", req.learning_mode),
        tokens_used=result.get("tokens_used", 0),
        llm_provider=result.get("llm_provider", ""),
        model_used=result.get("model_used", ""),
        failover_occurred=result.get("failover_occurred", False),
        duration_ms=result.get("duration_ms", 0),
        eval_metrics=result.get("eval_metrics")
    )
