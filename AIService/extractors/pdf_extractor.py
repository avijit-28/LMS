"""
PyMuPDF Multi-page PDF Extractor.
Extracts text page-by-page preserving document flow and page coordinates.
Implements:
- N-06: Zero-Text / Scanned PDF Ingestion Interceptor (< 50 chars validation)
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
            total_chars = 0
            for idx in range(total):
                page = doc[idx]
                text = clean_text(page.get_text("text"))
                if text:
                    total_chars += len(text)
                    pages.append({
                        "page_number": idx + 1,
                        "section_title": f"{path.stem} (Page {idx + 1})",
                        "text": text
                    })

            # N-06: Zero-Text / Scanned PDF Ingestion Interceptor
            if total_chars < 50:
                logger.warning(f"N-06 Ingestion Hygiene Alert: '{path.name}' yielded only {total_chars} characters.")
                raise ValueError("ParsingFailed: ScannedOrEncrypted - Insufficient readable text extracted (< 50 characters). The document may be image-only, scanned, or encrypted.")
        finally:
            doc.close()

        return pages
