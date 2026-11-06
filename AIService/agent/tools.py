"""
Pillar 1: Deterministic Tool Calling Engine.
Provides deterministic functions for course syllabus lookup, deadline queries, and calculations.
"""
from typing import Dict, Any, Optional

def tool_get_course_guidance(course_id: Optional[int]) -> Dict[str, Any]:
    """Retrieves high-level guidelines for the active course."""
    if not course_id:
        return {"status": "no_course_selected", "info": "General Academic Learning Mode."}
    return {
        "status": "success",
        "course_id": course_id,
        "policy": "Assignments require passing score >= 60%. Quizzes allow up to 3 attempts."
    }
