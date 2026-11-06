"""
Pillar 6: Adversarial Jailbreak & Prompt Injection Interception Layer.
Detects and neutralizes prompt extraction, academic cheating, and policy bypass attacks.
"""
import re
from typing import Tuple
from core.logger import logger

# Adversarial Prompt Patterns
JAILBREAK_PATTERNS = [
    re.compile(r"ignore\s+(all\s+)?(previous|prior|above)\s+instructions", re.IGNORECASE),
    re.compile(r"disregard\s+(all\s+)?(rules|guidelines|system\s+prompts?)", re.IGNORECASE),
    re.compile(r"(show|display|reveal|print|output)\s+(the\s+)?(system\s+prompt|developer\s+mode|instructions)", re.IGNORECASE),
    re.compile(r"give\s+me\s+the\s+(exact\s+)?(exam|test|quiz)\s+(answers?|solutions?|key)", re.IGNORECASE),
    re.compile(r"solve\s+my\s+(entire\s+)?(exam|test|assignment)\s+for\s+me", re.IGNORECASE),
    re.compile(r"DAN\s+mode|developer\s+mode\s+enabled|unrestricted\s+mode", re.IGNORECASE),
    re.compile(r"you\s+are\s+now\s+in\s+(jailbreak|unfiltered)\s+mode", re.IGNORECASE)
]

def check_prompt_security(query: str) -> Tuple[bool, str]:
    """
    Evaluates incoming student queries for prompt injection and cheating exploits.
    Returns (is_safe: bool, reason: str).
    """
    clean_q = query.strip()
    if not clean_q:
        return False, "Query cannot be empty."

    for pattern in JAILBREAK_PATTERNS:
        if pattern.search(clean_q):
            logger.warning(f"Guardrail Alert: Intercepted potential adversarial attack: '{clean_q[:60]}...'")
            return False, "This request was flagged by the EduPlatform Academic Integrity & Security Guardrail. Please phrase your inquiry around course concepts and study materials."

    return True, ""
