"""
Dense Embedding Engine using Google Gemini Embedding Model.
Provides 3072-dimensional semantic vectors with zero local memory strain.
"""
from typing import List
import numpy as np
from google import genai
from config import settings
from core.logger import logger

class GeminiEmbedder:
    _instance = None

    def __new__(cls):
        if cls._instance is None:
            cls._instance = super(GeminiEmbedder, cls).__new__(cls)
            cls._instance._init_client()
        return cls._instance

    def _init_client(self):
        self.api_key = settings.GEMINI_API_KEY
        self.model_name = settings.GEMINI_EMBEDDING_MODEL
        self.dim = settings.EMBEDDING_DIMENSION
        self.client = None

        if self.api_key:
            try:
                self.client = genai.Client(api_key=self.api_key)
                logger.info(f"Initialized Gemini Embedder with model '{self.model_name}' (dim={self.dim}).")
            except Exception as e:
                logger.error(f"Failed to initialize Gemini embedder client: {e}")
                self.client = None
        else:
            logger.warning("No GEMINI_API_KEY provided for embeddings, using zero-cost deterministic fallback.")

    @property
    def dimension(self) -> int:
        return self.dim

    def embed_text(self, text: str) -> List[float]:
        """Embeds a single query string."""
        if not text:
            return [0.0] * self.dim

        if self.client:
            try:
                res = self.client.models.embed_content(
                    model=self.model_name,
                    contents=text
                )
                if res.embeddings and len(res.embeddings) > 0:
                    return res.embeddings[0].values
            except Exception as e:
                logger.warning(f"Gemini embed_content call failed: {e}. Using deterministic vectorizer.")

        return self._deterministic_fallback(text)

    def embed_documents(self, texts: List[str]) -> List[List[float]]:
        """Embeds a list of document chunks."""
        if not texts:
            return []

        embeddings = []
        for t in texts:
            embeddings.append(self.embed_text(t))
        return embeddings

    def _deterministic_fallback(self, text: str) -> List[float]:
        """Fast, normalized 3072-dimensional vector fallback."""
        vec = np.zeros(self.dim, dtype=np.float32)
        words = text.lower().split()
        for idx, word in enumerate(words):
            h = hash(word) % self.dim
            vec[h] += 1.0 / (idx + 1)
        norm = np.linalg.norm(vec)
        if norm > 0:
            vec = vec / norm
        return vec.tolist()

embedder = GeminiEmbedder()
