"""
Pillar 2: Sparse Lexical BM25 Retriever.
Scores exact keyword matches, code tokens, and acronyms across indexed course chunks.
"""
import math
import re
from typing import List, Dict, Any, Optional

def tokenize(text: str) -> List[str]:
    """Tokenizes text preserving alphanumeric keywords, underscores, and code tokens."""
    return re.findall(r"[A-Za-z0-9_#]+", text.lower())

class BM25Retriever:
    def __init__(self, k1: float = 1.5, b: float = 0.75):
        self.k1 = k1
        self.b = b
        self.corpus: List[Dict[str, Any]] = []
        self.doc_len: List[int] = []
        self.avg_dl: float = 0.0
        self.doc_freqs: Dict[str, int] = {}
        self.idf: Dict[str, float] = {}

    def index_documents(self, documents: List[Dict[str, Any]]):
        """Builds in-memory BM25 index over documents."""
        self.corpus = documents
        self.doc_len = [len(tokenize(doc.get("text", ""))) for doc in documents]
        total_docs = len(documents)
        self.avg_dl = sum(self.doc_len) / total_docs if total_docs > 0 else 1.0

        self.doc_freqs = {}
        for doc in documents:
            tokens = set(tokenize(doc.get("text", "")))
            for tok in tokens:
                self.doc_freqs[tok] = self.doc_freqs.get(tok, 0) + 1

        self.idf = {}
        for tok, freq in self.doc_freqs.items():
            self.idf[tok] = math.log((total_docs - freq + 0.5) / (freq + 0.5) + 1.0)

    def search(
        self,
        query: str,
        course_id: Optional[int] = None,
        visibility: str = "student",
        top_k: int = 8
    ) -> List[Dict[str, Any]]:
        """Scores documents matching query and course isolation filters."""
        if not self.corpus:
            return []

        q_tokens = tokenize(query)
        if not q_tokens:
            return []

        scores: List[tuple] = []
        for idx, doc in enumerate(self.corpus):
            # Apply RBAC & course filtering
            if course_id is not None and doc.get("course_id") != course_id:
                continue
            if visibility == "student" and doc.get("visibility") == "instructor_only":
                continue

            doc_tokens = tokenize(doc.get("text", ""))
            doc_len = self.doc_len[idx]
            score = 0.0

            freq_map: Dict[str, int] = {}
            for t in doc_tokens:
                freq_map[t] = freq_map.get(t, 0) + 1

            for q_tok in q_tokens:
                if q_tok not in freq_map:
                    continue
                tf = freq_map[q_tok]
                idf = self.idf.get(q_tok, 0.0)
                numerator = tf * (self.k1 + 1.0)
                denominator = tf + self.k1 * (1.0 - self.b + self.b * (doc_len / self.avg_dl))
                score += idf * (numerator / (denominator + 1e-9))

            if score > 0:
                scores.append((score, doc))

        scores.sort(key=lambda x: x[0], reverse=True)
        results = []
        for score, doc in scores[:top_k]:
            res = dict(doc)
            res["bm25_score"] = round(score, 4)
            results.append(res)
        return results

bm25_retriever = BM25Retriever()
