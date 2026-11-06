using System.Data.Entity;
using LMS.Models;
using LMS.Models.AI;
using Microsoft.AspNet.Identity.EntityFramework;

namespace LMS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        private static string GetActiveConnectionString()
        {
            string envConn = LMS.Services.EnvConfig.DatabaseConnectionString;
            return !string.IsNullOrWhiteSpace(envConn) ? envConn : "DefaultConnection";
        }

        public ApplicationDbContext() : base(GetActiveConnectionString(), throwIfV1Schema: false)
        {
            Database.SetInitializer<ApplicationDbContext>(null);
        }

        public ApplicationDbContext(string connectionString) : base(connectionString, throwIfV1Schema: false)
        {
            Database.SetInitializer<ApplicationDbContext>(null);
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Course> Courses { get; set; }
        public DbSet<Module> Modules { get; set; }
        public DbSet<Lesson> Lessons { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<LessonProgress> LessonProgresses { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<QuestionOption> QuestionOptions { get; set; }
        public DbSet<QuizAttempt> QuizAttempts { get; set; }
        public DbSet<QuizAnswer> QuizAnswers { get; set; }
        public DbSet<Assignment> Assignments { get; set; }
        public DbSet<Submission> Submissions { get; set; }
        public DbSet<Certificate> Certificates { get; set; }
        public DbSet<CourseReview> CourseReviews { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<Announcement> Announcements { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<PasswordResetRequest> PasswordResetRequests { get; set; }
        public DbSet<KnowledgeChunk> KnowledgeChunks { get; set; }
        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        // Enterprise RAG AI Assistant DbSets
        public DbSet<AiDocument> AiDocuments { get; set; }
        public DbSet<AiDocumentChunk> AiDocumentChunks { get; set; }
        public DbSet<AiChatSession> AiChatSessions { get; set; }
        public DbSet<AiChatMessage> AiChatMessages { get; set; }
        public DbSet<AiChatSource> AiChatSources { get; set; }
        public DbSet<AiUsageLog> AiUsageLogs { get; set; }
        public DbSet<AiFeedback> AiFeedbacks { get; set; }
        public DbSet<AiSemanticCache> AiSemanticCaches { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure user approval properties
            modelBuilder.Entity<ApplicationUser>().Property(u => u.ApprovalStatus).HasMaxLength(50);
            modelBuilder.Entity<ApplicationUser>().Property(u => u.RequestedRole).HasMaxLength(50);
            modelBuilder.Entity<ApplicationUser>().Property(u => u.RejectionReason).HasMaxLength(500);

            // Configure decimal precisions
            modelBuilder.Entity<Course>().Property(c => c.Price).HasPrecision(18, 2);
            modelBuilder.Entity<Course>().Property(c => c.DurationInHours).HasPrecision(5, 2);
            modelBuilder.Entity<Course>().Property(c => c.Rating).HasPrecision(3, 2);
            modelBuilder.Entity<Enrollment>().Property(e => e.CompletionPercentage).HasPrecision(5, 2);
            modelBuilder.Entity<Quiz>().Property(q => q.PassingPercentage).HasPrecision(5, 2);
            modelBuilder.Entity<QuizAttempt>().Property(qa => qa.Score).HasPrecision(5, 2);
            modelBuilder.Entity<Submission>().Property(s => s.Marks).HasPrecision(6, 2);
            modelBuilder.Entity<Certificate>().Property(c => c.FinalScore).HasPrecision(5, 2);
            modelBuilder.Entity<Payment>().Property(p => p.Amount).HasPrecision(18, 2);
            modelBuilder.Entity<Payment>().Property(p => p.AdminCommission).HasPrecision(18, 2);
            modelBuilder.Entity<Payment>().Property(p => p.InstructorEarnings).HasPrecision(18, 2);

            // Optional 1-to-many relationships without cascade delete
            modelBuilder.Entity<Quiz>()
                .HasOptional(q => q.Lesson)
                .WithMany()
                .HasForeignKey(q => q.LessonId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Quiz>()
                .HasOptional(q => q.Module)
                .WithMany(m => m.Quizzes)
                .HasForeignKey(q => q.ModuleId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Assignment>()
                .HasOptional(a => a.Lesson)
                .WithMany()
                .HasForeignKey(a => a.LessonId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Assignment>()
                .HasOptional(a => a.Module)
                .WithMany(m => m.Assignments)
                .HasForeignKey(a => a.ModuleId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Payment>()
                .HasOptional(p => p.Enrollment)
                .WithMany()
                .HasForeignKey(p => p.EnrollmentId)
                .WillCascadeOnDelete(false);
        }
    }
}
