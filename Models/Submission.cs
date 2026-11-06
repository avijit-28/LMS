using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("Submissions")]
    public class Submission
    {
        [Key]
        public int SubmissionId { get; set; }

        public int AssignmentId { get; set; }

        [Required]
        [StringLength(450)]
        public string StudentId { get; set; }

        public string SubmissionText { get; set; }

        [StringLength(500)]
        public string FileUrl { get; set; }

        public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

        public decimal? Marks { get; set; }

        public string Feedback { get; set; }

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Submitted"; // Submitted, Graded, Late

        public DateTime? GradedDate { get; set; }

        [ForeignKey("AssignmentId")]
        public virtual Assignment Assignment { get; set; }
    }
}
