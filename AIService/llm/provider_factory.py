"""
Pillar 4: Model Cascading & Automatic Failover Engine.
Cascades between Google Gemini (primary) and Groq (secondary) seamlessly.
"""
from typing import Dict, Any, Optional
from google import genai
from groq import Groq
from config import settings
from core.logger import logger

class LLMProviderFactory:
    _instance = None

    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(LLMProviderFactory, cls).__new__(cls)
            cls._instance._init_clients()
        return cls._instance

    def _init_clients(self):
        # 1. Initialize Gemini
        self.gemini_client = None
        if settings.GEMINI_API_KEY:
            try:
                self.gemini_client = genai.Client(api_key=settings.GEMINI_API_KEY)
                logger.info(f"Initialized Gemini client with model '{settings.GEMINI_MODEL}'.")
            except Exception as e:
                logger.error(f"Failed to initialize Gemini: {e}")

        # 2. Initialize Groq
        self.groq_client = None
        if settings.GROQ_API_KEY:
            try:
                self.groq_client = Groq(api_key=settings.GROQ_API_KEY)
                logger.info(f"Initialized Groq client with model '{settings.GROQ_MODEL}'.")
            except Exception as e:
                logger.error(f"Failed to initialize Groq: {e}")

    def generate(
        self,
        prompt: str,
        system_instruction: str,
        temperature: float = 0.2,
        max_tokens: int = 1024
    ) -> Dict[str, Any]:
        """
        Executes generation with automatic cascading failover:
        Gemini -> Groq -> Local Deterministic Fallback.
        """
        # --- Attempt 1: Gemini (Primary) ---
        if self.gemini_client:
            try:
                full_contents = f"{system_instruction}\n\nUser Question:\n{prompt}"
                resp = self.gemini_client.models.generate_content(
                    model=settings.GEMINI_MODEL,
                    contents=full_contents
                )
                text = resp.text.strip() if resp.text else ""
                logger.info(f"Gemini generation successful ({len(text)} chars).")
                return {
                    "text": text,
                    "model": settings.GEMINI_MODEL,
                    "provider": "gemini",
                    "failover_occurred": False,
                    "prompt_tokens": len(full_contents.split()),
                    "completion_tokens": len(text.split()),
                    "total_tokens": len(full_contents.split()) + len(text.split())
                }
            except Exception as e:
                logger.warning(f"Gemini call failed ({e}). Cascading to failover provider 'groq'...")

        # --- Attempt 2: Groq (Secondary Failover) ---
        if self.groq_client:
            try:
                messages = [
                    {"role": "system", "content": system_instruction},
                    {"role": "user", "content": prompt}
                ]
                resp = self.groq_client.chat.completions.create(
                    model=settings.GROQ_MODEL,
                    messages=messages,
                    temperature=temperature,
                    max_tokens=max_tokens
                )
                text = resp.choices[0].message.content or ""
                logger.info(f"Groq failover generation successful ({len(text)} chars).")
                return {
                    "text": text,
                    "model": settings.GROQ_MODEL,
                    "provider": "groq",
                    "failover_occurred": True,
                    "prompt_tokens": resp.usage.prompt_tokens if resp.usage else 0,
                    "completion_tokens": resp.usage.completion_tokens if resp.usage else 0,
                    "total_tokens": resp.usage.total_tokens if resp.usage else 0
                }
            except Exception as e:
                logger.error(f"Groq failover also failed: {e}")

        # --- Attempt 3: Local Fallback ---
        logger.warning("All frontier & SLM providers unavailable. Providing local educational fallback.")
        fallback_text = (
            "**[EduPlatform Academic Assistant Notice]**\n\n"
            "Our external AI language service is currently experiencing high load or rate limits. "
            "Please review the retrieved course syllabus sections and lecture slides attached below."
        )
        return {
            "text": fallback_text,
            "model": "local-fallback",
            "provider": "local",
            "failover_occurred": True,
            "prompt_tokens": 0,
            "completion_tokens": 30,
            "total_tokens": 30
        }

llm_factory = LLMProviderFactory()
