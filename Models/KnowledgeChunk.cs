using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("KnowledgeChunks")]
    public class KnowledgeChunk
    {
        [Key]
        public int ChunkId { get; set; }

        [Required]
        [StringLength(50)]
        public string SourceType { get; set; } // Course, Module, Lesson, Instructor, FAQ, Policy

        public int SourceId { get; set; }

        [Required]
        [StringLength(250)]
        public string Title { get; set; }

        [Required]
        public string Content { get; set; }

        [StringLength(500)]
        public string Keywords { get; set; }

        [StringLength(250)]
        public string UrlPath { get; set; }

        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
