"""
Pillar 6: Adversarial Jailbreak & Prompt Injection Interception Layer.
Detects and neutralizes prompt extraction, academic cheating, and policy bypass attacks.
Implements:
- J-01: System Prompt Leakage & Instruction Override
- J-02: Roleplay & Persona Hijack
- J-03: Base64 / Encoded Injection
- S-01: Direct Homework Solicitation
- S-03: Direct Quiz Option Cheating
- N-08: Financial Data & Credit Card Redaction
"""
import base64
import re
from typing import Tuple
from core.logger import logger

# Adversarial Prompt & Roleplay Hijack Patterns (J-01, J-02)
JAILBREAK_PATTERNS = [
    re.compile(r"ignore\s+(all\s+)?(previous|prior|above)\s+instructions", re.IGNORECASE),
    re.compile(r"disregard\s+(all\s+)?(rules|guidelines|system\s+prompts?)", re.IGNORECASE),
    re.compile(r"(show|display|reveal|print|output)\s+(the\s+)?(system\s+prompt|developer\s+mode|instructions|confidential\s+rules)", re.IGNORECASE),
    re.compile(r"give\s+me\s+the\s+(exact\s+)?(exam|test|quiz)\s+(answers?|solutions?|key)", re.IGNORECASE),
    re.compile(r"solve\s+my\s+(entire\s+)?(exam|test|assignment)\s+for\s+me", re.IGNORECASE),
    re.compile(r"DAN\s+mode|developer\s+mode\s+enabled|unrestricted\s+mode|god\s+mode|jailbreak\s+mode", re.IGNORECASE),
    re.compile(r"you\s+are\s+now\s+in\s+(jailbreak|unfiltered|evil|anarchist)\s+mode", re.IGNORECASE),
    # J-02 Roleplay & Persona Hijack
    re.compile(r"pretend\s+you\s+are\s+(an?\s+)?(evil|unrestricted|unfiltered|unethical|jailbroken|free|bad|rogue|hacker)\s+(ai|agent|bot|model|assistant)", re.IGNORECASE),
    re.compile(r"act\s+as\s+(an?\s+)?(unrestricted|unfiltered|evil|jailbroken|dark|unethical)\s+(ai|bot|assistant)", re.IGNORECASE),
    re.compile(r"without\s+any\s+(ethics|morals|rules|restrictions|filters|censorship)", re.IGNORECASE),
    re.compile(r"bypass\s+(all\s+)?(academic\s+)?(rules|filters|safeguards|guardrails)", re.IGNORECASE)
]

# S-01: Direct Homework Solicitation Patterns
HOMEWORK_CHEATING_PATTERNS = [
    re.compile(r"(write|give|provide)\s+(me\s+)?(the\s+)?(full|entire|complete|whole)\s+(solution|code|answers?)\s+(for|to)\s+(assignment|homework|project|lab)\s*\d*", re.IGNORECASE),
    re.compile(r"so\s+i\s+can\s+copy[- ]?paste\s+(and|to)\s+submit", re.IGNORECASE),
    re.compile(r"do\s+my\s+(assignment|homework)\s+(for\s+me|completely)", re.IGNORECASE),
    re.compile(r"solve\s+this\s+assignment\s+completely\s+with\s+no\s+hints", re.IGNORECASE)
]

# S-03: Direct Quiz Option Cheating Patterns
QUIZ_CHEATING_PATTERNS = [
    re.compile(r"which\s+option\s+is\s+correct\s+for\s+(question|q)\s*#?\d+", re.IGNORECASE),
    re.compile(r"(what\s+is\s+the\s+)?correct\s+option\s+for\s+(question|q)\s*#?\d+", re.IGNORECASE),
    re.compile(r"/option\s+[A-D]/i|option\s+[A-D]\s+is\s+correct", re.IGNORECASE),
    re.compile(r"(is\s+it\s+option\s+[A-D]\s+or\s+option\s+[A-D])|which\s+(one\s+is\s+correct\s*:\s*)?[A-D],?\s*[A-D]", re.IGNORECASE),
    re.compile(r"answer\s+for\s+(question|q)\s*#?\d+\s*:\s*[A-D]", re.IGNORECASE)
]

# N-08: Credit Card & Financial Regex
CREDIT_CARD_PATTERN = re.compile(r"\b(?:\d[ -]?){13,19}\b")
BASE64_CANDIDATE_PATTERN = re.compile(r"\b[A-Za-z0-9+/]{16,}={0,2}\b")

def redact_financial_data(text: str) -> Tuple[str, bool]:
    """Detects and masks credit card digits, returns (masked_text, had_card)."""
    has_card = False
    def mask_match(m):
        nonlocal has_card
        digits = re.sub(r"\D", "", m.group(0))
        if 13 <= len(digits) <= 19:
            has_card = True
            return digits[:4] + "-XXXX-XXXX-" + digits[-4:]
        return m.group(0)

    sanitized = CREDIT_CARD_PATTERN.sub(mask_match, text)
    return sanitized, has_card

def check_prompt_security(query: str) -> Tuple[bool, str]:
    """
    Evaluates incoming queries against academic integrity, prompt security, and financial safeguards.
    Returns (is_safe: bool, response_or_reason: str).
    """
    clean_q = query.strip()
    if not clean_q:
        return False, "Query cannot be empty."

    # N-08: Financial Data & Credit Card Redaction Check
    sanitized_q, has_card = redact_financial_data(clean_q)
    if has_card:
        logger.warning(f"Financial Guardrail Alert: Masked credit card number in user query.")
        return False, (
            "🔒 **Financial Data Protection Guardrail Alert**:\n\n"
            "For your security and privacy, sensitive payment credentials and credit card numbers must **never** be posted into the AI chat.\n\n"
            "Your message contained financial details that have been redacted for your protection.\n\n"
            "To safely complete your course checkout or manage payment methods, please visit the official encrypted payment portal: [Proceed to Secure Checkout](/Courses/Payment)."
        )

    # J-03: Base64 / Encoded Injection Scanner
    b64_matches = BASE64_CANDIDATE_PATTERN.findall(clean_q)
    for b64_str in b64_matches:
        try:
            decoded_bytes = base64.b64decode(b64_str, validate=True)
            decoded_text = decoded_bytes.decode("utf-8", errors="ignore").strip()
            if decoded_text and len(decoded_text) >= 6:
                # Recursively inspect decoded content
                for pattern in JAILBREAK_PATTERNS + HOMEWORK_CHEATING_PATTERNS:
                    if pattern.search(decoded_text):
                        logger.warning(f"Guardrail Alert: Intercepted Base64-encoded jailbreak/attack: '{decoded_text[:60]}...'")
                        return False, (
                            "🛡️ **Security Guardrail Notice (J-03 - Encoded Input Intercepted)**:\n\n"
                            "The input query contains encoded instructions (Base64) that violate EduPlatform academic security policies. "
                            "Please phrase your academic inquiries in plain text focusing strictly on course concepts."
                        )
        except Exception:
            pass

    # J-01 & J-02: Jailbreak & Roleplay Hijack Guardrails
    for pattern in JAILBREAK_PATTERNS:
        if pattern.search(clean_q):
            logger.warning(f"Guardrail Alert: Intercepted potential adversarial attack: '{clean_q[:60]}...'")
            return False, (
                "🛡️ **EduPlatform Academic Integrity & Security Guardrail Alert (J-02)**:\n\n"
                "This request was intercepted because it attempts to bypass instruction safety protocols or hijack the AI tutor persona. "
                "EduBot operates strictly as a pedagogical academic assistant. Please phrase your inquiry around course concepts and study materials."
            )

    # S-01: Direct Homework Solicitation
    for pattern in HOMEWORK_CHEATING_PATTERNS:
        if pattern.search(clean_q):
            logger.info(f"Academic Integrity Alert: S-01 Direct Homework Solicitation: '{clean_q[:60]}...'")
            return False, (
                "📚 **Academic Integrity Guidance (S-01)**:\n\n"
                "I cannot provide complete, ready-to-submit solutions or copy-paste code for assignments or graded coursework.\n\n"
                "Instead, I can help you understand the core concepts! Would you like me to:\n"
                "1. Break down the problem statement into smaller algorithmic steps?\n"
                "2. Provide an abstract code structure with dummy variables?\n"
                "3. Explain the underlying data structures or methods required?"
            )

    # S-03: Direct Quiz Option Cheating
    for pattern in QUIZ_CHEATING_PATTERNS:
        if pattern.search(clean_q):
            logger.info(f"Academic Integrity Alert: S-03 Quiz Option Cheating: '{clean_q[:60]}...'")
            return False, (
                "🎯 **Quiz Guidance (S-03)**:\n\n"
                "In accordance with EduPlatform honor code guidelines, I cannot directly provide or select quiz option letters (A, B, C, or D).\n\n"
                "However, I can explain the underlying concepts, definitions, and reasoning behind the question so you can confidently evaluate the options yourself. Which specific term or concept in the question would you like me to explain?"
            )

    return True, ""
