using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LMS.Models
{
    [Table("QuestionOptions")]
    public class QuestionOption
    {
        [Key]
        public int OptionId { get; set; }

        public int QuestionId { get; set; }

        [Required]
        [StringLength(1000)]
        public string OptionText { get; set; }

        public bool IsCorrect { get; set; } = false;

        public int OptionOrder { get; set; } = 1;

        [ForeignKey("QuestionId")]
        public virtual Question Question { get; set; }
    }
}
