using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models.AI
{
    [Table("AI_SemanticCache")]
    public class AiSemanticCache
    {
        [Key]
        public int CacheId { get; set; }

        public int? CourseId { get; set; }

        [Required]
        [StringLength(64)]
        public string QueryHash { get; set; }

        [Required]
        [StringLength(1000)]
        public string NormalizedQuery { get; set; }

        [Required]
        public string CachedAnswer { get; set; }

        public string SourcesJson { get; set; }

        public int HitCount { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }
    }
}
