using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("Courses")]
    public class Course
    {
        [Key]
        public int CourseId { get; set; }

        public int CategoryId { get; set; }

        [Required]
        [StringLength(450)]
        public string InstructorId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        public string Description { get; set; }

        [StringLength(500)]
        public string Thumbnail { get; set; }

        public decimal Price { get; set; } = 0;

        [Required]
        [StringLength(50)]
        public string Level { get; set; } = "Beginner";

        public decimal DurationInHours { get; set; } = 0;

        public int ValidityInDays { get; set; } = 30; // Access validity period in days (set by instructor)

        [Required]
        [StringLength(30)]
        public string Status { get; set; } = "Draft";

        public decimal Rating { get; set; } = 0;

        public int TotalStudents { get; set; } = 0;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedDate { get; set; }

        public DateTime? PublishedDate { get; set; }

        [ForeignKey("CategoryId")]
        public virtual Category Category { get; set; }

        [ForeignKey("InstructorId")]
        public virtual ApplicationUser Instructor { get; set; }

        public virtual ICollection<Module> Modules { get; set; } = new List<Module>();
        public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public virtual ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
        public virtual ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
        public virtual ICollection<CourseReview> CourseReviews { get; set; } = new List<CourseReview>();
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
        public virtual ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
    }
}
