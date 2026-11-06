"""
Pillar 1: Agentic Query Router & Intent Classifier.
Classifies query intention: factual, conceptual, coding, quiz_request, or logistics.
"""
import re
from typing import Dict, Any

class QueryRouter:
    @staticmethod
    def classify_intent(query: str, requested_mode: str = "CourseAssistant") -> Dict[str, Any]:
        q_lower = query.lower()

        # Check for Code / Debugging intent
        code_keywords = ["error", "exception", "bug", "traceback", "debug", "compile", "nullreference", "syntax", "stacktrace", "function", "class", "async", "c#", "python"]
        if requested_mode == "CodeDebugger" or any(k in q_lower for k in code_keywords):
            return {
                "intent": "code_debugging",
                "mode": "CodeDebugger",
                "requires_code_walkthrough": True
            }

        # Check for Quiz Generation intent
        quiz_keywords = ["generate quiz", "create mcq", "practice questions", "make a quiz", "test my knowledge", "quiz me"]
        if requested_mode == "PracticeQuiz" or any(k in q_lower for k in quiz_keywords):
            return {
                "intent": "quiz_generation",
                "mode": "PracticeQuiz",
                "requires_code_walkthrough": False
            }

        # Check for Summarization / Revision intent
        summary_keywords = ["summarize", "revision notes", "key concepts", "cheat sheet", "important formulas", "bullet points"]
        if requested_mode == "ExamPrep" or any(k in q_lower for k in summary_keywords):
            return {
                "intent": "summarization",
                "mode": "ExamPrep",
                "requires_code_walkthrough": False
            }

        # Check for Logistics / Deadline intent
        logistics_keywords = ["deadline", "due date", "submission date", "schedule", "passing grade", "syllabus", "office hours"]
        if any(k in q_lower for k in logistics_keywords):
            return {
                "intent": "course_logistics",
                "mode": "CourseAssistant",
                "requires_code_walkthrough": False
            }

        # Default: Conceptual / Socratic Learner Assistant
        return {
            "intent": "conceptual_qna",
            "mode": requested_mode or "CourseAssistant",
            "requires_code_walkthrough": False
        }

query_router = QueryRouter()
