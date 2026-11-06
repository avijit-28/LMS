using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("Enrollments")]
    public class Enrollment
    {
        [Key]
        public int EnrollmentId { get; set; }

        [Required]
        [StringLength(450)]
        public string StudentId { get; set; }

        public int CourseId { get; set; }

        public DateTime EnrollmentDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Active"; // Active, Completed, Cancelled

        public decimal CompletionPercentage { get; set; } = 0;

        public DateTime? CompletionDate { get; set; }

        public DateTime? ExpiryDate { get; set; } // Course validity end date

        [NotMapped]
        public bool IsExpired => Status != "Completed" && ExpiryDate.HasValue && DateTime.UtcNow > ExpiryDate.Value;

        [NotMapped]
        public int DaysRemaining => ExpiryDate.HasValue ? Math.Max(0, (int)Math.Ceiling((ExpiryDate.Value - DateTime.UtcNow).TotalDays)) : 0;

        [NotMapped]
        public decimal ProgressPercentage
        {
            get => CompletionPercentage;
            set => CompletionPercentage = value;
        }

        [NotMapped]
        public DateTime? CompletedDate
        {
            get => CompletionDate;
            set => CompletionDate = value;
        }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }
    }
}
