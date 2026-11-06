"""
Enterprise Agentic RAG Engine.
Orchestrates:
Semantic Cache -> Guardrails -> Query Router -> Hybrid Search (Dense+BM25) ->
RRF Fusion -> Self-RAG Context Verification -> Model Cascading -> Telemetry.
"""
from typing import Dict, Any, List, Optional
from config import settings
from core.logger import logger
from core.guardrails import check_prompt_security
from core.cache import semantic_cache
from embeddings.gemini_embedder import embedder
from vectorstore.qdrant_store import qdrant_store
from search.bm25_retriever import bm25_retriever
from search.rrf_fusion import reciprocal_rank_fusion
from llm.provider_factory import llm_factory
from llm.router import query_router
from agent.self_rag import self_rag
from rag.prompt_templates import build_system_prompt, format_context_block
from telemetry.tracer import RequestTracer

class EnterpriseRAGEngine:
    def process_query(
        self,
        query: str,
        user_id: str,
        user_role: str = "Student",
        course_id: Optional[int] = None,
        learning_mode: str = "CourseAssistant",
        chat_history: Optional[List[Dict[str, str]]] = None
    ) -> Dict[str, Any]:
        tracer = RequestTracer(query=query, user_id=user_id, course_id=course_id)
        
        # 1. Pillar 6: Prompt Injection & Adversarial Guardrails
        is_safe, guardrail_reason = check_prompt_security(query)
        if not is_safe:
            return {
                "answer": guardrail_reason,
                "sources": [],
                "grounded_in_rag": False,
                "was_cache_hit": False,
                "learning_mode": learning_mode,
                "tokens_used": 0,
                "llm_provider": "guardrail_interceptor",
                "model_used": "security_firewall",
                "duration_ms": 10,
                "eval_metrics": {"faithfulness": 1.0, "answer_relevance": 0.0, "context_precision": 0.0}
            }
        tracer.mark_stage("guardrails_passed")

        # 2. Pillar 1: Agentic Intent Router
        intent_info = query_router.classify_intent(query, requested_mode=learning_mode)
        active_mode = intent_info.get("mode", learning_mode)
        tracer.mark_stage("intent_classified")

        # 3. Dense Query Embedding
        query_vector = embedder.embed_text(query)
        tracer.mark_stage("query_embedded")

        # 4. Pillar 4: Semantic Caching Layer
        cached_result = semantic_cache.lookup(
            query_vector=query_vector,
            course_id=course_id,
            learning_mode=active_mode
        )
        if cached_result:
            tracer.mark_stage("cache_hit")
            telemetry = tracer.finalize(
                llm_response={"provider": "semantic_cache", "model": "redis_gptcache", "tokens": 0},
                sources=cached_result["sources"],
                was_cache_hit=True
            )
            return {
                "answer": cached_result["answer"],
                "sources": cached_result["sources"],
                "grounded_in_rag": len(cached_result["sources"]) > 0,
                "was_cache_hit": True,
                "learning_mode": active_mode,
                "tokens_used": 0,
                "llm_provider": "semantic_cache",
                "model_used": "semantic-cache-hit",
                "duration_ms": telemetry["total_latency_ms"],
                "eval_metrics": telemetry["eval_metrics"]
            }

        # 5. Pillar 1: Query Decomposition (Self-RAG)
        sub_queries = self_rag.decompose_query(query)

        # 6. Pillar 2: Hybrid Search (Dense + Sparse BM25)
        visibility = "student" if user_role.lower() == "student" else "instructor_only"
        
        dense_hits = []
        sparse_hits = []
        for sq in sub_queries:
            sq_vec = query_vector if sq == query else embedder.embed_text(sq)
            dense_hits.extend(qdrant_store.search_dense(
                query_vector=sq_vec,
                course_id=course_id,
                visibility=visibility,
                top_k=settings.HYBRID_SEARCH_TOP_K
            ))
            sparse_hits.extend(bm25_retriever.search(
                query=sq,
                course_id=course_id,
                visibility=visibility,
                top_k=settings.HYBRID_SEARCH_TOP_K
            ))
        tracer.mark_stage("hybrid_retrieval_complete")

        # 7. Pillar 2: Reciprocal Rank Fusion (RRF) Re-ranking
        fused_chunks = reciprocal_rank_fusion(
            dense_results=dense_hits,
            sparse_results=sparse_hits,
            top_k=5,
            rrf_k=settings.RRF_K
        )
        tracer.mark_stage("rrf_ranking_complete")

        # 8. Pillar 1: Corrective RAG (CRAG) Gap Detection
        has_sufficient_context, crag_action = self_rag.evaluate_retrieval_gap(fused_chunks)

        # 9. Prompt Assembly & Context Grounding
        system_instruction = build_system_prompt(user_role=user_role, learning_mode=active_mode)
        context_block = format_context_block(fused_chunks)

        user_prompt = f"""CONTEXT FROM COURSE REPOSITORY:
{context_block}

STUDENT INQUIRY:
{query}

Please provide your grounded, pedagogical response following the required role guidelines."""

        # 10. Pillar 4: Model Cascading (Gemini 3.1 Flash Lite -> Groq GPT-OSS-120B -> Local)
        llm_res = llm_factory.generate(
            prompt=user_prompt,
            system_instruction=system_instruction,
            temperature=0.2,
            max_tokens=1024
        )
        tracer.mark_stage("llm_synthesis_complete")

        # 11. Format Clickable Source Citations
        citations = []
        for idx, chunk in enumerate(fused_chunks, 1):
            citations.append({
                "source_index": idx,
                "document_id": chunk.get("document_id"),
                "chunk_id": chunk.get("chunk_id"),
                "source_title": chunk.get("section_title") or f"Section {idx}",
                "page_number": chunk.get("page_number"),
                "relevance_score": chunk.get("fused_score") or chunk.get("score", 0.0),
                "excerpt": chunk.get("text", "")[:200] + "..."
            })

        # 12. Pillar 4: Save to Semantic Cache
        answer_text = llm_res.get("text", "")
        semantic_cache.store(
            query=query,
            query_vector=query_vector,
            answer=answer_text,
            sources=citations,
            course_id=course_id,
            learning_mode=active_mode
        )

        # 13. Pillar 3: Telemetry & Automated Evaluation
        telemetry = tracer.finalize(llm_response=llm_res, sources=citations, was_cache_hit=False)

        return {
            "answer": answer_text,
            "sources": citations,
            "grounded_in_rag": len(citations) > 0,
            "was_cache_hit": False,
            "learning_mode": active_mode,
            "tokens_used": llm_res.get("total_tokens", 0),
            "llm_provider": llm_res.get("provider", ""),
            "model_used": llm_res.get("model", ""),
            "failover_occurred": llm_res.get("failover_occurred", False),
            "duration_ms": telemetry["total_latency_ms"],
            "eval_metrics": telemetry["eval_metrics"]
        }

rag_engine = EnterpriseRAGEngine()
