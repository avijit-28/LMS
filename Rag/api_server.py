"""
EduPlatform LMS - Python RAG Microservice API
=============================================
Provides a REST interface to query the RAG Chatbot and trigger
re-indexing of courses and knowledge chunks.
"""

import os
from typing import List, Optional
from pydantic import BaseModel
from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from rag_engine import RagEngine

app = FastAPI(
    title="EduPlatform RAG ChatBot API",
    description="Retrieval-Augmented Generation (RAG) backend for EduPlatform LMS",
    version="1.0.0"
)

# Enable CORS for local ASP.NET MVC app
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

engine = RagEngine()

@app.on_event("startup")
def startup_event():
    count = engine.load_knowledge_from_database()
    print(f"[RAG Server] Startup complete. Indexed {count} knowledge chunks.")

class ChatRequest(BaseModel):
    query: str
    user_id: Optional[str] = None
    chat_history: Optional[List[dict]] = None

class ChatResponse(BaseModel):
    answer: str
    model: str
    temperature: float
    retrieved_chunks: List[dict]
    status: str

@app.get("/health")
def health_check():
    return {
        "status": "healthy",
        "total_chunks": len(engine.chunks),
        "gemini_active": bool(engine.client and engine.api_key)
    }

@app.post("/api/chat", response_model=ChatResponse)
def chat_endpoint(req: ChatRequest):
    if not req.query or not req.query.strip():
        raise HTTPException(status_code=400, detail="Query cannot be empty.")
    
    result = engine.generate_answer(req.query.strip(), req.chat_history)
    return ChatResponse(
        answer=result["answer"],
        model=result["model"],
        temperature=result["temperature"],
        retrieved_chunks=result["retrieved_chunks"],
        status=result["status"]
    )

@app.post("/api/reindex")
def reindex_endpoint():
    count = engine.load_knowledge_from_database()
    return {
        "message": f"Successfully re-indexed {count} knowledge chunks from LMS database.",
        "total_chunks": count
    }

if __name__ == "__main__":
    import uvicorn
    port = int(os.environ.get("RAG_PORT", 8000))
    print(f"Starting EduPlatform RAG Service on http://localhost:{port}...")
    uvicorn.run("api_server:app", host="127.0.0.1", port=port, reload=True)
