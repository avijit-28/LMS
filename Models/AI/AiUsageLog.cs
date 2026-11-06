using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models.AI
{
    [Table("AI_UsageLogs")]
    public class AiUsageLog
    {
        [Key]
        public int LogId { get; set; }

        [Required]
        [StringLength(128)]
        public string UserId { get; set; }

        public int? CourseId { get; set; }

        [Required]
        [StringLength(50)]
        public string QueryType { get; set; } // RAG, QuizGen, Summarize, CodeExplain

        [Required]
        [StringLength(50)]
        public string LlmProvider { get; set; }

        [Required]
        [StringLength(100)]
        public string ModelName { get; set; }

        public int PromptTokens { get; set; }

        public int CompletionTokens { get; set; }

        public int TotalTokens { get; set; }

        [Column(TypeName = "decimal")]
        public decimal EstimatedCost { get; set; } = 0.0m;

        public int LatencyMs { get; set; }

        public bool WasCacheHit { get; set; } = false;

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public virtual ApplicationUser User { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }
    }
}
