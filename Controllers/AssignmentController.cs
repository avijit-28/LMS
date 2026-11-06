using System.Linq;
using System.Web.Mvc;
using LMS.Data;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNet.Identity;

namespace LMS.Controllers
{
    [Authorize]
    public class AssignmentController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly AssignmentService _assignmentService;

        public AssignmentController()
        {
            _assignmentService = new AssignmentService(_db);
        }

        // GET: Assignment/Details/5
        public ActionResult Details(int id)
        {
            string studentId = User.Identity.GetUserId();
            var assignment = _db.Assignments.Find(id);
            if (assignment == null) return HttpNotFound();

            var existingSubmission = _db.Submissions.FirstOrDefault(s => s.AssignmentId == id && s.StudentId == studentId);

            var model = new AssignmentSubmissionViewModel
            {
                Assignment = assignment,
                ExistingSubmission = existingSubmission,
                SubmissionText = existingSubmission?.SubmissionText,
                FileUrl = existingSubmission?.FileUrl
            };

            return View(model);
        }

        // POST: Assignment/Submit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Submit(AssignmentSubmissionViewModel model)
        {
            string studentId = User.Identity.GetUserId();
            int assignmentId = model.AssignmentId > 0 ? model.AssignmentId : (model.Assignment != null ? model.Assignment.AssignmentId : 0);
            if (assignmentId <= 0)
            {
                return HttpNotFound();
            }

            bool success = _assignmentService.SubmitAssignment(
                assignmentId,
                studentId,
                model.SubmissionText,
                model.FileUrl,
                out string errorMessage
            );

            if (!success)
            {
                TempData["ErrorMessage"] = errorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = "Assignment submitted successfully!";
            }

            return RedirectToAction("Details", new { id = assignmentId });
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
