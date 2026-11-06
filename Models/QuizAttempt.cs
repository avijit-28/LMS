using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("QuizAttempts")]
    public class QuizAttempt
    {
        [Key]
        public int AttemptId { get; set; }

        public int QuizId { get; set; }

        [Required]
        [StringLength(450)]
        public string StudentId { get; set; }

        public decimal Score { get; set; } = 0;

        public int TotalMarks { get; set; } = 0;

        public int ObtainedMarks { get; set; } = 0;

        public bool IsPassed { get; set; } = false;

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        [ForeignKey("QuizId")]
        public virtual Quiz Quiz { get; set; }

        public virtual ICollection<QuizAnswer> QuizAnswers { get; set; } = new List<QuizAnswer>();
    }
}
