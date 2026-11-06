using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("Modules")]
    public class Module
    {
        [Key]
        public int ModuleId { get; set; }

        public int CourseId { get; set; }

        [Required]
        [StringLength(200)]
        public string ModuleName { get; set; }

        [StringLength(1000)]
        public string Description { get; set; }

        public int ModuleOrder { get; set; } = 1;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }

        public virtual ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
        public virtual ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
        public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
    }
}
