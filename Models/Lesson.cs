using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("Lessons")]
    public class Lesson
    {
        [Key]
        public int LessonId { get; set; }

        public int ModuleId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public string Description { get; set; }

        [Required]
        [StringLength(30)]
        public string LessonType { get; set; } // Video, PDF, Article, Link, Quiz, Assignment

        [StringLength(500)]
        public string VideoUrl { get; set; }

        public string Content { get; set; }

        [StringLength(500)]
        public string ResourceUrl { get; set; }

        public int DurationInMinutes { get; set; } = 0;

        public int LessonOrder { get; set; } = 1;

        public bool IsPreview { get; set; } = false;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedDate { get; set; }

        [ForeignKey("ModuleId")]
        public virtual Module Module { get; set; }

        public virtual ICollection<LessonProgress> LessonProgresses { get; set; } = new List<LessonProgress>();
    }
}
