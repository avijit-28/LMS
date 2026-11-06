using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("Quizzes")]
    public class Quiz
    {
        [Key]
        public int QuizId { get; set; }

        public int CourseId { get; set; }

        public int? ModuleId { get; set; }

        public int? LessonId { get; set; }

        public bool IsFinalExam { get; set; } = false;

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public string Description { get; set; }

        public decimal PassingPercentage { get; set; } = 40;

        public int? TimeLimitMinutes { get; set; }

        public int TotalMarks { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [ForeignKey("ModuleId")]
        public virtual Module Module { get; set; }

        [ForeignKey("LessonId")]
        public virtual Lesson Lesson { get; set; }

        public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
        public virtual ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
    }
}
