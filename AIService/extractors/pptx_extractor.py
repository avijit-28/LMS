"""
PPTX Slide-by-slide Extractor using python-pptx.
"""
from pathlib import Path
from typing import List, Dict, Any
from pptx import Presentation
from extractors.text_cleaner import clean_text
from core.logger import logger

class PptxExtractor:
    @staticmethod
    def extract(file_path: str) -> List[Dict[str, Any]]:
        path = Path(file_path)
        if not path.exists():
            raise FileNotFoundError(f"PPTX not found: {file_path}")

        prs = Presentation(str(path))
        slides: List[Dict[str, Any]] = []

        for idx, slide in enumerate(prs.slides, 1):
            slide_lines: List[str] = []
            for shape in slide.shapes:
                if shape.has_text_frame:
                    for paragraph in shape.text_frame.paragraphs:
                        t = clean_text(paragraph.text)
                        if t:
                            slide_lines.append(t)

            slide_text = "\n".join(slide_lines).strip()
            if slide_text:
                slides.append({
                    "page_number": idx,
                    "section_title": f"{path.stem} (Slide {idx})",
                    "text": slide_text
                })

        return slides
