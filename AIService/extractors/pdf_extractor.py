"""
PyMuPDF Multi-page PDF Extractor.
Extracts text page-by-page preserving document flow and page coordinates.
"""
from pathlib import Path
from typing import List, Dict, Any
import pymupdf
from extractors.text_cleaner import clean_text
from core.logger import logger

class PdfExtractor:
    @staticmethod
    def extract(file_path: str) -> List[Dict[str, Any]]:
        path = Path(file_path)
        if not path.exists():
            raise FileNotFoundError(f"PDF not found: {file_path}")

        pages: List[Dict[str, Any]] = []
        doc = pymupdf.open(str(path))
        try:
            total = len(doc)
            logger.info(f"Extracting {total} pages from PDF '{path.name}'...")
            for idx in range(total):
                page = doc[idx]
                text = clean_text(page.get_text("text"))
                if text:
                    pages.append({
                        "page_number": idx + 1,
                        "section_title": f"{path.stem} (Page {idx + 1})",
                        "text": text
                    })
        finally:
            doc.close()

        return pages
