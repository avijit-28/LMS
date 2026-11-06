"""
Pillar 1: Self-RAG & Corrective RAG (CRAG) Engine.
Decomposes multi-part queries, evaluates context sufficiency, and reformulates queries on retrieval gaps.
"""
import re
from typing import List, Dict, Any, Tuple
from core.logger import logger

class SelfRAG:
    @staticmethod
    def decompose_query(query: str) -> List[str]:
        """
        Decomposes complex multi-concept questions into atomic sub-queries:
        E.g. "How does Module 2 array memory layout relate to cache locality in Module 5?"
        -> ["Module 2 array memory allocation layout", "Module 5 CPU cache locality"]
        """
        query_clean = query.strip()
        # Look for conjunctions connecting two distinct questions
        parts = re.split(r"\s+(?:and how does|compared to|versus|vs\.?|as well as|while in)\s+", query_clean, flags=re.IGNORECASE)
        if len(parts) > 1 and all(len(p.strip()) > 8 for p in parts):
            logger.info(f"Self-RAG Decomposed query into {len(parts)} sub-queries: {parts}")
            return [p.strip() for p in parts]
        return [query_clean]

    @staticmethod
    def evaluate_retrieval_gap(
        retrieved_chunks: List[Dict[str, Any]],
        threshold: float = 0.40
    ) -> Tuple[bool, str]:
        """
        Corrective RAG (CRAG): Evaluates whether retrieved context is adequate.
        Returns (has_sufficient_context: bool, corrective_action: str).
        """
        if not retrieved_chunks:
            return False, "reformulate_broad"

        top_score = retrieved_chunks[0].get("score", 0.0) or retrieved_chunks[0].get("fused_score", 0.0)
        if top_score < threshold:
            logger.info(f"CRAG Gap Detection: Top retrieval score ({top_score:.3f}) below threshold {threshold}.")
            return False, "supplement_domain_knowledge"

        return True, "proceed_to_synthesis"

    @staticmethod
    def reformulate_query(query: str) -> str:
        """Strips conversational noise and extracts high-impact keywords."""
        noise_phrases = [
            r"can you please explain", r"could you tell me about", r"what do you know about",
            r"i would like to understand", r"help me understand", r"please explain"
        ]
        q = query
        for np in noise_phrases:
            q = re.sub(np, "", q, flags=re.IGNORECASE)
        return q.strip()

self_rag = SelfRAG()
