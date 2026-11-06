using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models.AI
{
    [Table("AI_DocumentChunks")]
    public class AiDocumentChunk
    {
        [Key]
        public int ChunkId { get; set; }

        public int DocumentId { get; set; }

        public int CourseId { get; set; }

        public int? ModuleId { get; set; }

        public int? LessonId { get; set; }

        [Required]
        [StringLength(64)]
        public string VectorPointId { get; set; } // UUID in Qdrant

        public int ChunkIndex { get; set; }

        public int? PageNumber { get; set; }

        [StringLength(255)]
        public string SectionTitle { get; set; }

        [Required]
        public string ChunkText { get; set; }

        public int TokenCount { get; set; } = 0;

        [Required]
        [StringLength(50)]
        public string Visibility { get; set; } = "student";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("DocumentId")]
        public virtual AiDocument Document { get; set; }
    }
}
