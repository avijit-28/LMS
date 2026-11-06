"""
Semantic Recursive Chunker.
Splits document pages/sections into overlapping chunks and generates deterministic UUIDs.
"""
import uuid
import re
from typing import List, Dict, Any

class SemanticChunker:
    def __init__(self, chunk_size: int = 700, chunk_overlap: int = 150):
        self.chunk_size = chunk_size
        self.chunk_overlap = chunk_overlap

    def chunk_document_pages(
        self,
        pages: List[Dict[str, Any]],
        document_id: int,
        course_id: int,
        module_id: int = None,
        lesson_id: int = None,
        visibility: str = "student"
    ) -> List[Dict[str, Any]]:
        chunks: List[Dict[str, Any]] = []
        global_idx = 0

        for page in pages:
            text = page.get("text", "").strip()
            page_num = page.get("page_number", 1)
            section = page.get("section_title") or f"Section {page_num}"

            if not text or len(text) < 15:
                continue

            raw_splits = self._split_text(text)
            for split in raw_splits:
                split_clean = split.strip()
                if len(split_clean) < 15:
                    continue

                point_id = str(uuid.uuid5(uuid.NAMESPACE_DNS, f"doc_{document_id}_chunk_{global_idx}"))
                chunks.append({
                    "chunk_id": point_id,
                    "chunk_index": global_idx,
                    "document_id": document_id,
                    "course_id": course_id,
                    "module_id": module_id,
                    "lesson_id": lesson_id,
                    "page_number": page_num,
                    "section_title": section,
                    "visibility": visibility,
                    "text": split_clean
                })
                global_idx += 1

        return chunks

    def _split_text(self, text: str) -> List[str]:
        if len(text) <= self.chunk_size:
            return [text]

        splits = []
        start = 0
        text_len = len(text)

        while start < text_len:
            end = min(start + self.chunk_size, text_len)
            if end < text_len:
                # Break at paragraph or sentence boundary
                for sep in ["\n\n", "\n", ". ", "? ", "! ", " "]:
                    idx = text.rfind(sep, start + self.chunk_size // 2, end)
                    if idx != -1:
                        end = idx + len(sep)
                        break

            chunk = text[start:end].strip()
            if chunk:
                splits.append(chunk)

            if end >= text_len:
                break
            start = max(start + 1, end - self.chunk_overlap)

        return splits

chunker = SemanticChunker()
