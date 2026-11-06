using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models.AI
{
    [Table("AI_ChatMessages")]
    public class AiChatMessage
    {
        [Key]
        public int MessageId { get; set; }

        public int SessionId { get; set; }

        [Required]
        [StringLength(50)]
        public string SenderRole { get; set; } // User, Assistant, System

        [Required]
        public string MessageText { get; set; }

        public bool IsGroundedInRag { get; set; } = false;

        [StringLength(50)]
        public string LlmProvider { get; set; } // gemini, groq, local

        [StringLength(100)]
        public string ModelUsed { get; set; }

        public int PromptTokens { get; set; } = 0;

        public int CompletionTokens { get; set; } = 0;

        public int TotalTokens { get; set; } = 0;

        public int LatencyMs { get; set; } = 0;

        public bool WasCacheHit { get; set; } = false;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SessionId")]
        public virtual AiChatSession Session { get; set; }

        public virtual ICollection<AiChatSource> Sources { get; set; } = new List<AiChatSource>();

        public virtual ICollection<AiFeedback> Feedback { get; set; } = new List<AiFeedback>();
    }
}
