using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using LMS.Models;

namespace LMS.ViewModels
{
    public class StudentTaskItemViewModel
    {
        public int AssignmentId { get; set; }
        public string Title { get; set; }
        public string CourseTitle { get; set; }
        public int CourseId { get; set; }
        public decimal MaxMarks { get; set; }
        public DateTime? DueDate { get; set; }
        public bool IsSubmitted { get; set; }
        public string SubmissionStatus { get; set; } // "Not Submitted", "In Review", "Graded"
        public decimal? MarksAwarded { get; set; }
        public string Feedback { get; set; }
    }

    public class StudentDashboardViewModel
    {
        public string StudentFirstName { get; set; }
        public int ActiveCourses { get; set; }
        public int CompletedCourses { get; set; }
        public int CertificatesCount { get; set; }
        public int CompletedQuizzes { get; set; }

        public List<Enrollment> EnrolledCourses { get; set; } = new List<Enrollment>();
        public List<Certificate> Certificates { get; set; } = new List<Certificate>();
        public List<Announcement> Announcements { get; set; } = new List<Announcement>();
        public List<Assignment> UpcomingAssignments { get; set; } = new List<Assignment>();
        public List<StudentTaskItemViewModel> PracticalTasks { get; set; } = new List<StudentTaskItemViewModel>();
        public List<QuizAttempt> RecentQuizResults { get; set; } = new List<QuizAttempt>();
    }

    public class CourseDetailViewModel
    {
        public Course Course { get; set; }
        public ApplicationUser Instructor { get; set; }
        public List<Module> Modules { get; set; } = new List<Module>();
        public List<Quiz> Quizzes { get; set; } = new List<Quiz>();
        public List<Quiz> ModuleQuizzes { get; set; } = new List<Quiz>();
        public Quiz FinalExam { get; set; }
        public bool IsFinalExamUnlocked { get; set; }
        public bool HasPassedFinalExam { get; set; }
        public List<Assignment> Assignments { get; set; } = new List<Assignment>();
        public List<CourseReview> Reviews { get; set; } = new List<CourseReview>();
        public bool IsEnrolled { get; set; }
        public Enrollment Enrollment { get; set; }
        public bool HasReviewed { get; set; }
        public CourseReview UserReview { get; set; }
        public decimal AverageRating { get; set; }
        public int TotalReviews { get; set; }
    }

    public class LearningViewModel
    {
        public Course Course { get; set; }
        public Module CurrentModule { get; set; }
        public Lesson CurrentLesson { get; set; }
        public List<Module> Modules { get; set; } = new List<Module>();
        public HashSet<int> CompletedLessonIds { get; set; } = new HashSet<int>();
        public decimal ProgressPercentage { get; set; }
        public int? PreviousLessonId { get; set; }
        public int? NextLessonId { get; set; }
        public bool IsCurrentCompleted { get; set; }
        public bool IsCourseCompleted { get; set; }
        public Quiz FinalExam { get; set; }
        public bool IsFinalExamUnlocked { get; set; }
        public bool HasPassedFinalExam { get; set; }
        public decimal FinalExamScore { get; set; }
        public HashSet<int> SubmittedAssignmentIds { get; set; } = new HashSet<int>();
        public HashSet<int> PassedQuizIds { get; set; } = new HashSet<int>();
        public List<Assignment> CourseAssignments { get; set; } = new List<Assignment>();
        public List<Quiz> ModuleQuizzes { get; set; } = new List<Quiz>();
        public Certificate Certificate { get; set; }
    }

    public class QuizAttemptViewModel
    {
        public Quiz Quiz { get; set; }
        public int AttemptId { get; set; }
        public List<Question> Questions { get; set; } = new List<Question>();
        public int? TimeLimitMinutes { get; set; }
    }

    public class QuizSubmitViewModel
    {
        public int AttemptId { get; set; }
        public List<QuizAnswerInput> Answers { get; set; } = new List<QuizAnswerInput>();
    }

    public class QuizAnswerInput
    {
        public int QuestionId { get; set; }
        public int? SelectedOptionId { get; set; }
    }

    public class QuizResultViewModel
    {
        public int AttemptId { get; set; }
        public string QuizTitle { get; set; }
        public string CourseTitle { get; set; }
        public int CourseId { get; set; }
        public decimal Score { get; set; }
        public int TotalMarks { get; set; }
        public int ObtainedMarks { get; set; }
        public bool IsPassed { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class AssignmentSubmissionViewModel
    {
        public int AssignmentId { get; set; }
        public Assignment Assignment { get; set; }
        public Submission ExistingSubmission { get; set; }

        [Display(Name = "Submission Text / Solution")]
        public string SubmissionText { get; set; }

        [Display(Name = "File Upload / Attachment Link")]
        public string FileUrl { get; set; }
    }

    public class StudentReceiptViewModel
    {
        public int PaymentId { get; set; }
        public string InvoiceNumber { get; set; }
        public string TransactionId { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentStatus { get; set; }
        public decimal Amount { get; set; }

        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentEmail { get; set; }

        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public string CategoryName { get; set; }
        public string InstructorName { get; set; }
        public decimal DurationHours { get; set; }
    }

    public class StudentAllReceiptsViewModel
    {
        public string StudentId { get; set; }
        public string StudentName { get; set; }
        public string StudentEmail { get; set; }
        public DateTime GeneratedDate { get; set; }
        public decimal TotalAmountPaid { get; set; }
        public int TotalCoursesPurchased { get; set; }
        public List<StudentReceiptViewModel> Receipts { get; set; } = new List<StudentReceiptViewModel>();
    }

    public class CertificateViewModel
    {
        public int CertificateId { get; set; }
        public string CertificateNumber { get; set; }
        public string StudentName { get; set; }
        public string CourseTitle { get; set; }
        public DateTime IssueDate { get; set; }
        public decimal? FinalScore { get; set; }
        public string InstructorName { get; set; }
        public string InstructorSignaturePath { get; set; }
    }

    public class CourseReviewInputModel
    {
        public int CourseId { get; set; }

        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
        public int Rating { get; set; }

        [Required(ErrorMessage = "Please write a review.")]
        [StringLength(2000)]
        public string ReviewText { get; set; }
    }
}
