"""
Pillar 4: High-Performance Semantic Caching Layer.
Caches query embeddings and answers. Queries with >= 0.95 similarity return
instant (<50ms), 0-token, 0-cost cached responses.
Implements:
- C-03: Semantic Cache Hit (>= 0.95)
- C-04: Semantic Cache Divergence (< 0.95)
- N-07: Targeted Cache Invalidation on Course Update
"""
import time
import numpy as np
from typing import Dict, Any, Optional, List
from config import settings
from core.logger import logger

class SemanticCacheEntry:
    def __init__(
        self,
        query: str,
        embedding: List[float],
        answer: str,
        sources: List[Dict[str, Any]],
        course_id: Optional[int],
        learning_mode: str
    ):
        self.query = query
        self.embedding = np.array(embedding, dtype=np.float32)
        self.norm = np.linalg.norm(self.embedding)
        self.answer = answer
        self.sources = sources
        self.course_id = course_id
        self.learning_mode = learning_mode
        self.created_at = time.time()
        self.hit_count = 1

class SemanticCache:
    _instance = None

    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(SemanticCache, cls).__new__(cls)
            cls._instance.entries: List[SemanticCacheEntry] = []
            cls._instance.threshold = settings.SEMANTIC_CACHE_SIMILARITY_THRESHOLD
            cls._instance.max_size = 500
        return cls._instance

    def lookup(
        self,
        query_vector: List[float],
        course_id: Optional[int],
        learning_mode: str
    ) -> Optional[Dict[str, Any]]:
        """Finds closest matching cached query if cosine similarity exceeds threshold."""
        if not settings.ENABLE_SEMANTIC_CACHE or not self.entries:
            return None

        q_vec = np.array(query_vector, dtype=np.float32)
        q_norm = np.linalg.norm(q_vec)
        if q_norm == 0:
            return None

        best_sim = 0.0
        best_entry: Optional[SemanticCacheEntry] = None

        for entry in self.entries:
            # Check context scope
            if entry.course_id != course_id or entry.learning_mode != learning_mode:
                continue

            dot = np.dot(q_vec, entry.embedding)
            sim = dot / (q_norm * entry.norm + 1e-9)

            if sim > best_sim:
                best_sim = sim
                best_entry = entry

        if best_entry and best_sim >= self.threshold:
            best_entry.hit_count += 1
            logger.info(f"Semantic Cache HIT (sim={best_sim:.4f}): Serving cached response instantly.")
            return {
                "answer": best_entry.answer,
                "sources": best_entry.sources,
                "was_cache_hit": True,
                "similarity": float(best_sim),
                "original_query": best_entry.query
            }

        return None

    def store(
        self,
        query: str,
        query_vector: List[float],
        answer: str,
        sources: List[Dict[str, Any]],
        course_id: Optional[int],
        learning_mode: str
    ):
        """Stores a generated query-response pair in the semantic cache."""
        if not settings.ENABLE_SEMANTIC_CACHE:
            return

        if len(self.entries) >= self.max_size:
            # Evict oldest entry
            self.entries.pop(0)

        entry = SemanticCacheEntry(
            query=query,
            embedding=query_vector,
            answer=answer,
            sources=sources,
            course_id=course_id,
            learning_mode=learning_mode
        )
        self.entries.append(entry)
        logger.info(f"Cached new response for query '{query[:40]}...' (Total cached: {len(self.entries)})")

    def invalidate_course_cache(self, course_id: int) -> int:
        """
        N-07: Targeted Cache Invalidation on Course Update.
        Purges all cached answers associated with the specified course_id.
        """
        initial_count = len(self.entries)
        self.entries = [e for e in self.entries if e.course_id != course_id]
        evicted = initial_count - len(self.entries)
        logger.info(f"N-07 Cache Invalidation: Evicted {evicted} cached queries for Course #{course_id}.")
        return evicted

semantic_cache = SemanticCache()
