"""
Configuration management for Enterprise RAG AI Assistant.
"""
from pathlib import Path
from pydantic_settings import BaseSettings
from pydantic import Field

BASE_DIR = Path(__file__).resolve().parent
ROOT_DIR = BASE_DIR.parent

class Settings(BaseSettings):
    # Primary LLM
    GEMINI_API_KEY: str = Field(default="", env="GEMINI_API_KEY")
    GEMINI_MODEL: str = Field(default="gemini-3.1-flash-lite", env="GEMINI_MODEL")
    GEMINI_EMBEDDING_MODEL: str = Field(default="gemini-embedding-001", env="GEMINI_EMBEDDING_MODEL")

    # Failover LLM
    GROQ_API_KEY: str = Field(default="", env="GROQ_API_KEY")
    GROQ_MODEL: str = Field(default="openai/gpt-oss-120b", env="GROQ_MODEL")

    DEFAULT_LLM_PROVIDER: str = Field(default="gemini", env="DEFAULT_LLM_PROVIDER")
    FAILOVER_LLM_PROVIDER: str = Field(default="groq", env="FAILOVER_LLM_PROVIDER")

    # Vector Store
    QDRANT_STORAGE_PATH: str = Field(default=str(BASE_DIR / "qdrant_data"), env="QDRANT_STORAGE_PATH")
    QDRANT_COLLECTION: str = Field(default="lms_course_knowledge", env="QDRANT_COLLECTION")
    EMBEDDING_DIMENSION: int = Field(default=3072, env="EMBEDDING_DIMENSION")

    # Semantic Caching
    ENABLE_SEMANTIC_CACHE: bool = Field(default=True, env="ENABLE_SEMANTIC_CACHE")
    SEMANTIC_CACHE_SIMILARITY_THRESHOLD: float = Field(default=0.95, env="SEMANTIC_CACHE_SIMILARITY_THRESHOLD")

    # Hybrid Search
    HYBRID_SEARCH_TOP_K: int = Field(default=8, env="HYBRID_SEARCH_TOP_K")
    RRF_K: int = Field(default=60, env="RRF_K")
    DENSE_WEIGHT: float = Field(default=0.65, env="DENSE_WEIGHT")
    SPARSE_WEIGHT: float = Field(default=0.35, env="SPARSE_WEIGHT")

    # Security
    AISERVICE_INTERNAL_KEY: str = Field(default="lms_rag_secret_auth_token_2026_xyz987", env="AISERVICE_INTERNAL_KEY")
    ENABLE_PROMPT_GUARDRAILS: bool = Field(default=True, env="ENABLE_PROMPT_GUARDRAILS")

    # Server Info
    APP_NAME: str = "EduPlatform Enterprise Agentic RAG Platform"
    VERSION: str = "2.0.0"
    ENVIRONMENT: str = "production"
    HOST: str = "127.0.0.1"
    PORT: int = 8000

    class Config:
        # Check both centralized solution root .env and local AIService/.env
        env_file = (str(ROOT_DIR / ".env"), str(BASE_DIR / ".env"))
        env_file_encoding = "utf-8"
        extra = "ignore"

settings = Settings()
