using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using LMS.Data;
using LMS.Models;
using LMS.Services;
using LMS.ViewModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace LMS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly DashboardService _dashboardService;
        private readonly PaymentService _paymentService;

        public AdminController()
        {
            _dashboardService = new DashboardService(_db);
            _paymentService = new PaymentService(_db);
        }

        // GET: Admin/Index
        public ActionResult Index()
        {
            var model = _dashboardService.GetAdminDashboard();
            return View(model);
        }

        // GET: Admin/Users
        public ActionResult Users(string roleFilter = null)
        {
            var users = _db.Users.OrderByDescending(u => u.CreatedDate).ToList();
            var userRoles = _db.Database.SqlQuery<UserRoleMapping>(
                "SELECT ur.UserId, r.Name AS RoleName FROM AspNetUserRoles ur INNER JOIN AspNetRoles r ON ur.RoleId = r.Id"
            ).ToList();

            var list = users.Select(u => new UserManagementViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Role = userRoles.FirstOrDefault(ur => ur.UserId == u.Id)?.RoleName ?? "None",
                CreatedDate = u.CreatedDate,
                IsActive = !u.LockoutEnabled || (u.LockoutEndDateUtc == null || u.LockoutEndDateUtc < DateTime.UtcNow),
                IsApproved = u.IsApproved,
                ApprovalStatus = u.ApprovalStatus ?? "Approved",
                RequestedRole = u.RequestedRole
            }).ToList();

            if (!string.IsNullOrEmpty(roleFilter))
            {
                list = list.Where(u => string.Equals(u.Role, roleFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            ViewBag.CurrentRoleFilter = roleFilter;
            ViewBag.PendingCount = _db.Users.Count(u => !u.IsApproved && (u.ApprovalStatus == "Pending" || u.ApprovalStatus == null));
            return View(list);
        }

        // GET: Admin/PendingApprovals
        public ActionResult PendingApprovals(string search = null, string roleFilter = null, string statusFilter = "Pending")
        {
            var query = _db.Users.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                query = query.Where(u => u.Email.Contains(search) || u.FullName.Contains(search));
            }

            if (!string.IsNullOrEmpty(roleFilter))
            {
                query = query.Where(u => u.RequestedRole == roleFilter);
            }

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                if (statusFilter == "Pending")
                {
                    query = query.Where(u => u.ApprovalStatus == "Pending" || (u.ApprovalStatus == null && !u.IsApproved));
                }
                else if (statusFilter == "Approved")
                {
                    query = query.Where(u => u.ApprovalStatus == "Approved" || (u.ApprovalStatus == null && u.IsApproved));
                }
                else
                {
                    query = query.Where(u => u.ApprovalStatus == statusFilter);
                }
            }

            var users = query.OrderByDescending(u => u.RegisteredDate).ToList();

            var list = users.Select(u => new PendingUserApprovalViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                RequestedRole = !string.IsNullOrEmpty(u.RequestedRole) ? u.RequestedRole : "Student",
                RegisteredDate = u.RegisteredDate != default(DateTime) ? u.RegisteredDate : u.CreatedDate,
                ApprovalStatus = u.ApprovalStatus ?? "Pending",
                RejectionReason = u.RejectionReason
            }).ToList();

            ViewBag.TotalPendingCount = _db.Users.Count(u => !u.IsApproved && (u.ApprovalStatus == "Pending" || u.ApprovalStatus == null));
            ViewBag.Search = search;
            ViewBag.RoleFilter = roleFilter;
            ViewBag.StatusFilter = statusFilter;

            return View(list);
        }

        // POST: Admin/ApproveUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ApproveUser(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User ID was not provided.";
                return RedirectToAction("PendingApprovals");
            }

            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(_db));
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("PendingApprovals");
            }

            if (user.IsApproved && user.ApprovalStatus == "Approved")
            {
                TempData["InfoMessage"] = $"Registration for {user.FullName} ({user.Email}) is already approved.";
                return RedirectToAction("PendingApprovals");
            }

            user.IsApproved = true;
            user.ApprovalStatus = "Approved";
            user.RejectionReason = null;

            string roleToAssign = !string.IsNullOrEmpty(user.RequestedRole) ? user.RequestedRole : "Student";
            var currentRoles = await userManager.GetRolesAsync(user.Id);

            var otherRoles = currentRoles.Where(r => r != roleToAssign && r != "Admin").ToArray();
            if (otherRoles.Any())
            {
                await userManager.RemoveFromRolesAsync(user.Id, otherRoles);
            }

            if (!currentRoles.Contains(roleToAssign))
            {
                await userManager.AddToRoleAsync(user.Id, roleToAssign);
            }

            await userManager.UpdateAsync(user);

            // Dispatch notification to user
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = "Account Verified & Approved!",
                Message = $"Congratulations {user.FullName}! Your {roleToAssign} account has been verified and approved by the platform administrator. You can now log in and access all features.",
                NotificationType = "AccountApproval",
                IsRead = false,
                CreatedDate = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Registration for {user.FullName} ({user.Email}) has been approved as {roleToAssign}.";
            return RedirectToAction("PendingApprovals");
        }

        // POST: Admin/DeclineUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeclineUser(string userId, string reason)
        {
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User ID was not provided.";
                return RedirectToAction("PendingApprovals");
            }

            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(_db));
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "User not found.";
                return RedirectToAction("PendingApprovals");
            }

            if (!user.IsApproved && user.ApprovalStatus == "Declined")
            {
                TempData["InfoMessage"] = $"Registration for {user.FullName} ({user.Email}) is already declined.";
                return RedirectToAction("PendingApprovals");
            }

            var currentRoles = await userManager.GetRolesAsync(user.Id);
            if (currentRoles.Contains("Admin"))
            {
                TempData["ErrorMessage"] = "Cannot decline an administrator account.";
                return RedirectToAction("PendingApprovals");
            }

            user.IsApproved = false;
            user.ApprovalStatus = "Declined";

            string trimmedReason = string.IsNullOrWhiteSpace(reason)
                ? "Application does not meet platform verification guidelines."
                : reason.Trim();
            if (trimmedReason.Length > 500)
            {
                trimmedReason = trimmedReason.Substring(0, 500);
            }
            user.RejectionReason = trimmedReason;

            // Revoke any non-admin roles so unapproved user cannot access restricted portals
            var nonAdminRoles = currentRoles.Where(r => r != "Admin").ToArray();
            if (nonAdminRoles.Any())
            {
                await userManager.RemoveFromRolesAsync(user.Id, nonAdminRoles);
            }

            await userManager.UpdateSecurityStampAsync(user.Id);
            await userManager.UpdateAsync(user);

            // Dispatch notification to user
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = "Account Application Declined",
                Message = $"Your application for an EduPlatform account was declined. Reason: {user.RejectionReason}",
                NotificationType = "AccountApproval",
                IsRead = false,
                CreatedDate = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            TempData["WarningMessage"] = $"Registration for {user.FullName} ({user.Email}) has been declined.";
            return RedirectToAction("PendingApprovals");
        }

        // GET: Admin/InstructorApplications
        public ActionResult InstructorApplications(string statusFilter = "Pending", string search = null)
        {
            var instructorRole = _db.Roles.FirstOrDefault(r => r.Name == "Instructor");
            string instructorRoleId = instructorRole?.Id;

            var query = _db.Users.Where(u => u.RequestedRole == "Instructor" || (instructorRoleId != null && u.Roles.Any(r => r.RoleId == instructorRoleId)));

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                query = query.Where(u => u.Email.Contains(search) || u.FullName.Contains(search) || (u.Bio != null && u.Bio.Contains(search)));
            }

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                if (statusFilter == "Pending")
                {
                    query = query.Where(u => u.ApprovalStatus == "Pending" || (u.ApprovalStatus == null && !u.IsApproved));
                }
                else if (statusFilter == "Approved")
                {
                    query = query.Where(u => u.ApprovalStatus == "Approved" || (u.ApprovalStatus == null && u.IsApproved));
                }
                else
                {
                    query = query.Where(u => u.ApprovalStatus == statusFilter);
                }
            }

            var users = query.OrderByDescending(u => u.RegisteredDate).ToList();

            var list = users.Select(u => new InstructorApplicationViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Bio = u.Bio,
                RegisteredDate = u.RegisteredDate != default(DateTime) ? u.RegisteredDate : u.CreatedDate,
                ApprovalStatus = u.ApprovalStatus ?? (u.IsApproved ? "Approved" : "Pending"),
                RejectionReason = u.RejectionReason,
                PhoneNumber = u.PhoneNumber
            }).ToList();

            ViewBag.TotalPendingCount = _db.Users.Count(u => !u.IsApproved && (u.ApprovalStatus == "Pending" || u.ApprovalStatus == null));
            ViewBag.PendingInstructorCount = _db.Users.Count(u => (u.RequestedRole == "Instructor" || (instructorRoleId != null && u.Roles.Any(r => r.RoleId == instructorRoleId))) && !u.IsApproved && (u.ApprovalStatus == "Pending" || u.ApprovalStatus == null));
            ViewBag.Search = search;
            ViewBag.StatusFilter = statusFilter;

            return View(list);
        }

        // POST: Admin/ApproveInstructor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ApproveInstructor(string userId, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User ID was not provided.";
                return RedirectToLocalOrDefault(returnUrl, "InstructorApplications");
            }

            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(_db));
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Applicant not found.";
                return RedirectToLocalOrDefault(returnUrl, "InstructorApplications");
            }

            user.IsApproved = true;
            user.ApprovalStatus = "Approved";
            user.RejectionReason = null;
            user.RequestedRole = "Instructor";

            var currentRoles = await userManager.GetRolesAsync(user.Id);
            var otherRoles = currentRoles.Where(r => r != "Instructor" && r != "Admin").ToArray();
            if (otherRoles.Any())
            {
                await userManager.RemoveFromRolesAsync(user.Id, otherRoles);
            }

            if (!currentRoles.Contains("Instructor"))
            {
                await userManager.AddToRoleAsync(user.Id, "Instructor");
            }

            await userManager.UpdateSecurityStampAsync(user.Id);
            await userManager.UpdateAsync(user);

            // Dispatch notification to instructor
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = "Instructor Application Approved!",
                Message = $"Congratulations {user.FullName}! Your instructor account has been verified and approved by the platform administrator. You can now log in, create courses, and manage your students.",
                NotificationType = "InstructorApproval",
                IsRead = false,
                CreatedDate = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Instructor application for {user.FullName} ({user.Email}) has been approved successfully.";
            return RedirectToLocalOrDefault(returnUrl, "InstructorApplications");
        }

        // POST: Admin/DeclineInstructor
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeclineInstructor(string userId, string reason, string returnUrl = null)
        {
            if (string.IsNullOrEmpty(userId))
            {
                TempData["ErrorMessage"] = "User ID was not provided.";
                return RedirectToLocalOrDefault(returnUrl, "InstructorApplications");
            }

            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(_db));
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Applicant not found.";
                return RedirectToLocalOrDefault(returnUrl, "InstructorApplications");
            }

            var currentRoles = await userManager.GetRolesAsync(user.Id);
            if (currentRoles.Contains("Admin"))
            {
                TempData["ErrorMessage"] = "Cannot decline an administrator account.";
                return RedirectToLocalOrDefault(returnUrl, "InstructorApplications");
            }

            user.IsApproved = false;
            user.ApprovalStatus = "Declined";
            string trimmedReason = string.IsNullOrWhiteSpace(reason)
                ? "Application does not meet platform verification guidelines."
                : reason.Trim();
            if (trimmedReason.Length > 500)
            {
                trimmedReason = trimmedReason.Substring(0, 500);
            }
            user.RejectionReason = trimmedReason;

            // Revoke any non-admin roles so unapproved user cannot access restricted portals
            var nonAdminRoles = currentRoles.Where(r => r != "Admin").ToArray();
            if (nonAdminRoles.Any())
            {
                await userManager.RemoveFromRolesAsync(user.Id, nonAdminRoles);
            }

            await userManager.UpdateSecurityStampAsync(user.Id);
            await userManager.UpdateAsync(user);

            // Dispatch notification to applicant
            _db.Notifications.Add(new Notification
            {
                UserId = user.Id,
                Title = "Instructor Application Declined",
                Message = $"Your application for an Instructor account on EduPlatform was declined. Reason: {user.RejectionReason}",
                NotificationType = "InstructorApproval",
                IsRead = false,
                CreatedDate = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();

            TempData["WarningMessage"] = $"Instructor application for {user.FullName} ({user.Email}) has been declined.";
            return RedirectToLocalOrDefault(returnUrl, "InstructorApplications");
        }

        private ActionResult RedirectToLocalOrDefault(string returnUrl, string defaultAction)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(defaultAction);
        }

        // GET: Admin/Students
        public ActionResult Students()
        {
            return RedirectToAction("Users", new { roleFilter = "Student" });
        }

        // GET: Admin/Instructors
        public ActionResult Instructors()
        {
            return RedirectToAction("Users", new { roleFilter = "Instructor" });
        }

        private class UserRoleMapping
        {
            public string UserId { get; set; }
            public string RoleName { get; set; }
        }

        // POST: Admin/ChangeRole
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangeRole(string userId, string newRole)
        {
            var userManager = new UserManager<ApplicationUser>(new UserStore<ApplicationUser>(_db));
            var currentRoles = userManager.GetRoles(userId);
            if (currentRoles.Any())
            {
                userManager.RemoveFromRoles(userId, currentRoles.ToArray());
            }

            if (!string.IsNullOrEmpty(newRole))
            {
                userManager.AddToRole(userId, newRole);
            }

            var user = userManager.FindById(userId);
            if (user != null && !user.IsApproved)
            {
                user.IsApproved = true;
                user.ApprovalStatus = "Approved";
                user.RejectionReason = null;
                user.RequestedRole = newRole;
                userManager.Update(user);
            }

            TempData["SuccessMessage"] = "User role updated successfully.";
            return RedirectToAction("Users");
        }

        // GET: Admin/Categories
        public ActionResult Categories()
        {
            var categories = _db.Categories
                .Select(c => new CategoryViewModel
                {
                    CategoryId = c.CategoryId,
                    CategoryName = c.CategoryName,
                    Description = c.Description,
                    IsActive = c.IsActive,
                    CourseCount = c.Courses.Count
                })
                .OrderBy(c => c.CategoryName)
                .ToList();

            return View(categories);
        }

        // GET: Admin/CreateCategory
        public ActionResult CreateCategory()
        {
            return View(new CategoryViewModel());
        }

        // POST: Admin/CreateCategory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateCategory(CategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                string catName = model.CategoryName.Trim().ToLower();
                if (_db.Categories.Any(c => c.CategoryName.ToLower() == catName))
                {
                    ModelState.AddModelError("CategoryName", "Subject/Category name already exists.");
                    return View(model);
                }

                var cat = new Category
                {
                    CategoryName = model.CategoryName.Trim(),
                    Description = model.Description?.Trim(),
                    IsActive = true, // Explicit requirement: always saved with IsActive = true
                    CreatedDate = DateTime.UtcNow
                };

                _db.Categories.Add(cat);
                _db.SaveChanges();

                TempData["SuccessMessage"] = "Subject/Category created successfully!";
                return RedirectToAction("Categories");
            }

            return View(model);
        }

        // GET: Admin/EditCategory/5
        public ActionResult EditCategory(int id)
        {
            var cat = _db.Categories.Find(id);
            if (cat == null) return HttpNotFound();

            var model = new CategoryViewModel
            {
                CategoryId = cat.CategoryId,
                CategoryName = cat.CategoryName,
                Description = cat.Description,
                IsActive = cat.IsActive
            };

            return View(model);
        }

        // POST: Admin/EditCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditCategory(CategoryViewModel model)
        {
            if (ModelState.IsValid)
            {
                var cat = _db.Categories.Find(model.CategoryId);
                if (cat == null) return HttpNotFound();

                string catName = model.CategoryName.Trim().ToLower();
                if (_db.Categories.Any(c => c.CategoryId != model.CategoryId && c.CategoryName.ToLower() == catName))
                {
                    ModelState.AddModelError("CategoryName", "Category name already exists.");
                    return View(model);
                }

                cat.CategoryName = model.CategoryName;
                cat.Description = model.Description;
                cat.IsActive = model.IsActive;

                _db.SaveChanges();
                TempData["SuccessMessage"] = "Category updated successfully!";
                return RedirectToAction("Categories");
            }

            return View(model);
        }

        // POST: Admin/DeleteCategory/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteCategory(int id)
        {
            var cat = _db.Categories.Include(c => c.Courses).FirstOrDefault(c => c.CategoryId == id);
            if (cat == null) return HttpNotFound();

            if (cat.Courses.Any())
            {
                TempData["ErrorMessage"] = "Cannot delete category containing courses. Deactivate it instead.";
                return RedirectToAction("Categories");
            }

            _db.Categories.Remove(cat);
            _db.SaveChanges();

            TempData["SuccessMessage"] = "Category removed successfully.";
            return RedirectToAction("Categories");
        }

        // GET: Admin/Courses
        public ActionResult Courses()
        {
            var courses = _db.Courses
                .Include(c => c.Category)
                .OrderByDescending(c => c.CreatedDate)
                .ToList();

            return View(courses);
        }

        // POST: Admin/UpdateCourseStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UpdateCourseStatus(int courseId, string status)
        {
            var course = _db.Courses.Find(courseId);
            if (course == null) return HttpNotFound();

            course.Status = status;
            if (status == "Published" && course.PublishedDate == null)
            {
                course.PublishedDate = DateTime.UtcNow;
            }
            _db.SaveChanges();
            TriggerRagIndexRefresh();

            TempData["SuccessMessage"] = $"Course '{course.Title}' status updated to {status}.";
            return RedirectToAction("Courses");
        }

        // GET: Admin/Enrollments
        public ActionResult Enrollments()
        {
            var enrollments = _db.Enrollments
                .Include(e => e.Course)
                .OrderByDescending(e => e.EnrollmentDate)
                .ToList();

            return View(enrollments);
        }

        // GET: Admin/Payments
        public ActionResult Payments(string filter = "All", int? courseId = null, string search = null)
        {
            var model = _paymentService.GetAdminPayments(filter, courseId, search);
            return View(model);
        }

        // GET: Admin/ExportTransactionsPdf
        [HttpGet]
        public ActionResult ExportTransactionsPdf(string filter = "All", int? courseId = null, string search = null)
        {
            var model = _paymentService.GetAdminPayments(filter, courseId, search);
            return new Rotativa.ViewAsPdf("TransactionsPdf", model)
            {
                FileName = $"Platform-Transactions-Audit-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf",
                PageOrientation = Rotativa.Options.Orientation.Landscape,
                PageSize = Rotativa.Options.Size.A4,
                PageMargins = new Rotativa.Options.Margins(10, 10, 10, 10)
            };
        }

        // GET: Admin/ExportTransactionsCsv
        [HttpGet]
        public ActionResult ExportTransactionsCsv(string filter = "All", int? courseId = null, string search = null)
        {
            var model = _paymentService.GetAdminPayments(filter, courseId, search);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Transaction ID,Date (UTC),Course,Buyer Name,Buyer Email,Buyer Role,Instructor,Amount (INR),Platform Commission (5%),Instructor Earnings (95%),Payment Method,Status");
            foreach (var t in model.Transactions)
            {
                sb.AppendLine($"\"{t.TransactionId}\",\"{t.PaymentDate:yyyy-MM-dd HH:mm}\",\"{EscapeCsv(t.CourseTitle)}\",\"{EscapeCsv(t.BuyerName)}\",\"{EscapeCsv(t.BuyerEmail)}\",\"{t.BuyerRole}\",\"{EscapeCsv(t.InstructorName)}\",{t.Amount:F2},{t.AdminCommission:F2},{t.InstructorEarnings:F2},\"{EscapeCsv(t.PaymentMethod)}\",\"{t.PaymentStatus}\"");
            }
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"Platform-Transactions-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        // GET: Admin/Announcements
        public ActionResult Announcements()
        {
            var announcements = _db.Announcements
                .OrderByDescending(a => a.CreatedDate)
                .ToList();

            return View(announcements);
        }

        // GET: Admin/CreateAnnouncement
        public ActionResult CreateAnnouncement()
        {
            return View(new AnnouncementFormViewModel());
        }

        // POST: Admin/CreateAnnouncement
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateAnnouncement(AnnouncementFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var announcement = new Announcement
                {
                    Title = model.Title,
                    Message = model.Message,
                    CreatedBy = User.Identity.GetUserId(),
                    CreatedDate = DateTime.UtcNow,
                    IsActive = model.IsActive
                };

                _db.Announcements.Add(announcement);
                _db.SaveChanges();

                TempData["SuccessMessage"] = "Announcement published successfully!";
                return RedirectToAction("Announcements");
            }

            return View(model);
        }

        // POST: Admin/DeleteAnnouncement/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteAnnouncement(int id)
        {
            var a = _db.Announcements.Find(id);
            if (a != null)
            {
                _db.Announcements.Remove(a);
                _db.SaveChanges();
            }
            TempData["SuccessMessage"] = "Announcement deleted.";
            return RedirectToAction("Announcements");
        }

        // GET: Admin/Reports
        public ActionResult Reports()
        {
            var model = _dashboardService.GetAdminDashboard();
            return View(model);
        }

        // GET: Admin/ExportReportPdf
        [HttpGet]
        public ActionResult ExportReportPdf()
        {
            var model = _dashboardService.GetAdminDashboard();
            return new Rotativa.ViewAsPdf("ReportPdf", model)
            {
                FileName = $"Platform-Executive-Report-{DateTime.UtcNow:yyyyMMdd-HHmm}.pdf",
                PageOrientation = Rotativa.Options.Orientation.Portrait,
                PageSize = Rotativa.Options.Size.A4,
                PageMargins = new Rotativa.Options.Margins(12, 12, 12, 12)
            };
        }

        // GET: Admin/ExportReportCsv
        [HttpGet]
        public ActionResult ExportReportCsv()
        {
            var model = _dashboardService.GetAdminDashboard();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Executive Metric,Value");
            sb.AppendLine($"Total Registered Users,{model.TotalUsers}");
            sb.AppendLine($"Total Students,{model.TotalStudents}");
            sb.AppendLine($"Total Instructors,{model.TotalInstructors}");
            sb.AppendLine($"Total Courses,{model.TotalCourses}");
            sb.AppendLine($"Published Courses,{model.PublishedCourses}");
            sb.AppendLine($"Total Enrollments,{model.TotalEnrollments}");
            sb.AppendLine($"Total Platform Revenue (INR),{model.TotalRevenue:F2}");
            sb.AppendLine($"Total Certificates Issued,{model.TotalCertificates}");
            sb.AppendLine();
            sb.AppendLine("Category Name,Course Count");
            foreach (var c in model.CategoryStats)
            {
                sb.AppendLine($"\"{EscapeCsv(c.CategoryName)}\",{c.CourseCount}");
            }
            return File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"Platform-Executive-Report-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        private static string EscapeCsv(string s)
        {
            return s?.Replace("\"", "\"\"") ?? "";
        }

        // GET: Admin/PasswordResets
        public async Task<ActionResult> PasswordResets(string statusFilter = "Pending", string search = null)
        {
            var query = _db.PasswordResetRequests.Include(r => r.User).AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.Trim();
                query = query.Where(r => r.Email.Contains(search) || (r.User != null && r.User.FullName.Contains(search)));
            }

            if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "All")
            {
                query = query.Where(r => r.Status == statusFilter);
            }

            var list = await query.OrderByDescending(r => r.RequestDate).ToListAsync();

            ViewBag.CurrentStatus = statusFilter;
            ViewBag.Search = search;
            ViewBag.PendingCount = await _db.PasswordResetRequests.CountAsync(r => r.Status == "Pending");
            ViewBag.ApprovedCount = await _db.PasswordResetRequests.CountAsync(r => r.Status == "Approved");
            ViewBag.RejectedCount = await _db.PasswordResetRequests.CountAsync(r => r.Status == "Rejected");
            ViewBag.CompletedCount = await _db.PasswordResetRequests.CountAsync(r => r.Status == "Completed");

            return View(list);
        }

        // POST: Admin/ApprovePasswordReset
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ApprovePasswordReset(int id, string adminNotes = null)
        {
            var req = await _db.PasswordResetRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.RequestId == id);
            if (req != null)
            {
                req.IsApproved = true;
                req.Status = "Approved";
                req.ApprovedByAdminId = User.Identity.GetUserId();
                req.ApprovedDate = DateTime.UtcNow;
                req.AdminNotes = adminNotes;
                req.TokenExpiry = DateTime.UtcNow.AddDays(3);
                if (string.IsNullOrEmpty(req.ResetToken))
                {
                    req.ResetToken = Guid.NewGuid().ToString("N");
                }

                var resetUrl = Url.Action("ResetPassword", "Account", new { token = req.ResetToken }, Request.Url.Scheme);
                
                // Add notification to requester
                _db.Notifications.Add(new Notification
                {
                    UserId = req.UserId,
                    Title = "Password Reset Request Approved",
                    Message = $"Your password reset request has been verified and approved by the administration. You can now reset your password using the following link: {resetUrl}",
                    NotificationType = "PasswordResetApproved",
                    IsRead = false,
                    CreatedDate = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Password reset request for {req.Email} was APPROVED. Secure reset URL: {resetUrl}";
            }
            else
            {
                TempData["ErrorMessage"] = "Password reset request record not found.";
            }

            return RedirectToAction("PasswordResets");
        }

        // POST: Admin/TogglePasswordResetApproval
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> TogglePasswordResetApproval(int id)
        {
            var req = await _db.PasswordResetRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.RequestId == id);
            if (req != null)
            {
                req.IsApproved = !req.IsApproved;
                req.Status = req.IsApproved ? "Approved" : "Pending";
                req.ApprovedByAdminId = req.IsApproved ? User.Identity.GetUserId() : null;
                req.ApprovedDate = req.IsApproved ? DateTime.UtcNow : (DateTime?)null;

                if (req.IsApproved)
                {
                    req.TokenExpiry = DateTime.UtcNow.AddDays(3);
                    if (string.IsNullOrEmpty(req.ResetToken))
                    {
                        req.ResetToken = Guid.NewGuid().ToString("N");
                    }

                    _db.Notifications.Add(new Notification
                    {
                        UserId = req.UserId,
                        Title = "Password Reset Approved",
                        Message = "Your password reset request has been approved by the administrator. You can now set your new password on the recovery page.",
                        NotificationType = "PasswordResetApproved",
                        IsRead = false,
                        CreatedDate = DateTime.UtcNow
                    });
                }
                else
                {
                    req.AdminNotes = "Approval toggled to pending by administrator";
                }

                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Approval status for {req.Email} toggled to {(req.IsApproved ? "APPROVED" : "PENDING")}.";
            }
            else
            {
                TempData["ErrorMessage"] = "Password reset request record not found.";
            }

            return RedirectToAction("PasswordResets");
        }

        // POST: Admin/RejectPasswordReset
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RejectPasswordReset(int id, string reason = null)
        {
            var req = await _db.PasswordResetRequests.Include(r => r.User).FirstOrDefaultAsync(r => r.RequestId == id);
            if (req != null)
            {
                req.IsApproved = false;
                req.Status = "Rejected";
                req.ApprovedByAdminId = User.Identity.GetUserId();
                req.ApprovedDate = DateTime.UtcNow;
                req.AdminNotes = reason;

                // Add notification to requester
                _db.Notifications.Add(new Notification
                {
                    UserId = req.UserId,
                    Title = "Password Reset Request Declined",
                    Message = $"Your password reset request was declined by the administrator. Reason: {(string.IsNullOrEmpty(reason) ? "Identity could not be verified" : reason)}",
                    NotificationType = "PasswordResetRejected",
                    IsRead = false,
                    CreatedDate = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();
                TempData["WarningMessage"] = $"Password reset request for {req.Email} was DECLINED.";
            }
            else
            {
                TempData["ErrorMessage"] = "Password reset request record not found.";
            }

            return RedirectToAction("PasswordResets");
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
