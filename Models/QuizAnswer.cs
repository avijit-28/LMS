using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("QuizAnswers")]
    public class QuizAnswer
    {
        [Key]
        public int AnswerId { get; set; }

        public int AttemptId { get; set; }

        public int QuestionId { get; set; }

        public int? SelectedOptionId { get; set; }

        public bool IsCorrect { get; set; } = false;

        public int MarksObtained { get; set; } = 0;

        [ForeignKey("AttemptId")]
        public virtual QuizAttempt QuizAttempt { get; set; }

        [ForeignKey("QuestionId")]
        public virtual Question Question { get; set; }

        [ForeignKey("SelectedOptionId")]
        public virtual QuestionOption SelectedOption { get; set; }
    }
}
