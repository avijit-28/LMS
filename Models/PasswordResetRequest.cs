using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("PasswordResetRequests")]
    public class PasswordResetRequest
    {
        [Key]
        public int RequestId { get; set; }

        [Required]
        [StringLength(128)]
        public string UserId { get; set; }

        [Required]
        [StringLength(256)]
        public string Email { get; set; }

        [StringLength(50)]
        public string Role { get; set; }

        [StringLength(500)]
        public string RequestReason { get; set; }

        public DateTime RequestDate { get; set; } = DateTime.UtcNow;

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Completed

        public bool IsApproved { get; set; } = false;

        [StringLength(100)]
        public string ResetToken { get; set; }

        public DateTime? TokenExpiry { get; set; }

        [StringLength(128)]
        public string ApprovedByAdminId { get; set; }

        public DateTime? ApprovedDate { get; set; }

        [StringLength(500)]
        public string AdminNotes { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
}
