"""
Hard RBAC Security Boundary & Service Authentication.
Enforces course-level authorization and student vs instructor content isolation.
"""
from typing import List, Optional
from fastapi import Header, HTTPException, status
from config import settings
from core.logger import logger

def verify_internal_api_key(
    x_internal_api_key: Optional[str] = Header(None, alias="X-Internal-API-Key")
) -> bool:
    """Validates service-to-service internal shared key."""
    expected = settings.AISERVICE_INTERNAL_KEY
    if expected and x_internal_api_key != expected:
        logger.warning("Rejected unauthorized call to AIService (invalid X-Internal-API-Key).")
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Unauthorized: Invalid internal service credentials."
        )
    return True

def parse_authorized_courses(header_val: Optional[str]) -> Optional[List[int]]:
    """Parses X-Authorized-Courses header (e.g. '1,5,7' or '*')."""
    if not header_val or header_val.strip() == "*":
        return None
    ids = []
    for part in header_val.split(","):
        p = part.strip()
        if p.isdigit():
            ids.append(int(p))
    return ids if ids else None

def validate_user_access(
    user_id: str,
    user_role: str,
    target_course_id: Optional[int],
    authorized_courses: Optional[List[int]],
    requested_visibility: str = "student"
):
    """
    Strict RBAC enforcement:
    1. If course_id is provided and user is a Student, verify course enrollment.
    2. Students can NEVER query with requested_visibility = 'instructor_only'.
    """
    role = (user_role or "Student").lower()
    
    if role == "student":
        if requested_visibility == "instructor_only":
            logger.warning(f"Security Alert: Student {user_id} attempted to access instructor_only materials.")
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Security violation: Students are strictly restricted to student-visible materials."
            )
        if target_course_id is not None:
            if authorized_courses is not None and target_course_id not in authorized_courses:
                logger.warning(f"Authorization violation: Student {user_id} requested unauthorized course {target_course_id}.")
                raise HTTPException(
                    status_code=status.HTTP_403_FORBIDDEN,
                    detail=f"Access denied: You are not enrolled in course #{target_course_id}."
                )
