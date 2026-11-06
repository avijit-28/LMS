using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LMS.Services
{
    public class AiChatRequestDto
    {
        public string user_id { get; set; }
        public string user_role { get; set; } = "Student"; // Student, Instructor, Admin
        public string session_id { get; set; }
        public int? course_id { get; set; }
        public int? module_id { get; set; }
        public int? lesson_id { get; set; }
        public string query { get; set; }
        public string learning_mode { get; set; } = "CourseAssistant"; // CourseAssistant, SocraticTutor, ExamPrep, PracticeQuiz, CodeDebugger
        public List<Dictionary<string, string>> history { get; set; } = new List<Dictionary<string, string>>();
    }

    public class AiCitationDto
    {
        public int source_index { get; set; }
        public int? document_id { get; set; }
        public string chunk_id { get; set; }
        public string source_title { get; set; }
        public int? page_number { get; set; }
        public double relevance_score { get; set; }
        public string excerpt { get; set; }
    }

    public class AiTelemetryDto
    {
        public string provider { get; set; }
        public string model { get; set; }
        public int prompt_tokens { get; set; }
        public int completion_tokens { get; set; }
        public int total_tokens { get; set; }
        public int latency_ms { get; set; }
        public bool was_cache_hit { get; set; }
        public string estimated_cost_usd { get; set; }
        public double faithfullness_heuristic { get; set; }
        public double answer_relevance_heuristic { get; set; }
    }

    public class AiChatResponseDto
    {
        public string answer { get; set; }
        public List<AiCitationDto> sources { get; set; } = new List<AiCitationDto>();
        public bool grounded_in_rag { get; set; }
        public bool was_cache_hit { get; set; }
        public string learning_mode { get; set; }
        public int tokens_used { get; set; }
        public string llm_provider { get; set; }
        public string model_used { get; set; }
        public bool failover_occurred { get; set; }
        public int duration_ms { get; set; }
        public Dictionary<string, double> eval_metrics { get; set; }
    }

    public class AiDocumentIndexResponseDto
    {
        public string status { get; set; }
        public int document_id { get; set; }
        public int course_id { get; set; }
        public int total_pages { get; set; }
        public int total_chunks { get; set; }
        public int chunks_created => total_chunks;
        public string message { get; set; }
    }

    public class AiQuizQuestionDto
    {
        public int question_number { get; set; }
        public string question { get; set; }
        public List<string> options { get; set; } = new List<string>();
        public string correct_answer { get; set; }
        public string explanation { get; set; }
    }

    public class AiQuizResponseDto
    {
        public string course_title { get; set; }
        public string difficulty { get; set; }
        public List<AiQuizQuestionDto> questions { get; set; } = new List<AiQuizQuestionDto>();
        public List<AiCitationDto> citations { get; set; } = new List<AiCitationDto>();
        public AiTelemetryDto telemetry { get; set; }
    }

    public class AiQuizRequestDto
    {
        public int course_id { get; set; }
        public int? module_id { get; set; }
        public string topic { get; set; }
        public int num_questions { get; set; } = 5;
        public string difficulty { get; set; } = "Medium"; // Easy, Medium, Hard
        public string user_id { get; set; }
        public string user_role { get; set; } = "Student";
    }

    public class AiSummarizeRequestDto
    {
        public int course_id { get; set; }
        public int? document_id { get; set; }
        public string format { get; set; } = "bullet_points"; // bullet_points, executive_summary, study_notes
        public string user_id { get; set; }
        public string user_role { get; set; } = "Student";
    }

    public class AiSummarizeResponseDto
    {
        public string summary { get; set; }
        public List<string> key_takeaways { get; set; } = new List<string>();
        public List<AiCitationDto> citations { get; set; } = new List<AiCitationDto>();
        public AiTelemetryDto telemetry { get; set; }
    }

    public class AiCodeExplainRequestDto
    {
        public string code_snippet { get; set; }
        public string language { get; set; } = "python";
        public string error_message { get; set; }
        public int? course_id { get; set; }
        public string user_id { get; set; }
        public string user_role { get; set; } = "Student";
    }

    public class AiCodeExplainResponseDto
    {
        public string explanation { get; set; }
        public string fixed_code { get; set; }
        public List<string> best_practices { get; set; } = new List<string>();
        public AiTelemetryDto telemetry { get; set; }
    }

    public class AiStudyPlanRequestDto
    {
        public int course_id { get; set; }
        public string student_name { get; set; } = "Student";
        public int completed_lessons { get; set; }
        public int total_lessons { get; set; }
        public int target_days { get; set; } = 14;
        public string user_id { get; set; }
        public string user_role { get; set; } = "Student";
    }

    public class AiStudyPlanResponseDto
    {
        public string study_plan { get; set; }
        public List<string> daily_milestones { get; set; } = new List<string>();
        public AiTelemetryDto telemetry { get; set; }
    }

    public class AiHealthResponseDto
    {
        public string status { get; set; }
        public string service { get; set; }
        public string version { get; set; }
        public string environment { get; set; }
    }

    public interface IAiServiceClient
    {
        Task<AiChatResponseDto> AskQuestionAsync(AiChatRequestDto request, string internalApiKey, string authorizedCourses);
        Task<AiDocumentIndexResponseDto> IndexDocumentAsync(int documentId, string filePath, int courseId, int? moduleId, int? lessonId, string visibility, string title, string internalApiKey);
        Task<bool> DeleteDocumentIndexAsync(int documentId, string internalApiKey);
        Task<AiQuizResponseDto> GenerateQuizAsync(AiQuizRequestDto request, string internalApiKey, string authorizedCourses);
        Task<AiSummarizeResponseDto> SummarizeAsync(AiSummarizeRequestDto request, string internalApiKey, string authorizedCourses);
        Task<AiCodeExplainResponseDto> ExplainCodeAsync(AiCodeExplainRequestDto request, string internalApiKey, string authorizedCourses);
        Task<AiStudyPlanResponseDto> GenerateStudyPlanAsync(AiStudyPlanRequestDto request, string internalApiKey, string authorizedCourses);
        Task<AiHealthResponseDto> GetHealthAsync();
    }
}
