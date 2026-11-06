"""
Main FastAPI Application Entrypoint for LMS RAG AI Service.
Integrates all 6 Enterprise RAG Pillars:
1. Agentic RAG, Tooling & CRAG Self-Correction
2. Hybrid Search (Dense Gemini 3072-dim + Sparse BM25 + Reciprocal Rank Fusion)
3. Production LLMOps, Secret Masking & RAGAS Heuristic Telemetry
4. Model Cascading (Gemini 3.1 Flash Lite -> Groq GPT-OSS-120B) & In-Memory Semantic Caching
5. Multi-Modal Document Extractors (PDF, DOCX, PPTX, TXT) & Multi-Course Ingestion
6. Adversarial Prompt Guardrails & Hard-Filtered RBAC Authorization
"""
import sys
import os
from contextlib import asynccontextmanager
from fastapi import FastAPI, Request
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import JSONResponse

# Ensure AIService root is in sys.path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from config import settings
from core.logger import logger
from vectorstore.qdrant_store import qdrant_store
from api.v1.health import router as health_router
from api.v1.chat import router as chat_router
from api.v1.documents import router as documents_router
from api.v1.features import router as features_router


@asynccontextmanager
async def lifespan(app: FastAPI):
    # Startup initialization
    logger.info(f"Starting {settings.APP_NAME} v{settings.VERSION} [{settings.ENVIRONMENT}]")
    try:
        total_vectors = qdrant_store.count()
        logger.info(f"Qdrant VectorStore initialized: collection='{settings.QDRANT_COLLECTION}', count={total_vectors}")
    except Exception as e:
        logger.error(f"Vector store startup error: {str(e)}")

    yield

    # Shutdown teardown
    logger.info(f"Shutting down {settings.APP_NAME}...")


app = FastAPI(
    title=settings.APP_NAME,
    version=settings.VERSION,
    description="Enterprise RAG AI Assistant Microservice for EduPlatform LMS (ASP.NET MVC 5)",
    lifespan=lifespan
)

# CORS Configuration for ASP.NET MVC 5 Client
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)


# Global Exception Handler
@app.exception_handler(Exception)
async def global_exception_handler(request: Request, exc: Exception):
    logger.error(f"Unhandled Exception on {request.method} {request.url.path}: {str(exc)}", exc_info=True)
    return JSONResponse(
        status_code=500,
        content={"detail": "An internal error occurred in the AI processing engine.", "error": str(exc)}
    )


# Include API v1 Routers
app.include_router(health_router, prefix="/api/v1")
app.include_router(chat_router, prefix="/api/v1")
app.include_router(documents_router, prefix="/api/v1")
app.include_router(features_router, prefix="/api/v1")


@app.get("/", tags=["Root"])
def root():
    return {
        "service": settings.APP_NAME,
        "version": settings.VERSION,
        "status": "online",
        "docs_url": "/docs",
        "api_v1_health": "/api/v1/health"
    }


if __name__ == "__main__":
    import uvicorn
    uvicorn.run("main:app", host=settings.HOST, port=settings.PORT, reload=settings.DEBUG)
