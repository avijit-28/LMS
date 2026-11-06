using System.Linq;
using System.Web.Mvc;
using LMS.Data;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNet.Identity;

namespace LMS.Controllers
{
    [Authorize]
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly QuizService _quizService;

        public QuizController()
        {
            _quizService = new QuizService(_db);
        }

        // GET: Quiz/Take/5
        public ActionResult Take(int id)
        {
            string studentId = User.Identity.GetUserId();

            var quiz = _db.Quizzes.Find(id);
            if (quiz == null || !quiz.IsActive)
            {
                TempData["ErrorMessage"] = "Quiz not found or is currently inactive.";
                return RedirectToAction("Index", "Student");
            }

            bool isEnrolled = _db.Enrollments.Any(e => e.StudentId == studentId && e.CourseId == quiz.CourseId && e.Status != "Cancelled");
            var course = _db.Courses.Find(quiz.CourseId);
            bool isInstructor = course != null && course.InstructorId == studentId;
            bool isAdmin = User.IsInRole("Admin");

            if (!isEnrolled && !isInstructor && !isAdmin)
            {
                TempData["ErrorMessage"] = "You must be enrolled in this course to take the assessment test.";
                return RedirectToAction("Details", "Courses", new { id = quiz.CourseId });
            }

            // If this is the Final Exam, enforce that 100% of curriculum modules are complete
            if (quiz.IsFinalExam && !isInstructor && !isAdmin)
            {
                var enrollment = _db.Enrollments.FirstOrDefault(e => e.StudentId == studentId && e.CourseId == quiz.CourseId && e.Status != "Cancelled");
                if (enrollment == null || enrollment.CompletionPercentage < 100)
                {
                    TempData["ErrorMessage"] = "The Final Mandatory Certification Exam is LOCKED! You must complete 100% of all curriculum modules (lessons, tasks, and quizzes) before attempting this final exam.";
                    return RedirectToAction("Index", "Learning", new { courseId = quiz.CourseId });
                }
            }

            var attempt = _quizService.StartAttempt(id, studentId);
            if (attempt == null)
            {
                TempData["ErrorMessage"] = "Unable to start quiz attempt.";
                return RedirectToAction("Details", "Courses", new { id = quiz.CourseId });
            }

            var model = _quizService.GetQuizAttemptViewModel(attempt.AttemptId, studentId);
            return View(model);
        }

        // POST: Quiz/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(QuizSubmitViewModel model)
        {
            string studentId = User.Identity.GetUserId();
            var result = _quizService.SubmitQuiz(model.AttemptId, model.Answers, studentId);
            if (result == null)
            {
                return HttpNotFound();
            }

            return RedirectToAction("Result", new { attemptId = model.AttemptId });
        }

        // GET: Quiz/Result/5
        public ActionResult Result(int attemptId)
        {
            string studentId = User.Identity.GetUserId();
            var attempt = _db.QuizAttempts.FirstOrDefault(a => a.AttemptId == attemptId && a.StudentId == studentId);
            if (attempt == null) return HttpNotFound();

            var result = new QuizResultViewModel
            {
                AttemptId = attempt.AttemptId,
                QuizTitle = attempt.Quiz.Title,
                CourseTitle = attempt.Quiz.Course?.Title,
                CourseId = attempt.Quiz.CourseId,
                Score = attempt.Score,
                TotalMarks = attempt.TotalMarks,
                ObtainedMarks = attempt.ObtainedMarks,
                IsPassed = attempt.IsPassed,
                CompletedAt = attempt.CompletedAt
            };

            return View(result);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
