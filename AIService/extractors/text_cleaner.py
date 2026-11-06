"""
Text Cleaning and Normalization Utility.
Removes duplicate linebreaks, cleans hyphenated line wraps, and normalizes whitespace.
"""
import re
import unicodedata

def clean_text(text: str) -> str:
    if not text:
        return ""
    # Normalize unicode
    text = unicodedata.normalize("NFKC", text)
    # Fix hyphenated words broken across lines
    text = re.sub(r"(\w+)-\n(\w+)", r"\1\2", text)
    # Replace multiple spaces with a single space
    text = re.sub(r"[ \t]+", " ", text)
    # Replace more than 2 consecutive newlines with 2
    text = re.sub(r"\n\s*\n\s*\n+", "\n\n", text)
    return text.strip()
