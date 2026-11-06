using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models.AI
{
    [Table("AI_Documents")]
    public class AiDocument
    {
        [Key]
        public int DocumentId { get; set; }

        public int CourseId { get; set; }

        public int? ModuleId { get; set; }

        public int? LessonId { get; set; }

        [Required]
        [StringLength(255)]
        public string Title { get; set; }

        [Required]
        [StringLength(255)]
        public string FileName { get; set; }

        [Required]
        [StringLength(500)]
        public string FilePath { get; set; }

        [Required]
        [StringLength(50)]
        public string FileType { get; set; } // PDF, DOCX, PPTX, TXT

        public int FileSize { get; set; }

        public int TotalPages { get; set; } = 1;

        public int TotalChunks { get; set; } = 0;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Uploaded"; // Uploaded, Processing, Indexed, Failed

        public string ErrorMessage { get; set; }

        [Required]
        [StringLength(50)]
        public string Visibility { get; set; } = "student"; // student, instructor_only, admin

        [Required]
        [StringLength(128)]
        public string UploadedBy { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

        public DateTime? IndexedAt { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [ForeignKey("ModuleId")]
        public virtual Module Module { get; set; }

        [ForeignKey("LessonId")]
        public virtual Lesson Lesson { get; set; }

        public virtual ICollection<AiDocumentChunk> Chunks { get; set; } = new List<AiDocumentChunk>();
    }
}
