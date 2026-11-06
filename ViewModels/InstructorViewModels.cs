using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Web;
using System.Web.Mvc;
using LMS.Models;

namespace LMS.ViewModels
{
    public class InstructorDashboardViewModel
    {
        public int TotalCourses { get; set; }
        public int TotalStudents { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageRating { get; set; }

        public List<Course> Courses { get; set; } = new List<Course>();
        public List<Enrollment> RecentEnrollments { get; set; } = new List<Enrollment>();
    }

    public class CourseFormViewModel
    {
        public int CourseId { get; set; }

        [Required(ErrorMessage = "Please select a category.")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200)]
        public string Title { get; set; }

        [Display(Name = "Description")]
        public string Description { get; set; }

        [Display(Name = "Thumbnail URL")]
        [StringLength(500)]
        public string Thumbnail { get; set; }

        [Required]
        [Range(0, 100000, ErrorMessage = "Price must be 0 or positive.")]
        [Display(Name = "Course Price (INR)")]
        public decimal Price { get; set; } = 0;

        [Required]
        public string Level { get; set; } = "Beginner"; // Beginner, Intermediate, Advanced

        [Display(Name = "Duration In Hours")]
        [Range(0, 1000)]
        public decimal DurationInHours { get; set; } = 0;

        [Display(Name = "Course Access Validity (Days)")]
        [Range(1, 3650, ErrorMessage = "Access validity must be between 1 and 3650 days.")]
        public int ValidityInDays { get; set; } = 30; // Access validity in days set by instructor

        public string Status { get; set; } = "Draft"; // Draft, Published, Archived

        public IEnumerable<SelectListItem> Categories { get; set; }
    }

    public class ModuleFormViewModel
    {
        public int ModuleId { get; set; }
        public int CourseId { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Module Name")]
        public string ModuleName { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        [Display(Name = "Order")]
        public int ModuleOrder { get; set; } = 1;
    }

    public class LessonFormViewModel
    {
        public int LessonId { get; set; }
        public int ModuleId { get; set; }
        public int CourseId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        [Display(Name = "Lesson Type")]
        public string LessonType { get; set; } = "Video"; // Video, PDF, Article, Link, Quiz, Assignment

        [Display(Name = "Video URL / Embed Link")]
        [StringLength(500)]
        public string VideoUrl { get; set; }

        [Display(Name = "Article Content / Notes")]
        [AllowHtml]
        public string Content { get; set; }

        [Display(Name = "Resource File URL / Document")]
        [StringLength(500)]
        public string ResourceUrl { get; set; }

        [Display(Name = "Upload PDF File")]
        public HttpPostedFileBase PdfFile { get; set; }

        [Display(Name = "Duration (Minutes)")]
        public int DurationInMinutes { get; set; } = 10;

        [Display(Name = "Lesson Order")]
        public int LessonOrder { get; set; } = 1;

        [Display(Name = "Is Free Preview?")]
        public bool IsPreview { get; set; } = false;
    }

    public class QuizFormViewModel
    {
        public int QuizId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public int? ModuleId { get; set; }
        public int? LessonId { get; set; }
        public bool IsFinalExam { get; set; } = false;

        public List<System.Web.Mvc.SelectListItem> ModuleList { get; set; } = new List<System.Web.Mvc.SelectListItem>();

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public string Description { get; set; }

        [Display(Name = "Passing Percentage (%)")]
        [Range(1, 100)]
        public decimal PassingPercentage { get; set; } = 40;

        [Display(Name = "Time Limit (Minutes)")]
        public int? TimeLimitMinutes { get; set; } = 30;

        public int TotalMarks { get; set; } = 0;
    }

    public class QuestionOptionInput
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; }
        public bool IsCorrect { get; set; }
        public int OptionOrder { get; set; }
    }

    public class QuestionFormViewModel
    {
        public int QuestionId { get; set; }
        public int QuizId { get; set; }

        [Required]
        [Display(Name = "Question Text")]
        public string QuestionText { get; set; }

        [Range(1, 100)]
        public int Marks { get; set; } = 1;

        public int QuestionOrder { get; set; } = 1;

        public List<QuestionOptionInput> Options { get; set; } = new List<QuestionOptionInput>();
    }

    public class AssignmentFormViewModel
    {
        public int AssignmentId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public int? ModuleId { get; set; }
        public int? LessonId { get; set; }

        public List<System.Web.Mvc.SelectListItem> ModuleList { get; set; } = new List<System.Web.Mvc.SelectListItem>();

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Display(Name = "Instructions / Description")]
        public string Description { get; set; }

        [Display(Name = "Due Date")]
        public DateTime? DueDate { get; set; }

        [Display(Name = "Maximum Marks")]
        [Range(1, 1000)]
        public int MaxMarks { get; set; } = 100;
    }

    public class GradeSubmissionViewModel
    {
        public int SubmissionId { get; set; }
        public int AssignmentId { get; set; }
        public string AssignmentTitle { get; set; }
        public string CourseTitle { get; set; }
        public string StudentName { get; set; }
        public string SubmissionText { get; set; }
        public string FileUrl { get; set; }
        public DateTime SubmittedDate { get; set; }

        [Required]
        [Range(0, 1000)]
        [Display(Name = "Marks Awarded")]
        public decimal Marks { get; set; }

        public int MaxMarks { get; set; }

        [Display(Name = "Feedback")]
        public string Feedback { get; set; }

        public string Status { get; set; }
    }

    public class InstructorShowcaseViewModel
    {
        public ApplicationUser Instructor { get; set; }
        public int CourseCount { get; set; }
        public int TotalStudents { get; set; }
        public double AverageRating { get; set; }
        public List<Course> Courses { get; set; } = new List<Course>();
    }
}
