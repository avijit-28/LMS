using System;
using System.Collections.Generic;
using LMS.Models.AI;
using LMS.Services;

namespace LMS.ViewModels
{
    public class ChatRequestViewModel
    {
        public string Message { get; set; }
        public string SessionGuid { get; set; }
        public int? CourseId { get; set; }
        public int? ModuleId { get; set; }
        public int? LessonId { get; set; }
        public string LearningMode { get; set; } = "CourseAssistant"; // CourseAssistant, SocraticTutor, ExamPrep, PracticeQuiz, CodeDebugger
        public string Persona { get; set; } = "learner_socratic";
    }

    public class CourseRecommendationCard
    {
        public int CourseId { get; set; }
        public string Title { get; set; }
        public string CategoryName { get; set; }
        public string Level { get; set; }
        public decimal Price { get; set; }
        public string ThumbnailUrl { get; set; }
        public string DetailUrl { get; set; }
        public string LearnUrl { get; set; }
        public bool IsEnrolled { get; set; }
        public int ProgressPercent { get; set; }
    }

    public class ChatResponseViewModel
    {
        public bool Success { get; set; } = true;
        public string Reply { get; set; }
        public string SessionGuid { get; set; }
        public string ModelUsed { get; set; }
        public string Provider { get; set; }
        public bool IsGrounded { get; set; }
        public string AgenticIntent { get; set; }
        public List<AiCitationDto> Citations { get; set; } = new List<AiCitationDto>();
        public AiTelemetryDto Telemetry { get; set; }
        public List<string> Suggestions { get; set; } = new List<string>();
        public List<CourseRecommendationCard> RecommendedCourses { get; set; } = new List<CourseRecommendationCard>();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string ErrorMessage { get; set; }
    }

    public class ChatHistoryMessageDto
    {
        public int MessageId { get; set; }
        public string Sender { get; set; }
        public string Content { get; set; }
        public bool IsGrounded { get; set; }
        public string ModelUsed { get; set; }
        public List<AiCitationDto> Citations { get; set; } = new List<AiCitationDto>();
        public DateTime Timestamp { get; set; }
        public List<CourseRecommendationCard> RecommendedCourses { get; set; } = new List<CourseRecommendationCard>();
    }

    public class AiStudioViewModel
    {
        public string SessionGuid { get; set; }
        public int? ActiveCourseId { get; set; }
        public string ActiveCourseTitle { get; set; }
        public string ActiveLearningMode { get; set; } = "CourseAssistant";
        public List<CourseOptionViewModel> EnrolledCourses { get; set; } = new List<CourseOptionViewModel>();
        public List<ChatHistoryMessageDto> InitialMessages { get; set; } = new List<ChatHistoryMessageDto>();
        public bool IsUserLoggedIn { get; set; }
        public string UserRole { get; set; }

        // Additional compatibility properties
        public int? CurrentSessionId { get; set; }
        public List<AiChatSession> RecentSessions { get; set; } = new List<AiChatSession>();
        public List<AiChatMessage> Messages { get; set; } = new List<AiChatMessage>();
        public int TotalChunksIndexed { get; set; }
        public string SelectedLearningMode { get; set; }
        public bool IsGuestUser { get; set; }
    }

    public class CourseOptionViewModel
    {
        public int CourseId { get; set; }
        public string Title { get; set; }
        public string CategoryName { get; set; }
        public bool IsEnrolled { get; set; }
    }
}
