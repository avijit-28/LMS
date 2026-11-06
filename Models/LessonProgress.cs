using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("LessonProgress")]
    public class LessonProgress
    {
        [Key]
        public int ProgressId { get; set; }

        [Required]
        [StringLength(450)]
        public string StudentId { get; set; }

        public int LessonId { get; set; }

        public bool IsCompleted { get; set; } = false;

        public int WatchTimeInSeconds { get; set; } = 0;

        public DateTime? CompletedDate { get; set; }

        public DateTime LastAccessedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("LessonId")]
        public virtual Lesson Lesson { get; set; }
    }
}
