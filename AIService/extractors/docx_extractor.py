"""
DOCX Multi-paragraph and Table Extractor using python-docx.
"""
from pathlib import Path
from typing import List, Dict, Any
from docx import Document
from extractors.text_cleaner import clean_text
from core.logger import logger

class DocxExtractor:
    @staticmethod
    def extract(file_path: str) -> List[Dict[str, Any]]:
        path = Path(file_path)
        if not path.exists():
            raise FileNotFoundError(f"DOCX not found: {file_path}")

        doc = Document(str(path))
        lines: List[str] = []

        # Extract paragraphs
        for p in doc.paragraphs:
            t = clean_text(p.text)
            if t:
                lines.append(t)

        # Extract tables
        for table in doc.tables:
            for row in table.rows:
                cells = [clean_text(c.text) for c in row.cells if clean_text(c.text)]
                if cells:
                    lines.append(" | ".join(cells))

        full_text = "\n\n".join(lines)
        if not full_text:
            return []

        return [{
            "page_number": 1,
            "section_title": path.stem,
            "text": full_text
        }]
