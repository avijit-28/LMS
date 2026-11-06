using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("Assignments")]
    public class Assignment
    {
        [Key]
        public int AssignmentId { get; set; }

        public int CourseId { get; set; }

        public int? ModuleId { get; set; }

        public int? LessonId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public string Description { get; set; }

        public DateTime? DueDate { get; set; }

        public int MaxMarks { get; set; } = 100;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        [ForeignKey("ModuleId")]
        public virtual Module Module { get; set; }

        [ForeignKey("LessonId")]
        public virtual Lesson Lesson { get; set; }

        public virtual ICollection<Submission> Submissions { get; set; } = new List<Submission>();
    }
}
