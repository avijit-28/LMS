"""
Pillar 2: Reciprocal Rank Fusion (RRF) & Re-Ranking.
Fuses dense semantic vector rankings with sparse BM25 lexical rankings:
RRF(d) = sum( 1 / (k + rank_m(d)) )
"""
from typing import List, Dict, Any
from config import settings
from core.logger import logger

def reciprocal_rank_fusion(
    dense_results: List[Dict[str, Any]],
    sparse_results: List[Dict[str, Any]],
    top_k: int = 5,
    rrf_k: int = 60
) -> List[Dict[str, Any]]:
    """
    Combines dense semantic hits and sparse keyword hits using RRF.
    """
    fused_scores: Dict[str, float] = {}
    doc_map: Dict[str, Dict[str, Any]] = {}

    # 1. Process Dense Rankings
    for rank, hit in enumerate(dense_results, 1):
        chunk_id = str(hit.get("chunk_id"))
        doc_map[chunk_id] = hit
        score = 1.0 / (rrf_k + rank)
        fused_scores[chunk_id] = fused_scores.get(chunk_id, 0.0) + (score * settings.DENSE_WEIGHT)

    # 2. Process Sparse BM25 Rankings
    for rank, hit in enumerate(sparse_results, 1):
        chunk_id = str(hit.get("chunk_id"))
        if chunk_id not in doc_map:
            doc_map[chunk_id] = hit
        score = 1.0 / (rrf_k + rank)
        fused_scores[chunk_id] = fused_scores.get(chunk_id, 0.0) + (score * settings.SPARSE_WEIGHT)

    # 3. Sort by fused RRF score
    sorted_chunks = sorted(fused_scores.items(), key=lambda x: x[1], reverse=True)

    final_results: List[Dict[str, Any]] = []
    for rank, (chunk_id, score) in enumerate(sorted_chunks[:top_k], 1):
        doc = dict(doc_map[chunk_id])
        doc["rrf_rank"] = rank
        doc["fused_score"] = round(score, 5)
        final_results.append(doc)

    logger.info(f"RRF Fusion completed: {len(dense_results)} dense + {len(sparse_results)} sparse -> {len(final_results)} top fused chunks.")
    return final_results
