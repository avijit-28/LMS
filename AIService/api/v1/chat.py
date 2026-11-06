"""
Chat and RAG query processing endpoints with Hard RBAC Security.
"""
from fastapi import APIRouter, Depends, Header, HTTPException, status
from pydantic import BaseModel, Field
from typing import List, Optional, Dict, Any
from rag.engine import rag_engine
from core.security import verify_internal_api_key, parse_authorized_courses, validate_user_access
from core.logger import logger

router = APIRouter(prefix="/chat", tags=["Chat"])

class ChatAskRequest(BaseModel):
    user_id: str
    user_role: str = "Student"
    session_id: Optional[str] = None
    course_id: Optional[int] = None
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
        query=req.query,
        user_id=req.user_id,
        user_role=req.user_role,
        course_id=req.course_id,
        learning_mode=req.learning_mode,
        chat_history=req.history
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
