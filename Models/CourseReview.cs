using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("CourseReviews")]
    public class CourseReview
    {
        [Key]
        public int ReviewId { get; set; }

        public int CourseId { get; set; }

        [Required]
        [StringLength(450)]
        public string StudentId { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(2000)]
        public string ReviewText { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedDate { get; set; }

        [ForeignKey("CourseId")]
        public virtual Course Course { get; set; }
    }
}
