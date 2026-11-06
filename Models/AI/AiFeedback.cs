using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models.AI
{
    [Table("AI_Feedback")]
    public class AiFeedback
    {
        [Key]
        public int FeedbackId { get; set; }

        public int MessageId { get; set; }

        [Required]
        [StringLength(128)]
        public string UserId { get; set; }

        public bool IsHelpful { get; set; }

        [StringLength(1000)]
        public string FeedbackText { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("MessageId")]
        public virtual AiChatMessage Message { get; set; }

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }
    }
}
