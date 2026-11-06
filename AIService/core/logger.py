"""
Structured Logger with Secret Masking.
Ensures API keys, tokens, and credentials are never logged to console or logs.
"""
import re
import logging
import sys

SECRET_PATTERNS = [
    re.compile(r"AQ\.[0-9A-Za-z-_]{35,}"),
    re.compile(r"gsk_[0-9A-Za-z]{40,}"),
    re.compile(r"Bearer\s+[0-9A-Za-z-_.]+"),
    re.compile(r"key=([0-9A-Za-z-_]+)"),
    re.compile(r"lms_rag_secret[0-9A-Za-z-_]*")
]

class SecretMaskingFilter(logging.Filter):
    def filter(self, record: logging.LogRecord) -> bool:
        if isinstance(record.msg, str):
            msg = record.msg
            for pattern in SECRET_PATTERNS:
                msg = pattern.sub("[REDACTED_CREDENTIAL]", msg)
            record.msg = msg
        return True

def setup_logger(name: str = "EnterpriseRAG") -> logging.Logger:
    logger = logging.getLogger(name)
    if not logger.handlers:
        logger.setLevel(logging.INFO)
        handler = logging.StreamHandler(sys.stdout)
        formatter = logging.Formatter(
            fmt="%(asctime)s [%(levelname)s] [%(name)s] %(message)s",
            datefmt="%Y-%m-%d %H:%M:%S"
        )
        handler.setFormatter(formatter)
        handler.addFilter(SecretMaskingFilter())
        logger.addHandler(handler)
    return logger

logger = setup_logger()
