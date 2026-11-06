using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("Certificates")]
    public class Certificate
    {
        [Key]
        public int CertificateId { get; set; }

        [Required]
        [StringLength(100)]
        public string CertificateNumber { get; set; }

        [Required]
        [StringLength(450)]
        public string StudentId { get; set; }

        public int CourseId { get; set; }

        public DateTime IssueDate { get; set; } = DateTime.UtcNow;

        public decimal? FinalScore { get; set; }

        [StringLength(500)]
        public string CertificateUrl { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }
    }
}
