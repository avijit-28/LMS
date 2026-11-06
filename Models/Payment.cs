using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("Payments")]
    public class Payment
    {
        [Key]
        public int PaymentId { get; set; }

        [Required]
        [StringLength(450)]
        public string StudentId { get; set; }

        public int CourseId { get; set; }

        public int? EnrollmentId { get; set; }

        public decimal Amount { get; set; }

        public decimal AdminCommission { get; set; } = 0; // 5% platform fee for admin

        public decimal InstructorEarnings { get; set; } = 0; // 95% net share for instructor

        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; }

        [StringLength(200)]
        public string TransactionId { get; set; }

        [Required]
        [StringLength(30)]
        public string PaymentStatus { get; set; } = "Pending"; // Pending, Completed, Failed, Refunded

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [ForeignKey("EnrollmentId")]
        public virtual Enrollment Enrollment { get; set; }
    }
}
