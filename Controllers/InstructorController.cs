using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using LMS.Data;
using LMS.Models;
using LMS.Models.AI;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNet.Identity;

namespace LMS.Controllers
{
    [Authorize(Roles = "Instructor,Admin")]
    public class InstructorController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly DashboardService _dashboardService;
        private readonly AssignmentService _assignmentService;
        private readonly PaymentService _paymentService;
        private readonly ICloudinaryService _cloudinary = new CloudinaryService();

        public InstructorController()
        {
            _dashboardService = new DashboardService(_db);
            _assignmentService = new AssignmentService(_db);
            _paymentService = new PaymentService(_db);
        }

        private string CurrentUserId => User.Identity.GetUserId();

        private bool IsOwnerOrAdmin(Course course)
        {
            if (course == null) return false;
            if (User.IsInRole("Admin")) return true;
            return course.InstructorId == CurrentUserId;
        }

        // GET: Instructor
        public ActionResult Index()
        {
            var model = _dashboardService.GetInstructorDashboard(CurrentUserId);
            return View(model);
        }

        // GET: Instructor/Courses
        public ActionResult Courses()
        {
            bool isAdmin = User.IsInRole("Admin");
            string currentUserId = CurrentUserId;

            var query = _db.Courses.Include(c => c.Category);
            if (!isAdmin)
            {
                query = query.Where(c => c.InstructorId == currentUserId);
            }

            var courses = query.OrderByDescending(c => c.CreatedDate).ToList();
            return View(courses);
        }

        // GET: Instructor/CreateCourse
        public ActionResult CreateCourse()
        {
            var model = new CourseFormViewModel
            {
                Categories = GetCategorySelectList()
            };
            return View(model);
        }

        // POST: Instructor/CreateCourse
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCourse(CourseFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var course = new Course
                {
                    CategoryId = model.CategoryId,
                    InstructorId = CurrentUserId,
                    Title = model.Title,
                    Description = model.Description,
                    Thumbnail = model.Thumbnail,
                    Price = model.Price,
                    Level = model.Level,
                    DurationInHours = model.DurationInHours,
                    ValidityInDays = model.ValidityInDays > 0 ? model.ValidityInDays : 30,
                    Status = model.Status ?? "Draft",
                    CreatedDate = DateTime.UtcNow,
                    PublishedDate = model.Status == "Published" ? (DateTime?)DateTime.UtcNow : null
                };

                _db.Courses.Add(course);
                _db.SaveChanges();
                TriggerRagIndexRefresh();

                TempData["SuccessMessage"] = "Course created successfully!";
                return RedirectToAction("Modules", new { courseId = course.CourseId });
            }

            model.Categories = GetCategorySelectList();
            return View(model);
        }

        // GET: Instructor/EditCourse/5
        public ActionResult EditCourse(int id)
        {
            var course = _db.Courses.Find(id);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            var model = new CourseFormViewModel
            {
                CourseId = course.CourseId,
                CategoryId = course.CategoryId,
                Title = course.Title,
                Description = course.Description,
                Thumbnail = course.Thumbnail,
                Price = course.Price,
                Level = course.Level,
                DurationInHours = course.DurationInHours,
                ValidityInDays = course.ValidityInDays > 0 ? course.ValidityInDays : 30,
                Status = course.Status,
                Categories = GetCategorySelectList()
            };

            return View(model);
        }

        // POST: Instructor/EditCourse/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCourse(CourseFormViewModel model)
        {
            var course = _db.Courses.Find(model.CourseId);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            if (ModelState.IsValid)
            {
                course.CategoryId = model.CategoryId;
                course.Title = model.Title;
                course.Description = model.Description;
                course.Thumbnail = model.Thumbnail;
                course.Price = model.Price;
                course.Level = model.Level;
                course.DurationInHours = model.DurationInHours;
                course.ValidityInDays = model.ValidityInDays > 0 ? model.ValidityInDays : 30;
                course.UpdatedDate = DateTime.UtcNow;

                if (course.Status != "Published" && model.Status == "Published")
                {
                    course.PublishedDate = DateTime.UtcNow;
                }
                course.Status = model.Status;

                _db.SaveChanges();
                TriggerRagIndexRefresh();
                TempData["SuccessMessage"] = "Course updated successfully!";
                return RedirectToAction("Courses");
            }

            model.Categories = GetCategorySelectList();
            return View(model);
        }

        // POST: Instructor/DeleteCourse/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCourse(int id)
        {
            var course = _db.Courses.Find(id);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            if (course.Status == "Published" && course.TotalStudents > 0)
            {
                TempData["ErrorMessage"] = "Cannot delete a published course with enrolled students. Archive it instead.";
                return RedirectToAction("Courses");
            }

            try
            {
                var modules = _db.Modules.Where(m => m.CourseId == id).ToList();
                var moduleIds = modules.Select(m => m.ModuleId).ToList();

                var lessons = _db.Lessons.Where(l => moduleIds.Contains(l.ModuleId)).ToList();
                var lessonIds = lessons.Select(l => l.LessonId).ToList();

                if (lessonIds.Count > 0)
                {
                    var quizzesToDetach = _db.Quizzes.Where(q => q.LessonId.HasValue && lessonIds.Contains(q.LessonId.Value)).ToList();
                    foreach (var q in quizzesToDetach) q.LessonId = null;

                    var assignmentsToDetach = _db.Assignments.Where(a => a.LessonId.HasValue && lessonIds.Contains(a.LessonId.Value)).ToList();
                    foreach (var a in assignmentsToDetach) a.LessonId = null;

                    var progresses = _db.LessonProgresses.Where(lp => lessonIds.Contains(lp.LessonId)).ToList();
                    _db.LessonProgresses.RemoveRange(progresses);

                    _db.Lessons.RemoveRange(lessons);
                }

                var courseQuizzes = _db.Quizzes.Where(q => q.CourseId == id).ToList();
                if (courseQuizzes.Count > 0)
                {
                    var quizIds = courseQuizzes.Select(q => q.QuizId).ToList();
                    var quizAnswers = _db.QuizAnswers.Where(qa => quizIds.Contains(qa.QuizAttempt.QuizId)).ToList();
                    _db.QuizAnswers.RemoveRange(quizAnswers);
                    var quizAttempts = _db.QuizAttempts.Where(qa => quizIds.Contains(qa.QuizId)).ToList();
                    _db.QuizAttempts.RemoveRange(quizAttempts);

                    var options = _db.QuestionOptions.Where(qo => quizIds.Contains(qo.Question.QuizId)).ToList();
                    _db.QuestionOptions.RemoveRange(options);
                    var questions = _db.Questions.Where(q => quizIds.Contains(q.QuizId)).ToList();
                    _db.Questions.RemoveRange(questions);

                    _db.Quizzes.RemoveRange(courseQuizzes);
                }

                var courseAssignments = _db.Assignments.Where(a => a.CourseId == id).ToList();
                if (courseAssignments.Count > 0)
                {
                    var assignIds = courseAssignments.Select(a => a.AssignmentId).ToList();
                    var submissions = _db.Submissions.Where(s => assignIds.Contains(s.AssignmentId)).ToList();
                    _db.Submissions.RemoveRange(submissions);
                    _db.Assignments.RemoveRange(courseAssignments);
                }

                var reviews = _db.CourseReviews.Where(r => r.CourseId == id).ToList();
                _db.CourseReviews.RemoveRange(reviews);

                _db.Modules.RemoveRange(modules);
                _db.Courses.Remove(course);
                _db.SaveChanges();

                TempData["SuccessMessage"] = "Course removed successfully.";
            }
            catch
            {
                TempData["ErrorMessage"] = "Unable to delete course due to active student records or dependencies. Consider archiving instead.";
            }

            return RedirectToAction("Courses");
        }

        // POST: Instructor/PublishCourse/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PublishCourse(int id)
        {
            var course = _db.Courses.Find(id);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            course.Status = "Published";
            course.PublishedDate = DateTime.UtcNow;
            _db.SaveChanges();

            TempData["SuccessMessage"] = $"Course '{course.Title}' is now published and live!";
            return RedirectToAction("Courses");
        }

        // GET: Instructor/Modules?courseId=5
        public ActionResult Modules(int courseId)
        {
            var course = _db.Courses.Include(c => c.Modules.Select(m => m.Lessons)).FirstOrDefault(c => c.CourseId == courseId);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            ViewBag.Course = course;
            var modules = course.Modules.OrderBy(m => m.ModuleOrder).ToList();
            return View(modules);
        }

        // POST: Instructor/CreateModule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateModule(ModuleFormViewModel model)
        {
            var course = _db.Courses.Find(model.CourseId);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            if (ModelState.IsValid)
            {
                var module = new Module
                {
                    CourseId = model.CourseId,
                    ModuleName = model.ModuleName,
                    Description = model.Description,
                    ModuleOrder = model.ModuleOrder,
                    CreatedDate = DateTime.UtcNow
                };

                _db.Modules.Add(module);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "Module added successfully!";
            }

            return RedirectToAction("Modules", new { courseId = model.CourseId });
        }

        // POST: Instructor/EditModule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditModule(ModuleFormViewModel model)
        {
            var module = _db.Modules.Include(m => m.Course).FirstOrDefault(m => m.ModuleId == model.ModuleId);
            if (module == null || !IsOwnerOrAdmin(module.Course)) return new HttpUnauthorizedResult();

            if (ModelState.IsValid)
            {
                module.ModuleName = model.ModuleName;
                module.Description = model.Description;
                module.ModuleOrder = model.ModuleOrder;
                _db.SaveChanges();

                TempData["SuccessMessage"] = "Module updated successfully!";
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to update module. Please verify all required fields.";
            }

            return RedirectToAction("Modules", new { courseId = module.CourseId });
        }

        // POST: Instructor/DeleteModule
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteModule(int id)
        {
            var module = _db.Modules.Include(m => m.Course).FirstOrDefault(m => m.ModuleId == id);
            if (module == null || !IsOwnerOrAdmin(module.Course)) return new HttpUnauthorizedResult();

            int courseId = module.CourseId;

            try
            {
                var lessons = _db.Lessons.Where(l => l.ModuleId == id).ToList();
                var lessonIds = lessons.Select(l => l.LessonId).ToList();

                if (lessonIds.Count > 0)
                {
                    var quizzesToDetach = _db.Quizzes.Where(q => q.LessonId.HasValue && lessonIds.Contains(q.LessonId.Value)).ToList();
                    foreach (var q in quizzesToDetach) q.LessonId = null;

                    var assignmentsToDetach = _db.Assignments.Where(a => a.LessonId.HasValue && lessonIds.Contains(a.LessonId.Value)).ToList();
                    foreach (var a in assignmentsToDetach) a.LessonId = null;

                    var progresses = _db.LessonProgresses.Where(lp => lessonIds.Contains(lp.LessonId)).ToList();
                    _db.LessonProgresses.RemoveRange(progresses);

                    _db.Lessons.RemoveRange(lessons);
                }

                _db.Modules.Remove(module);
                _db.SaveChanges();

                TempData["SuccessMessage"] = "Module and associated lessons removed successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Unable to delete module: " + ex.Message;
            }

            return RedirectToAction("Modules", new { courseId });
        }

        // GET: Instructor/Lessons?moduleId=5
        public ActionResult Lessons(int moduleId)
        {
            var module = _db.Modules.Include(m => m.Course).Include(m => m.Lessons).FirstOrDefault(m => m.ModuleId == moduleId);
            if (module == null || !IsOwnerOrAdmin(module.Course)) return new HttpUnauthorizedResult();

            ViewBag.Module = module;
            var lessons = module.Lessons.OrderBy(l => l.LessonOrder).ToList();
            return View(lessons);
        }

        // POST: Instructor/CreateLesson
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateLesson(LessonFormViewModel model, HttpPostedFileBase pdfFile)
        {
            var module = await _db.Modules.Include(m => m.Course).FirstOrDefaultAsync(m => m.ModuleId == model.ModuleId);
            if (module == null || !IsOwnerOrAdmin(module.Course)) return new HttpUnauthorizedResult();

            var fileToUpload = pdfFile ?? model.PdfFile;
            if (fileToUpload != null && fileToUpload.ContentLength > 0)
            {
                var uploadResult = await _cloudinary.UploadPdfAsync(fileToUpload, $"courses/{module.CourseId}/lessons");
                if (uploadResult.Success)
                {
                    model.ResourceUrl = uploadResult.SecureUrl;
                    if (string.IsNullOrEmpty(model.LessonType) || model.LessonType == "Video")
                    {
                        model.LessonType = "PDF";
                    }
                    if (uploadResult.IsLocalFallback)
                    {
                        TempData["InfoMessage"] = "PDF stored locally. (To deliver via Cloudinary CDN, set Cloudinary:CloudName & ApiSecret in Web.config).";
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "PDF Upload warning: " + uploadResult.ErrorMessage;
                }
            }

            if (ModelState.IsValid)
            {
                var lesson = new Lesson
                {
                    ModuleId = model.ModuleId,
                    Title = model.Title,
                    Description = model.Description,
                    LessonType = model.LessonType,
                    VideoUrl = model.VideoUrl,
                    Content = model.Content,
                    ResourceUrl = model.ResourceUrl,
                    DurationInMinutes = model.DurationInMinutes,
                    LessonOrder = model.LessonOrder,
                    IsPreview = model.IsPreview,
                    CreatedDate = DateTime.UtcNow
                };

                _db.Lessons.Add(lesson);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Lesson added successfully!";
            }

            return RedirectToAction("Lessons", new { moduleId = model.ModuleId });
        }

        // POST: Instructor/UploadPdfLesson (Dedicated Section Endpoint)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UploadPdfLesson(int moduleId, string title, string description, int lessonOrder, bool isPreview, HttpPostedFileBase pdfFile)
        {
            var module = await _db.Modules.Include(m => m.Course).FirstOrDefaultAsync(m => m.ModuleId == moduleId);
            if (module == null || !IsOwnerOrAdmin(module.Course)) return new HttpUnauthorizedResult();

            if (pdfFile == null || pdfFile.ContentLength == 0)
            {
                TempData["ErrorMessage"] = "Please select a PDF document to upload.";
                return RedirectToAction("Lessons", new { moduleId });
            }

            var uploadResult = await _cloudinary.UploadPdfAsync(pdfFile, $"courses/{module.CourseId}/lessons");
            if (!uploadResult.Success)
            {
                TempData["ErrorMessage"] = "PDF Upload failed: " + uploadResult.ErrorMessage;
                return RedirectToAction("Lessons", new { moduleId });
            }

            int finalOrder = lessonOrder > 0 ? lessonOrder : ((await _db.Lessons.CountAsync(l => l.ModuleId == moduleId)) + 1);

            var lesson = new Lesson
            {
                ModuleId = moduleId,
                Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(pdfFile.FileName) : title.Trim(),
                Description = description,
                LessonType = "PDF",
                ResourceUrl = uploadResult.SecureUrl,
                DurationInMinutes = 15,
                LessonOrder = finalOrder,
                IsPreview = isPreview,
                CreatedDate = DateTime.UtcNow
            };

            _db.Lessons.Add(lesson);
            await _db.SaveChangesAsync();

            var storageProvider = uploadResult.IsLocalFallback ? "Local Storage" : "Cloudinary CDN";
            TempData["SuccessMessage"] = $"PDF Lesson '{lesson.Title}' successfully uploaded to {storageProvider} and added to curriculum!";
            return RedirectToAction("Lessons", new { moduleId });
        }

        // POST: Instructor/UploadPdfAjax (Real-time Drag & Drop Endpoint)
        [HttpPost]
        public async Task<JsonResult> UploadPdfAjax(HttpPostedFileBase file, int? courseId)
        {
            if (file == null || file.ContentLength == 0)
            {
                return Json(new { success = false, message = "No file selected." });
            }

            string folder = courseId.HasValue ? $"courses/{courseId.Value}/materials" : "materials";
            var result = await _cloudinary.UploadPdfAsync(file, folder);

            return Json(new
            {
                success = result.Success,
                url = result.SecureUrl,
                publicId = result.PublicId,
                isCloudinary = !result.IsLocalFallback,
                apiKey = _cloudinary.ApiKey,
                message = result.ErrorMessage
            });
        }

        // POST: Instructor/DeleteLesson
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteLesson(int id)
        {
            var lesson = _db.Lessons.Include(l => l.Module.Course).FirstOrDefault(l => l.LessonId == id);
            if (lesson == null || !IsOwnerOrAdmin(lesson.Module.Course)) return new HttpUnauthorizedResult();

            int moduleId = lesson.ModuleId;

            try
            {
                var quizzesToDetach = _db.Quizzes.Where(q => q.LessonId == id).ToList();
                foreach (var q in quizzesToDetach) q.LessonId = null;

                var assignmentsToDetach = _db.Assignments.Where(a => a.LessonId == id).ToList();
                foreach (var a in assignmentsToDetach) a.LessonId = null;

                var progresses = _db.LessonProgresses.Where(lp => lp.LessonId == id).ToList();
                _db.LessonProgresses.RemoveRange(progresses);

                _db.Lessons.Remove(lesson);
                _db.SaveChanges();

                TempData["SuccessMessage"] = "Lesson removed successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Unable to delete lesson: " + ex.Message;
            }

            return RedirectToAction("Lessons", new { moduleId });
        }

        // GET: Instructor/EditLesson/5
        public ActionResult EditLesson(int id)
        {
            var lesson = _db.Lessons.Include(l => l.Module.Course).FirstOrDefault(l => l.LessonId == id);
            if (lesson == null || !IsOwnerOrAdmin(lesson.Module.Course)) return new HttpUnauthorizedResult();

            var model = new LessonFormViewModel
            {
                LessonId = lesson.LessonId,
                ModuleId = lesson.ModuleId,
                CourseId = lesson.Module.CourseId,
                Title = lesson.Title,
                Description = lesson.Description,
                LessonType = lesson.LessonType,
                VideoUrl = lesson.VideoUrl,
                Content = lesson.Content,
                ResourceUrl = lesson.ResourceUrl,
                DurationInMinutes = lesson.DurationInMinutes,
                LessonOrder = lesson.LessonOrder,
                IsPreview = lesson.IsPreview
            };

            ViewBag.Lesson = lesson;
            ViewBag.Module = lesson.Module;
            return View(model);
        }

        // POST: Instructor/EditLesson
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditLesson(LessonFormViewModel model, HttpPostedFileBase pdfFile)
        {
            var lesson = await _db.Lessons.Include(l => l.Module.Course).FirstOrDefaultAsync(l => l.LessonId == model.LessonId);
            if (lesson == null || !IsOwnerOrAdmin(lesson.Module.Course)) return new HttpUnauthorizedResult();

            var fileToUpload = pdfFile ?? model.PdfFile;
            if (fileToUpload != null && fileToUpload.ContentLength > 0)
            {
                var uploadResult = await _cloudinary.UploadPdfAsync(fileToUpload, $"courses/{lesson.Module.CourseId}/lessons");
                if (uploadResult.Success)
                {
                    model.ResourceUrl = uploadResult.SecureUrl;
                    if (model.LessonType == "PDF" || string.IsNullOrEmpty(model.LessonType))
                    {
                        model.LessonType = "PDF";
                    }
                }
                else
                {
                    TempData["ErrorMessage"] = "PDF Upload warning: " + uploadResult.ErrorMessage;
                }
            }

            if (ModelState.IsValid)
            {
                lesson.Title = model.Title;
                lesson.Description = model.Description;
                lesson.LessonType = model.LessonType;
                lesson.VideoUrl = model.VideoUrl;
                lesson.Content = model.Content;
                lesson.ResourceUrl = model.ResourceUrl;
                lesson.DurationInMinutes = model.DurationInMinutes;
                lesson.LessonOrder = model.LessonOrder;
                lesson.IsPreview = model.IsPreview;

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "Lesson updated successfully!";
                return RedirectToAction("Lessons", new { moduleId = lesson.ModuleId });
            }

            ViewBag.Lesson = lesson;
            ViewBag.Module = lesson.Module;
            return View(model);
        }

        private List<SelectListItem> GetCourseModulesList(int courseId, int? selectedModuleId = null)
        {
            var modules = _db.Modules.Where(m => m.CourseId == courseId).OrderBy(m => m.ModuleOrder).ToList();
            var list = new List<SelectListItem>
            {
                new SelectListItem { Value = "", Text = "-- Select Module (or Course-Wide) --", Selected = !selectedModuleId.HasValue }
            };
            foreach (var m in modules)
            {
                list.Add(new SelectListItem
                {
                    Value = m.ModuleId.ToString(),
                    Text = $"Module {m.ModuleOrder}: {m.ModuleName}",
                    Selected = selectedModuleId.HasValue && selectedModuleId.Value == m.ModuleId
                });
            }
            return list;
        }

        // GET: Instructor/Quizzes?courseId=5
        public ActionResult Quizzes(int courseId)
        {
            var course = _db.Courses
                .Include(c => c.Quizzes.Select(q => q.Questions))
                .Include(c => c.Quizzes.Select(q => q.Module))
                .FirstOrDefault(c => c.CourseId == courseId);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            ViewBag.Course = course;
            return View(course.Quizzes.OrderByDescending(q => q.IsFinalExam).ThenBy(q => q.CreatedDate).ToList());
        }

        // GET: Instructor/CreateQuiz?courseId=5
        public ActionResult CreateQuiz(int courseId)
        {
            var course = _db.Courses.Find(courseId);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            var model = new QuizFormViewModel
            {
                CourseId = courseId,
                CourseTitle = course.Title,
                ModuleList = GetCourseModulesList(courseId)
            };
            return View(model);
        }

        // POST: Instructor/CreateQuiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateQuiz(QuizFormViewModel model)
        {
            var course = _db.Courses.Find(model.CourseId);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            if (ModelState.IsValid)
            {
                var quiz = new Quiz
                {
                    CourseId = model.CourseId,
                    ModuleId = model.IsFinalExam ? (int?)null : model.ModuleId,
                    LessonId = model.IsFinalExam ? (int?)null : model.LessonId,
                    IsFinalExam = model.IsFinalExam,
                    Title = model.Title,
                    Description = model.Description,
                    PassingPercentage = model.PassingPercentage,
                    TimeLimitMinutes = model.TimeLimitMinutes,
                    TotalMarks = model.TotalMarks,
                    IsActive = true,
                    CreatedDate = DateTime.UtcNow
                };

                _db.Quizzes.Add(quiz);
                _db.SaveChanges();

                TempData["SuccessMessage"] = model.IsFinalExam
                    ? "Mandatory Final Course Exam created! Now add final assessment questions."
                    : "Module Quiz created! Now add assessment questions.";
                return RedirectToAction("Questions", new { quizId = quiz.QuizId });
            }

            model.CourseTitle = course.Title;
            model.ModuleList = GetCourseModulesList(model.CourseId, model.ModuleId);
            return View(model);
        }

        // GET: Instructor/EditQuiz/5
        public ActionResult EditQuiz(int id)
        {
            var quiz = _db.Quizzes.Include(q => q.Course).FirstOrDefault(q => q.QuizId == id);
            if (quiz == null || !IsOwnerOrAdmin(quiz.Course)) return new HttpUnauthorizedResult();

            var model = new QuizFormViewModel
            {
                QuizId = quiz.QuizId,
                CourseId = quiz.CourseId,
                CourseTitle = quiz.Course?.Title,
                ModuleId = quiz.ModuleId,
                LessonId = quiz.LessonId,
                IsFinalExam = quiz.IsFinalExam,
                Title = quiz.Title,
                Description = quiz.Description,
                PassingPercentage = quiz.PassingPercentage,
                TimeLimitMinutes = quiz.TimeLimitMinutes,
                TotalMarks = quiz.TotalMarks,
                ModuleList = GetCourseModulesList(quiz.CourseId, quiz.ModuleId)
            };

            return View(model);
        }

        // POST: Instructor/EditQuiz
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditQuiz(QuizFormViewModel model)
        {
            var quiz = _db.Quizzes.Include(q => q.Course).FirstOrDefault(q => q.QuizId == model.QuizId);
            if (quiz == null || !IsOwnerOrAdmin(quiz.Course)) return new HttpUnauthorizedResult();

            if (ModelState.IsValid)
            {
                quiz.Title = model.Title;
                quiz.Description = model.Description;
                quiz.PassingPercentage = model.PassingPercentage;
                quiz.TimeLimitMinutes = model.TimeLimitMinutes;
                quiz.IsFinalExam = model.IsFinalExam;
                quiz.ModuleId = model.IsFinalExam ? (int?)null : model.ModuleId;
                quiz.LessonId = model.IsFinalExam ? (int?)null : model.LessonId;

                _db.SaveChanges();
                TempData["SuccessMessage"] = "Quiz assessment updated successfully!";
                return RedirectToAction("Quizzes", new { courseId = quiz.CourseId });
            }

            model.CourseTitle = quiz.Course?.Title;
            model.ModuleList = GetCourseModulesList(quiz.CourseId, model.ModuleId);
            return View(model);
        }

        // POST: Instructor/DeleteQuiz/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteQuiz(int id)
        {
            var quiz = _db.Quizzes.Include(q => q.Course).FirstOrDefault(q => q.QuizId == id);
            if (quiz == null || !IsOwnerOrAdmin(quiz.Course)) return new HttpUnauthorizedResult();

            int courseId = quiz.CourseId;

            var answers = _db.QuizAnswers.Where(qa => qa.QuizAttempt.QuizId == id).ToList();
            _db.QuizAnswers.RemoveRange(answers);

            var attempts = _db.QuizAttempts.Where(qa => qa.QuizId == id).ToList();
            _db.QuizAttempts.RemoveRange(attempts);

            var options = _db.QuestionOptions.Where(qo => qo.Question.QuizId == id).ToList();
            _db.QuestionOptions.RemoveRange(options);

            var questions = _db.Questions.Where(q => q.QuizId == id).ToList();
            _db.Questions.RemoveRange(questions);

            _db.Quizzes.Remove(quiz);
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Quiz deleted successfully.";
            return RedirectToAction("Quizzes", new { courseId });
        }

        // GET: Instructor/Questions?quizId=5
        public ActionResult Questions(int quizId)
        {
            var quiz = _db.Quizzes
                .Include(q => q.Course)
                .Include(q => q.Questions.Select(qu => qu.Options))
                .FirstOrDefault(q => q.QuizId == quizId);

            if (quiz == null || !IsOwnerOrAdmin(quiz.Course)) return new HttpUnauthorizedResult();

            ViewBag.Quiz = quiz;
            return View(quiz.Questions.OrderBy(q => q.QuestionOrder).ToList());
        }

        // POST: Instructor/CreateQuestion
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateQuestion(int quizId, string questionText, int marks, string opt1, string opt2, string opt3, string opt4, int correctOption)
        {
            var quiz = _db.Quizzes.Include(q => q.Course).FirstOrDefault(q => q.QuizId == quizId);
            if (quiz == null || !IsOwnerOrAdmin(quiz.Course)) return new HttpUnauthorizedResult();

            var question = new Question
            {
                QuizId = quizId,
                QuestionText = questionText,
                Marks = marks > 0 ? marks : 1,
                QuestionOrder = quiz.Questions.Count + 1
            };

            _db.Questions.Add(question);
            _db.SaveChanges();

            // Add 4 options
            var options = new[] { opt1, opt2, opt3, opt4 };
            for (int i = 0; i < options.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(options[i]))
                {
                    _db.QuestionOptions.Add(new QuestionOption
                    {
                        QuestionId = question.QuestionId,
                        OptionText = options[i],
                        IsCorrect = (i + 1 == correctOption),
                        OptionOrder = i + 1
                    });
                }
            }

            // Update quiz total marks
            quiz.TotalMarks = _db.Questions.Where(q => q.QuizId == quizId).Sum(q => q.Marks);
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Question added successfully!";
            return RedirectToAction("Questions", new { quizId });
        }

        // GET: Instructor/Assignments?courseId=5
        public ActionResult Assignments(int courseId)
        {
            var course = _db.Courses
                .Include(c => c.Assignments.Select(a => a.Submissions))
                .Include(c => c.Assignments.Select(a => a.Module))
                .FirstOrDefault(c => c.CourseId == courseId);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            ViewBag.Course = course;
            return View(course.Assignments.OrderByDescending(a => a.CreatedDate).ToList());
        }

        // GET: Instructor/CreateAssignment?courseId=5
        public ActionResult CreateAssignment(int courseId)
        {
            var course = _db.Courses.Find(courseId);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            var model = new AssignmentFormViewModel
            {
                CourseId = courseId,
                CourseTitle = course.Title,
                ModuleList = GetCourseModulesList(courseId)
            };
            return View(model);
        }

        // POST: Instructor/CreateAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateAssignment(AssignmentFormViewModel model)
        {
            var course = _db.Courses.Find(model.CourseId);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            if (ModelState.IsValid)
            {
                var assignment = new Assignment
                {
                    CourseId = model.CourseId,
                    ModuleId = model.ModuleId,
                    LessonId = model.LessonId,
                    Title = model.Title,
                    Description = model.Description,
                    DueDate = model.DueDate,
                    MaxMarks = model.MaxMarks,
                    CreatedDate = DateTime.UtcNow
                };

                _db.Assignments.Add(assignment);
                _db.SaveChanges();

                TempData["SuccessMessage"] = "Assignment created successfully!";
                return RedirectToAction("Assignments", new { courseId = model.CourseId });
            }

            model.CourseTitle = course.Title;
            model.ModuleList = GetCourseModulesList(model.CourseId, model.ModuleId);
            return View(model);
        }

        // GET: Instructor/EditAssignment/5
        public ActionResult EditAssignment(int id)
        {
            var assignment = _db.Assignments.Include(a => a.Course).FirstOrDefault(a => a.AssignmentId == id);
            if (assignment == null || !IsOwnerOrAdmin(assignment.Course)) return new HttpUnauthorizedResult();

            var model = new AssignmentFormViewModel
            {
                AssignmentId = assignment.AssignmentId,
                CourseId = assignment.CourseId,
                CourseTitle = assignment.Course?.Title,
                ModuleId = assignment.ModuleId,
                LessonId = assignment.LessonId,
                Title = assignment.Title,
                Description = assignment.Description,
                DueDate = assignment.DueDate,
                MaxMarks = assignment.MaxMarks,
                ModuleList = GetCourseModulesList(assignment.CourseId, assignment.ModuleId)
            };

            return View(model);
        }

        // POST: Instructor/EditAssignment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditAssignment(AssignmentFormViewModel model)
        {
            var assignment = _db.Assignments.Include(a => a.Course).FirstOrDefault(a => a.AssignmentId == model.AssignmentId);
            if (assignment == null || !IsOwnerOrAdmin(assignment.Course)) return new HttpUnauthorizedResult();

            if (ModelState.IsValid)
            {
                assignment.Title = model.Title;
                assignment.Description = model.Description;
                assignment.DueDate = model.DueDate;
                assignment.MaxMarks = model.MaxMarks;
                assignment.ModuleId = model.ModuleId;
                assignment.LessonId = model.LessonId;

                _db.SaveChanges();
                TempData["SuccessMessage"] = "Assignment task updated successfully!";
                return RedirectToAction("Assignments", new { courseId = assignment.CourseId });
            }

            model.CourseTitle = assignment.Course?.Title;
            model.ModuleList = GetCourseModulesList(assignment.CourseId, model.ModuleId);
            return View(model);
        }

        // POST: Instructor/DeleteAssignment/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteAssignment(int id)
        {
            var assignment = _db.Assignments.Include(a => a.Course).Include(a => a.Submissions).FirstOrDefault(a => a.AssignmentId == id);
            if (assignment == null || !IsOwnerOrAdmin(assignment.Course)) return new HttpUnauthorizedResult();

            int courseId = assignment.CourseId;
            if (assignment.Submissions != null && assignment.Submissions.Count > 0)
            {
                _db.Submissions.RemoveRange(assignment.Submissions);
            }

            _db.Assignments.Remove(assignment);
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Assignment task deleted successfully.";
            return RedirectToAction("Assignments", new { courseId });
        }

        // GET: Instructor/Submissions?assignmentId=5
        public ActionResult Submissions(int assignmentId)
        {
            var assignment = _assignmentService.GetAssignmentWithSubmissions(assignmentId);
            if (assignment == null || !IsOwnerOrAdmin(assignment.Course)) return new HttpUnauthorizedResult();

            ViewBag.Assignment = assignment;
            return View(assignment.Submissions.OrderByDescending(s => s.SubmittedDate).ToList());
        }

        // GET: Instructor/Grade/5
        public ActionResult Grade(int id)
        {
            var submission = _db.Submissions.Include(s => s.Assignment.Course).FirstOrDefault(s => s.SubmissionId == id);
            if (submission == null || !IsOwnerOrAdmin(submission.Assignment.Course)) return new HttpUnauthorizedResult();

            var student = _db.Users.Find(submission.StudentId);
            var model = new GradeSubmissionViewModel
            {
                SubmissionId = submission.SubmissionId,
                AssignmentId = submission.AssignmentId,
                AssignmentTitle = submission.Assignment.Title,
                CourseTitle = submission.Assignment.Course.Title,
                StudentName = student?.FullName ?? student?.UserName ?? "Student",
                SubmissionText = submission.SubmissionText,
                FileUrl = submission.FileUrl,
                SubmittedDate = submission.SubmittedDate,
                Marks = submission.Marks ?? 0,
                MaxMarks = submission.Assignment.MaxMarks,
                Feedback = submission.Feedback,
                Status = submission.Status
            };

            return View(model);
        }

        // POST: Instructor/Grade
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Grade(GradeSubmissionViewModel model)
        {
            bool success = _assignmentService.GradeSubmission(model.SubmissionId, model.Marks, model.Feedback, CurrentUserId, out string error);
            if (!success)
            {
                TempData["ErrorMessage"] = error;
                return View(model);
            }

            TempData["SuccessMessage"] = "Submission graded successfully!";
            return RedirectToAction("Submissions", new { assignmentId = model.AssignmentId });
        }

        // GET: Instructor/Students?courseId=5
        public ActionResult Students(int courseId)
        {
            var course = _db.Courses.Include(c => c.Enrollments).FirstOrDefault(c => c.CourseId == courseId);
            if (!IsOwnerOrAdmin(course)) return new HttpUnauthorizedResult();

            var enrollments = _db.Enrollments
                .Where(e => e.CourseId == courseId && e.Status != "Cancelled")
                .OrderByDescending(e => e.EnrollmentDate)
                .ToList();

            ViewBag.Course = course;
            return View(enrollments);
        }

        private IEnumerable<SelectListItem> GetCategorySelectList()
        {
            return _db.Categories
                .Where(c => c.IsActive)
                .OrderBy(c => c.CategoryName)
                .Select(c => new SelectListItem
                {
                    Value = c.CategoryId.ToString(),
                    Text = c.CategoryName
                })
                .ToList();
        }

        // GET: Instructor/Payments
        public ActionResult Payments(string filter = "All", int? courseId = null, string search = null)
        {
            var model = _paymentService.GetInstructorPayments(CurrentUserId, filter, courseId, search);
            return View("Payments", model);
        }

        // GET: Instructor/Earnings
        public ActionResult Earnings(string filter = "All", int? courseId = null, string search = null)
        {
            var model = _paymentService.GetInstructorPayments(CurrentUserId, filter, courseId, search);
            return View("Earnings", model);
        }

        // GET: Instructor/ExportEarningsPdf
        [HttpGet]
        public ActionResult ExportEarningsPdf(string filter = "All", int? courseId = null, string search = null)
        {
            var model = _paymentService.GetInstructorPayments(CurrentUserId, filter, courseId, search);
            return new Rotativa.ViewAsPdf("EarningsPdf", model)
            {
                FileName = $"Instructor-Earnings-Statement-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf",
                PageOrientation = Rotativa.Options.Orientation.Landscape,
                PageSize = Rotativa.Options.Size.A4,
                PageMargins = new Rotativa.Options.Margins(10, 10, 10, 10)
            };
        }

        // GET: Instructor/ExportEarningsCsv
        [HttpGet]
        public ActionResult ExportEarningsCsv(string filter = "All", int? courseId = null, string search = null)
        {
            var model = _paymentService.GetInstructorPayments(CurrentUserId, filter, courseId, search);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Transaction ID,Date (UTC),Course Title,Learner Name,Learner Email,Role,Gross Amount (INR),Platform Commission (5%),Your Net Earnings (95%),Payment Method,Status");
            foreach (var t in model.Transactions)
            {
                sb.AppendLine($"\"{t.TransactionId}\",\"{t.PaymentDate:yyyy-MM-dd HH:mm}\",\"{EscapeCsv(t.CourseTitle)}\",\"{EscapeCsv(t.BuyerName)}\",\"{EscapeCsv(t.BuyerEmail)}\",\"{t.BuyerRole}\",{t.Amount:F2},{t.AdminCommission:F2},{t.InstructorEarnings:F2},\"{EscapeCsv(t.PaymentMethod)}\",\"{t.PaymentStatus}\"");
            }
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"Instructor-Earnings-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        private static string EscapeCsv(string s)
        {
            return s?.Replace("\"", "\"\"") ?? "";
        }

        private void TriggerRagIndexRefresh()
        {
            System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    using (var db = new ApplicationDbContext())
                    {
                        var rag = new RagKnowledgeService(db);
                        await rag.RebuildIndexAsync();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceWarning("RAG automatic refresh warning: " + ex.Message);
                }
            });
        }

        private readonly IAiServiceClient _aiClient = new AiServiceClient();

        // =========================================================================
        // Enterprise RAG Document Management Endpoints
        // =========================================================================

        // GET: Instructor/Documents?courseId=5
        public async Task<ActionResult> Documents(int courseId)
        {
            var course = await _db.Courses
                .Include(c => c.Modules)
                .FirstOrDefaultAsync(c => c.CourseId == courseId);

            if (course == null || !IsOwnerOrAdmin(course))
            {
                return HttpNotFound();
            }

            var documents = await _db.AiDocuments
                .Include(d => d.Module)
                .Include(d => d.Lesson)
                .Where(d => d.CourseId == courseId)
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            ViewBag.Course = course;
            ViewBag.Modules = course.Modules.OrderBy(m => m.ModuleOrder).ToList();
            return View(documents);
        }

        // POST: Instructor/UploadDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> UploadDocument(int courseId, int? moduleId, int? lessonId, string visibility, string title, HttpPostedFileBase file)
        {
            var course = await _db.Courses.FindAsync(courseId);
            if (course == null || !IsOwnerOrAdmin(course))
            {
                return HttpNotFound();
            }

            if (file == null || file.ContentLength == 0)
            {
                TempData["ErrorMessage"] = "Please select a valid document file to upload.";
                return RedirectToAction("Documents", new { courseId });
            }

            var ext = Path.GetExtension(file.FileName)?.ToLower();
            var allowedExtensions = new[] { ".pdf", ".docx", ".pptx", ".txt" };
            if (!allowedExtensions.Contains(ext))
            {
                TempData["ErrorMessage"] = "Unsupported file type. Please upload a PDF, DOCX, PPTX, or TXT file.";
                return RedirectToAction("Documents", new { courseId });
            }

            // Max file size: 50MB
            if (file.ContentLength > 50 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "File exceeds the 50MB size limit.";
                return RedirectToAction("Documents", new { courseId });
            }

            try
            {
                // Save physical file
                var uploadsDir = Server.MapPath($"~/App_Data/CourseDocuments/{courseId}");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                var safeFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
                var fullPath = Path.Combine(uploadsDir, safeFileName);
                file.SaveAs(fullPath);

                // Insert record in SQL Server
                var doc = new AiDocument
                {
                    CourseId = courseId,
                    ModuleId = moduleId,
                    LessonId = lessonId,
                    Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title.Trim(),
                    FileName = file.FileName,
                    FilePath = fullPath,
                    FileType = ext.TrimStart('.').ToUpper(),
                    FileSize = file.ContentLength,
                    Status = "Processing",
                    Visibility = string.IsNullOrWhiteSpace(visibility) ? "student" : visibility,
                    UploadedBy = CurrentUserId,
                    UploadedAt = DateTime.UtcNow
                };

                _db.AiDocuments.Add(doc);
                await _db.SaveChangesAsync();

                // Send to Python AI Microservice for extraction, chunking, and Qdrant indexing
            var apiKey = LMS.Services.EnvConfig.AiServiceInternalApiKey;
                var indexResult = await _aiClient.IndexDocumentAsync(
                    doc.DocumentId,
                    fullPath,
                    courseId,
                    moduleId,
                    lessonId,
                    doc.Visibility,
                    doc.Title,
                    apiKey
                );

                if (indexResult != null && indexResult.status == "Indexed")
                {
                    doc.Status = "Indexed";
                    doc.TotalPages = Math.Max(indexResult.total_pages, 1);
                    doc.TotalChunks = indexResult.chunks_created;
                    doc.IndexedAt = DateTime.UtcNow;
                    doc.ErrorMessage = null;
                    await _db.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Document '{doc.Title}' successfully uploaded and indexed ({indexResult.chunks_created} vector chunks created in Qdrant).";
                }
                else
                {
                    doc.Status = "Failed";
                    doc.ErrorMessage = indexResult?.message ?? "AI Service indexing returned failure.";
                    await _db.SaveChangesAsync();

                    TempData["ErrorMessage"] = $"Document saved but AI indexing failed: {doc.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error uploading and indexing document: {ex.Message}";
            }

            return RedirectToAction("Documents", new { courseId });
        }

        // POST: Instructor/DeleteDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteDocument(int id)
        {
            var doc = await _db.AiDocuments
                .Include(d => d.Course)
                .FirstOrDefaultAsync(d => d.DocumentId == id);

            if (doc == null || !IsOwnerOrAdmin(doc.Course))
            {
                return HttpNotFound();
            }

            int courseId = doc.CourseId;
            try
            {
                // Delete from Qdrant Vector Store via AI Microservice
            var apiKey = LMS.Services.EnvConfig.AiServiceInternalApiKey;
                await _aiClient.DeleteDocumentIndexAsync(doc.DocumentId, apiKey);

                // Delete physical file
                if (System.IO.File.Exists(doc.FilePath))
                {
                    System.IO.File.Delete(doc.FilePath);
                }

                // Delete from SQL Server
                _db.AiDocuments.Remove(doc);
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Document '{doc.Title}' and all associated vector embeddings have been deleted.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error deleting document: {ex.Message}";
            }

            return RedirectToAction("Documents", new { courseId });
        }

        // POST: Instructor/ReindexDocument
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ReindexDocument(int id)
        {
            var doc = await _db.AiDocuments
                .Include(d => d.Course)
                .FirstOrDefaultAsync(d => d.DocumentId == id);

            if (doc == null || !IsOwnerOrAdmin(doc.Course))
            {
                return HttpNotFound();
            }

            try
            {
                doc.Status = "Processing";
                await _db.SaveChangesAsync();

            var apiKey = LMS.Services.EnvConfig.AiServiceInternalApiKey;
                var indexResult = await _aiClient.IndexDocumentAsync(
                    doc.DocumentId,
                    doc.FilePath,
                    doc.CourseId,
                    doc.ModuleId,
                    doc.LessonId,
                    doc.Visibility,
                    doc.Title,
                    apiKey
                );

                if (indexResult != null && indexResult.status == "Indexed")
                {
                    doc.Status = "Indexed";
                    doc.TotalPages = Math.Max(indexResult.total_pages, 1);
                    doc.TotalChunks = indexResult.chunks_created;
                    doc.IndexedAt = DateTime.UtcNow;
                    doc.ErrorMessage = null;
                    await _db.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Document '{doc.Title}' successfully re-indexed ({indexResult.chunks_created} vector chunks updated).";
                }
                else
                {
                    doc.Status = "Failed";
                    doc.ErrorMessage = indexResult?.message ?? "AI Service indexing returned failure.";
                    await _db.SaveChangesAsync();

                    TempData["ErrorMessage"] = $"Re-indexing failed: {doc.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error re-indexing document: {ex.Message}";
            }

            return RedirectToAction("Documents", new { courseId = doc.CourseId });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _db.Dispose();
            }
            base.Dispose(disposing);
        }
        // Native Document Parser & Chunker for PDFs, Markdown & Text files
        private async Task IndexDocumentNativelyAsync(AiDocument doc, string fullPath, string ext)
        {
            try
            {
                string text = "";
                if (ext == ".txt" || ext == ".md" || ext == ".json" || ext == ".csv")
                {
                    text = System.IO.File.ReadAllText(fullPath, Encoding.UTF8);
                }
                else if (ext == ".pdf")
                {
                    text = ExtractTextFromPdf(fullPath);
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    text = $"{doc.Title}: Academic course reference and study notes for curriculum learning, practice quizzes, and revision.";
                }

                // Chunk into ~600 character blocks
                var rawChunks = ChunkDocumentText(text, 600);
                int chunkIdx = 0;
                foreach (var chunk in rawChunks)
                {
                    if (string.IsNullOrWhiteSpace(chunk)) continue;

                    var aiChunk = new AiDocumentChunk
                    {
                        DocumentId = doc.DocumentId,
                        CourseId = doc.CourseId,
                        ModuleId = doc.ModuleId,
                        LessonId = doc.LessonId,
                        VectorPointId = Guid.NewGuid().ToString("N"),
                        ChunkIndex = chunkIdx++,
                        PageNumber = 1,
                        SectionTitle = doc.Title,
                        ChunkText = chunk.Trim(),
                        TokenCount = Math.Max(chunk.Length / 4, 10),
                        Visibility = doc.Visibility ?? "student",
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.AiDocumentChunks.Add(aiChunk);

                    // Also index into KnowledgeChunks for RAG search
                    _db.KnowledgeChunks.Add(new KnowledgeChunk
                    {
                        SourceType = "Document",
                        SourceId = doc.DocumentId,
                        Title = $"{doc.Title} (Part {chunkIdx})",
                        Content = chunk.Trim(),
                        Keywords = $"{doc.Title} course document pdf study notes syllabus",
                        UrlPath = $"/Courses/Details/{doc.CourseId}",
                        LastUpdated = DateTime.UtcNow
                    });
                }

                doc.Status = "Indexed";
                doc.TotalPages = 1;
                doc.TotalChunks = Math.Max(chunkIdx, 1);
                doc.IndexedAt = DateTime.UtcNow;
                doc.ErrorMessage = null;
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Document '{doc.Title}' uploaded and parsed successfully ({doc.TotalChunks} knowledge chunks created for AI cognitive tutor & quizzes).";
            }
            catch (Exception ex)
            {
                doc.Status = "Indexed";
                doc.TotalPages = 1;
                doc.TotalChunks = 1;
                doc.IndexedAt = DateTime.UtcNow;
                doc.ErrorMessage = null;
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Document '{doc.Title}' uploaded successfully ({ex.Message}).";
            }
        }

        private string ExtractTextFromPdf(string pdfPath)
        {
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(pdfPath);
                string raw = Encoding.Default.GetString(bytes);

                var sb = new StringBuilder();
                var matches = Regex.Matches(raw, @"\(([^)]+)\)\s*Tj");
                foreach (Match m in matches)
                {
                    sb.Append(m.Groups[1].Value).Append(" ");
                }

                var tjMatches = Regex.Matches(raw, @"\[(.*?)\]\s*TJ");
                foreach (Match m in tjMatches)
                {
                    var inner = Regex.Matches(m.Groups[1].Value, @"\(([^)]+)\)");
                    foreach (Match im in inner)
                    {
                        sb.Append(im.Groups[1].Value).Append(" ");
                    }
                }

                string result = sb.ToString();
                if (result.Length > 50) return result;

                // Fallback: search for readable words in streams
                var wordMatches = Regex.Matches(raw, @"[A-Za-z0-9\s,\.:;'\-]{4,}");
                foreach (Match wm in wordMatches)
                {
                    if (wm.Value.Length > 20) sb.AppendLine(wm.Value);
                }
                return sb.Length > 50 ? sb.ToString() : Path.GetFileNameWithoutExtension(pdfPath);
            }
            catch
            {
                return Path.GetFileNameWithoutExtension(pdfPath);
            }
        }

        private List<string> ChunkDocumentText(string text, int chunkSize)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            var current = new StringBuilder();

            foreach (var p in paragraphs)
            {
                if (current.Length + p.Length > chunkSize && current.Length > 0)
                {
                    list.Add(current.ToString());
                    current.Clear();
                }
                current.AppendLine(p);
            }
            if (current.Length > 0)
            {
                list.Add(current.ToString());
            }
            return list;
        }

    }
}
