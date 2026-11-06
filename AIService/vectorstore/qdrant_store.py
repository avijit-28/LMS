"""
Qdrant Vector Database Integration with Hard RBAC Filtering.
Embedded on-disk storage with Cosine similarity and visibility partition.
"""
from pathlib import Path
from typing import List, Dict, Any, Optional
from qdrant_client import QdrantClient
from qdrant_client.models import PointStruct, VectorParams, Distance, Filter, FieldCondition, MatchValue
from config import settings
from core.logger import logger
from search.bm25_retriever import bm25_retriever

class QdrantStore:
    _instance = None

    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(QdrantStore, cls).__new__(cls)
            cls._instance._init_client()
        return cls._instance

    def _init_client(self):
        self.collection_name = settings.QDRANT_COLLECTION
        storage_path = Path(settings.QDRANT_STORAGE_PATH)
        storage_path.mkdir(parents=True, exist_ok=True)
        
        logger.info(f"Initializing embedded Qdrant vectorstore at '{storage_path}'...")
        self.client = QdrantClient(path=str(storage_path))
        self._ensure_collection()
        self._sync_bm25_from_storage()

    def _ensure_collection(self):
        try:
            collections = self.client.get_collections().collections
            exists = any(c.name == self.collection_name for c in collections)
            if not exists:
                logger.info(f"Creating Qdrant collection '{self.collection_name}' (dim={settings.EMBEDDING_DIMENSION}, Distance.COSINE)...")
                self.client.create_collection(
                    collection_name=self.collection_name,
                    vectors_config=VectorParams(
                        size=settings.EMBEDDING_DIMENSION,
                        distance=Distance.COSINE
                    )
                )
                logger.info(f"Collection '{self.collection_name}' created successfully.")
        except Exception as e:
            logger.error(f"Error ensuring Qdrant collection: {e}")

    def _sync_bm25_from_storage(self):
        """Pre-populates BM25 in-memory index from existing points in Qdrant."""
        try:
            res, _ = self.client.scroll(
                collection_name=self.collection_name,
                limit=1000,
                with_payload=True,
                with_vectors=False
            )
            docs = []
            for point in res:
                payload = point.payload or {}
                docs.append({
                    "chunk_id": str(point.id),
                    "document_id": payload.get("document_id"),
                    "course_id": payload.get("course_id"),
                    "module_id": payload.get("module_id"),
                    "lesson_id": payload.get("lesson_id"),
                    "page_number": payload.get("page_number", 1),
                    "section_title": payload.get("section_title", ""),
                    "visibility": payload.get("visibility", "student"),
                    "text": payload.get("text", "")
                })
            bm25_retriever.index_documents(docs)
            logger.info(f"Synced {len(docs)} documents to BM25 sparse index.")
        except Exception as e:
            logger.warning(f"Failed to scroll points for BM25 sync: {e}")

    def upsert_chunks(self, chunks: List[Dict[str, Any]], vectors: List[List[float]]) -> int:
        """Stores vectors and full metadata payload in Qdrant."""
        if not chunks or not vectors or len(chunks) != len(vectors):
            return 0

        points: List[PointStruct] = []
        bm25_docs: List[Dict[str, Any]] = []

        for chunk, vector in zip(chunks, vectors):
            payload = {
                "document_id": chunk["document_id"],
                "course_id": chunk["course_id"],
                "module_id": chunk.get("module_id"),
                "lesson_id": chunk.get("lesson_id"),
                "page_number": chunk.get("page_number", 1),
                "section_title": chunk.get("section_title", ""),
                "visibility": chunk.get("visibility", "student"),
                "chunk_index": chunk.get("chunk_index", 0),
                "text": chunk["text"]
            }
            points.append(PointStruct(
                id=chunk["chunk_id"],
                vector=vector,
                payload=payload
            ))
            bm25_docs.append({
                "chunk_id": chunk["chunk_id"],
                **payload
            })

        try:
            self.client.upsert(
                collection_name=self.collection_name,
                points=points
            )
            # Update BM25 index with new chunks
            existing_corpus = list(bm25_retriever.corpus)
            existing_corpus.extend(bm25_docs)
            bm25_retriever.index_documents(existing_corpus)

            logger.info(f"Upserted {len(points)} vectors into Qdrant collection '{self.collection_name}'.")
            return len(points)
        except Exception as ex:
            logger.error(f"Failed to upsert points into Qdrant: {ex}")
            raise RuntimeError(f"Qdrant upsert failed: {str(ex)}")

    def search_dense(
        self,
        query_vector: List[float],
        course_id: Optional[int],
        visibility: str = "student",
        top_k: int = 8,
        threshold: float = 0.35
    ) -> List[Dict[str, Any]]:
        """Dense semantic search with hard RBAC metadata filtering."""
        conditions = []
        if course_id is not None:
            conditions.append(FieldCondition(key="course_id", match=MatchValue(value=int(course_id))))
        if visibility == "student":
            conditions.append(FieldCondition(key="visibility", match=MatchValue(value="student")))

        query_filter = Filter(must=conditions) if conditions else None

        try:
            res = self.client.query_points(
                collection_name=self.collection_name,
                query=query_vector,
                query_filter=query_filter,
                limit=top_k,
                score_threshold=threshold
            )
            hits = getattr(res, "points", res)

            results: List[Dict[str, Any]] = []
            for hit in hits:
                payload = hit.payload or {}
                results.append({
                    "chunk_id": str(hit.id),
                    "score": float(hit.score),
                    "text": payload.get("text", ""),
                    "document_id": payload.get("document_id"),
                    "course_id": payload.get("course_id"),
                    "module_id": payload.get("module_id"),
                    "lesson_id": payload.get("lesson_id"),
                    "page_number": payload.get("page_number", 1),
                    "section_title": payload.get("section_title", ""),
                    "visibility": payload.get("visibility", "student")
                })

            return results
        except Exception as ex:
            logger.error(f"Qdrant vector search failed: {ex}")
            return []

    def count(self) -> int:
        try:
            info = self.client.get_collection(self.collection_name)
            return info.points_count or 0
        except Exception:
            return 0

    def delete_document_vectors(self, document_id: int) -> bool:
        try:
            doc_filter = Filter(must=[FieldCondition(key="document_id", match=MatchValue(value=int(document_id)))])
            self.client.delete(collection_name=self.collection_name, points_selector=doc_filter)
            logger.info(f"Deleted vectors for document_id={document_id} from Qdrant.")
            # Remove from BM25 corpus
            updated = [d for d in bm25_retriever.corpus if d.get("document_id") != document_id]
            bm25_retriever.index_documents(updated)
            return True
        except Exception as ex:
            logger.error(f"Failed to delete document vectors from Qdrant: {ex}")
            return False

qdrant_store = QdrantStore()
