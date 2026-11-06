using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("Questions")]
    public class Question
    {
        [Key]
        public int QuestionId { get; set; }

        public int QuizId { get; set; }

        [Required]
        public string QuestionText { get; set; }

        public int Marks { get; set; } = 1;

        public int QuestionOrder { get; set; } = 1;

        [ForeignKey("QuizId")]
        public virtual Quiz Quiz { get; set; }

        public virtual ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
    }
}
