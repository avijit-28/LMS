using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("ChatMessages")]
    public class ChatMessage
    {
        [Key]
        public int MessageId { get; set; }

        public int SessionId { get; set; }

        [Required]
        [StringLength(20)]
        public string Sender { get; set; } // User, Assistant

        [Required]
        public string Content { get; set; }

        [StringLength(500)]
        public string RetrievedChunkIds { get; set; }

        public string ActionPayloadJson { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ForeignKey("SessionId")]
        public virtual ChatSession Session { get; set; }
    }
}
