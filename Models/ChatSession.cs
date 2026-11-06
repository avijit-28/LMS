using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("ChatSessions")]
    public class ChatSession
    {
        [Key]
        public int SessionId { get; set; }

        [StringLength(128)]
        public string UserId { get; set; }

        [Required]
        [StringLength(50)]
        [Index(IsUnique = true)]
        public string SessionGuid { get; set; }

        [Required]
        [StringLength(150)]
        public string Title { get; set; } = "Academic Consultation";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }
}
