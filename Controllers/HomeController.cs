using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using LMS.Data;

namespace LMS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();

        public ActionResult Index()
        {
            var featuredCourses = _db.Courses
                .Include(c => c.Category)
                .Include(c => c.Instructor)
                .Where(c => c.Status == "Published")
                .OrderByDescending(c => c.Rating)
                .ThenByDescending(c => c.TotalStudents)
                .Take(6)
                .ToList();

            var categories = _db.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .Take(8)
                .ToList();

            ViewBag.TotalCourses = _db.Courses.Count(c => c.Status == "Published");
            ViewBag.TotalStudents = _db.Enrollments.Select(e => e.StudentId).Distinct().Count();
            ViewBag.TotalCertificates = _db.Certificates.Count();
            ViewBag.Categories = categories;

            return View(featuredCourses);
        }

        public ActionResult Instructors()
        {
            var instructorRoleId = _db.Roles.FirstOrDefault(r => r.Name == "Instructor")?.Id;
            var instructors = _db.Users
                .Where(u => u.Roles.Any(r => r.RoleId == instructorRoleId))
                .ToList();

            var instructorData = instructors.Select(ins => new LMS.ViewModels.InstructorShowcaseViewModel
            {
                Instructor = ins,
                CourseCount = _db.Courses.Count(c => c.InstructorId == ins.Id && c.Status == "Published"),
                TotalStudents = _db.Courses.Where(c => c.InstructorId == ins.Id && c.Status == "Published").Sum(c => (int?)c.TotalStudents) ?? 0,
                AverageRating = _db.Courses.Where(c => c.InstructorId == ins.Id && c.Status == "Published").Average(c => (double?)c.Rating) ?? 5.0,
                Courses = _db.Courses.Where(c => c.InstructorId == ins.Id && c.Status == "Published").Take(3).ToList()
            }).OrderByDescending(i => i.CourseCount).ThenByDescending(i => i.TotalStudents).ToList();

            return View(instructorData);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Empowering learners worldwide through modern interactive education.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Get in touch with our admissions and support team.";
            return View();
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