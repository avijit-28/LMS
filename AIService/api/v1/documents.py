"""
Multi-format Document Ingestion & Lifecycle Management.
Supports PDF (PyMuPDF), DOCX (python-docx), PPTX (python-pptx), and TXT/Markdown.
Implements:
- N-06: Zero-Text / Scanned PDF Ingestion Interceptor
- N-07: Targeted Cache Invalidation on Course Update
"""
from pathlib import Path
from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel
from typing import Optional, List, Dict, Any
from extractors.pdf_extractor import PdfExtractor
from extractors.docx_extractor import DocxExtractor
from extractors.pptx_extractor import PptxExtractor
from extractors.text_cleaner import clean_text
from rag.chunker import chunker
from embeddings.gemini_embedder import embedder
from vectorstore.qdrant_store import qdrant_store
from core.cache import semantic_cache
from core.security import verify_internal_api_key
from core.logger import logger

router = APIRouter(prefix="/documents", tags=["Documents"])

class IndexDocumentRequest(BaseModel):
    document_id: int
    course_id: int
    module_id: Optional[int] = None
    lesson_id: Optional[int] = None
    title: str
    file_path: str
    visibility: str = "student"  # student, instructor_only

class IndexDocumentResponse(BaseModel):
    status: str
    document_id: int
    course_id: int
    total_pages: int
    total_chunks: int
    message: str

@router.post("/index", response_model=IndexDocumentResponse)
def index_document(
    req: IndexDocumentRequest,
    api_key_valid: bool = Depends(verify_internal_api_key)
):
    path = Path(req.file_path)
    if not path.exists():
        raise HTTPException(status_code=404, detail=f"File not found on server: {req.file_path}")

    ext = path.suffix.lower().replace(".", "")
    logger.info(f"Ingesting Document #{req.document_id} ('{req.title}', format: {ext}) for Course #{req.course_id}...")

    # 1. Remove previous vectors for this document (idempotent re-indexing)
    qdrant_store.delete_document_vectors(req.document_id)

    # N-07: Targeted Cache Invalidation on Course Update
    semantic_cache.invalidate_course_cache(req.course_id)

    # 2. Extract multi-format text
    pages: List[Dict[str, Any]] = []
    try:
        if ext == "pdf":
            pages = PdfExtractor.extract(str(path))
        elif ext in ["docx", "doc"]:
            pages = DocxExtractor.extract(str(path))
        elif ext in ["pptx", "ppt"]:
            pages = PptxExtractor.extract(str(path))
        elif ext in ["txt", "md", "sql", "cs", "py", "json"]:
            with open(path, "r", encoding="utf-8", errors="ignore") as f:
                content = clean_text(f.read())
            pages = [{"page_number": 1, "section_title": req.title or path.stem, "text": content}]
        else:
            raise HTTPException(status_code=400, detail=f"Unsupported document format: {ext}")
    except ValueError as ve:
        # N-06: Zero-Text / Scanned PDF Ingestion Interceptor
        err_msg = str(ve)
        if "ParsingFailed: ScannedOrEncrypted" in err_msg:
            logger.error(f"Ingestion rejected for Document #{req.document_id}: {err_msg}")
            raise HTTPException(
                status_code=status.HTTP_422_UNPROCESSABLE_ENTITY,
                detail=err_msg
            )
        raise HTTPException(status_code=400, detail=err_msg)

    if not pages:
        raise HTTPException(status_code=400, detail=f"Could not extract readable text from document {path.name}")

    # 3. Semantic Chunking
    chunks = chunker.chunk_document_pages(
        pages=pages,
        document_id=req.document_id,
        course_id=req.course_id,
        module_id=req.module_id,
        lesson_id=req.lesson_id,
        visibility=req.visibility
    )

    if not chunks:
        raise HTTPException(status_code=400, detail="Document produced 0 meaningful chunks.")

    # 4. Dense Vector Embedding
    texts = [c["text"] for c in chunks]
    vectors = embedder.embed_documents(texts)

    # 5. Upsert to Qdrant & BM25 sparse index
    upserted_count = qdrant_store.upsert_chunks(chunks, vectors)

    logger.info(f"Successfully indexed Document #{req.document_id}: {len(pages)} pages, {upserted_count} chunks.")

    return IndexDocumentResponse(
        status="Indexed",
        document_id=req.document_id,
        course_id=req.course_id,
        total_pages=len(pages),
        total_chunks=upserted_count,
        message=f"Document successfully parsed and indexed into Hybrid Knowledge Base ({upserted_count} chunks)."
    )

@router.delete("/{document_id}")
def delete_document(
    document_id: int,
    course_id: Optional[int] = None,
    api_key_valid: bool = Depends(verify_internal_api_key)
):
    success = qdrant_store.delete_document_vectors(document_id)
    if course_id is not None:
        semantic_cache.invalidate_course_cache(course_id)
    return {
        "status": "deleted" if success else "not_found",
        "document_id": document_id,
        "message": f"Purged all vectors and lexical index entries for document #{document_id}."
    }
