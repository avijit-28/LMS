using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using LMS.Data;
using LMS.Models;
using LMS.ViewModels;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin.Security;

namespace LMS.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private ApplicationSignInManager _signInManager;
        private ApplicationUserManager _userManager;
        private ApplicationDbContext _db = new ApplicationDbContext();

        public AccountController()
        {
        }

        public AccountController(ApplicationUserManager userManager, ApplicationSignInManager signInManager)
        {
            UserManager = userManager;
            SignInManager = signInManager;
        }

        public ApplicationSignInManager SignInManager
        {
            get => _signInManager ?? HttpContext.GetOwinContext().Get<ApplicationSignInManager>();
            private set => _signInManager = value;
        }

        public ApplicationUserManager UserManager
        {
            get => _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            private set => _userManager = value;
        }

        private IAuthenticationManager AuthenticationManager => HttpContext.GetOwinContext().Authentication;

        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            if (Request.QueryString["antiforgery"] == "renew")
            {
                try
                {
                    AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                }
                catch { }
                ViewBag.StatusMessage = "Your security token has expired or was renewed. Please sign in to continue.";
            }
            else if (User.Identity.IsAuthenticated)
            {
                // Prevent infinite redirect loop when an authenticated user lacks permissions for returnUrl
                return RedirectToLocal(null);
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Login(LoginViewModel model, string returnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            string email = model.Email?.Trim();
            var result = await SignInManager.PasswordSignInAsync(email, model.Password, model.RememberMe, shouldLockout: false);
            switch (result)
            {
                case SignInStatus.Success:
                    var user = await UserManager.FindByEmailAsync(email) ?? await UserManager.FindByNameAsync(email);
                    if (user != null && !user.IsApproved)
                    {
                        // User credentials are correct, but account is NOT yet approved by Admin!
                        AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);

                        if (string.Equals(user.ApprovalStatus, "Declined", StringComparison.OrdinalIgnoreCase))
                        {
                            TempData["ErrorMessage"] = !string.IsNullOrWhiteSpace(user.RejectionReason)
                                ? $"Your registration application was declined by the administrator. Reason: {user.RejectionReason}"
                                : "Your registration application was declined by the administrator. Please contact support.";
                        }
                        else
                        {
                            TempData["InfoMessage"] = "Your instructor account is currently pending administrator verification. You will be able to sign in once approved.";
                        }

                        // Always redirect unapproved accounts to PendingApproval with their email
                        return RedirectToAction("PendingApproval", new { email = user.Email });
                    }
                    return await RedirectToUserDashboardAsync(user, returnUrl);
                case SignInStatus.LockedOut:
                    return View("Lockout");
                case SignInStatus.RequiresVerification:
                    return RedirectToAction("SendCode", new { ReturnUrl = returnUrl, RememberMe = model.RememberMe });
                case SignInStatus.Failure:
                default:
                    ModelState.AddModelError("", "Invalid email or password.");
                    return View(model);
            }
        }

        // GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                string fullName = model.FullName?.Trim();
                string email = model.Email?.Trim();
                string bio = model.Bio?.Trim();

                // Disallow registering as Admin
                bool isInstructor = string.Equals(model.Role, "Instructor", StringComparison.OrdinalIgnoreCase);
                string chosenRole = isInstructor ? "Instructor" : "Student";

                var user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    Bio = bio,
                    CreatedDate = DateTime.UtcNow,
                    RegisteredDate = DateTime.UtcNow,
                    IsApproved = !isInstructor, // Students approved immediately! Instructors default to Pending
                    ApprovalStatus = isInstructor ? "Pending" : "Approved",
                    RequestedRole = chosenRole
                };

                var result = await UserManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    if (isInstructor)
                    {
                        // Do NOT auto sign-in unapproved teachers!
                        // Dispatch an administrative Notification to all Admin users
                        await NotifyAdminsOfNewRegistrationAsync(user);

                        // Set success message and immediately redirect to the dedicated waiting/approval screen
                        TempData["SuccessMessage"] = "Your educator registration has been submitted successfully and is awaiting administrator verification.";
                        return RedirectToAction("PendingApproval", new { email = user.Email });
                    }
                    else
                    {
                        // Student is approved immediately! Assign Student role and sign in
                        await UserManager.AddToRoleAsync(user.Id, "Student");
                        await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                        return RedirectToAction("Index", "Student");
                    }
                }
                AddErrors(result);
            }

            return View(model);
        }

        // GET: /Account/PendingApproval
        [AllowAnonymous]
        public async Task<ActionResult> PendingApproval(string email)
        {
            if (string.IsNullOrEmpty(email) && User.Identity.IsAuthenticated)
            {
                email = User.Identity.GetUserName();
            }

            PendingApprovalViewModel model = null;
            if (!string.IsNullOrEmpty(email))
            {
                email = email.Trim();
                var user = await UserManager.FindByEmailAsync(email) ?? await UserManager.FindByNameAsync(email);
                if (user != null)
                {
                    // If user was already approved and is authenticated, redirect to user dashboard
                    if (user.IsApproved && User.Identity.IsAuthenticated)
                    {
                        return await RedirectToUserDashboardAsync(user, null);
                    }

                    model = new PendingApprovalViewModel
                    {
                        FullName = user.FullName,
                        Email = user.Email,
                        RequestedRole = !string.IsNullOrEmpty(user.RequestedRole) ? user.RequestedRole : "Instructor",
                        RegisteredDate = user.RegisteredDate != default(DateTime) ? user.RegisteredDate : user.CreatedDate,
                        ApprovalStatus = user.ApprovalStatus ?? "Pending",
                        RejectionReason = user.RejectionReason
                    };
                }
            }

            if (model == null)
            {
                model = new PendingApprovalViewModel
                {
                    FullName = "Valued Educator",
                    Email = email ?? "your-email@example.com",
                    RequestedRole = "Instructor",
                    RegisteredDate = DateTime.UtcNow,
                    ApprovalStatus = "Pending"
                };
            }

            return View(model);
        }

        private async Task NotifyAdminsOfNewRegistrationAsync(ApplicationUser newUser)
        {
            try
            {
                var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
                if (adminRole != null)
                {
                    var adminUserIds = await _db.Database.SqlQuery<string>(
                        "SELECT UserId FROM AspNetUserRoles WHERE RoleId = @p0", adminRole.Id
                    ).ToListAsync();

                    bool isInstructor = string.Equals(newUser.RequestedRole, "Instructor", StringComparison.OrdinalIgnoreCase);
                    string title = isInstructor ? "New Instructor Application Pending Review" : "New Registration Pending Approval";
                    string message = isInstructor
                        ? $"A new Instructor application has been submitted by '{newUser.FullName}' ({newUser.Email}) and is awaiting administrator verification."
                        : $"A new {newUser.RequestedRole} '{newUser.FullName}' ({newUser.Email}) has registered and is awaiting your review and approval.";
                    string notifType = isInstructor ? "InstructorApproval" : "AccountApproval";

                    foreach (var adminId in adminUserIds)
                    {
                        _db.Notifications.Add(new Notification
                        {
                            UserId = adminId,
                            Title = title,
                            Message = message,
                            NotificationType = notifType,
                            IsRead = false,
                            CreatedDate = DateTime.UtcNow
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch
            {
                // Suppress notification delivery error so registration flow completes safely
            }
        }

        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/ForgotPassword
        [AllowAnonymous]
        public async Task<ActionResult> ForgotPassword(string email = null, string tab = null)
        {
            var model = new PasswordRecoveryFlowViewModel
            {
                ActiveTab = tab ?? (string.IsNullOrEmpty(email) ? "request" : "status")
            };

            if (!string.IsNullOrWhiteSpace(email))
            {
                email = email.Trim();
                model.Email = email;

                var req = await _db.PasswordResetRequests
                    .Where(r => r.Email == email)
                    .OrderByDescending(r => r.RequestDate)
                    .FirstOrDefaultAsync();

                if (req != null)
                {
                    model.Status = req.Status;
                    model.IsApproved = req.IsApproved || req.Status == "Approved";
                    model.Reason = req.RequestReason;
                    model.AdminNotes = req.AdminNotes;
                    model.RequestDate = req.RequestDate;
                    model.ResetToken = req.ResetToken;
                    model.ActiveTab = "status";
                }
                else
                {
                    ViewBag.StatusCheckMessage = "No password reset request found for this email. You can submit a new recovery request below.";
                    model.ActiveTab = "request";
                }
            }

            return View(model);
        }

        // POST: /Account/ForgotPassword (Submit Recovery Request)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ForgotPassword(PasswordRecoveryFlowViewModel model)
        {
            model.ActiveTab = "request";

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError("Email", "Email address is required.");
                return View(model);
            }

            var email = model.Email.Trim();
            var user = await UserManager.FindByEmailAsync(email);
            if (user == null)
            {
                TempData["SuccessMessage"] = "If an account exists with this email address, your request has been logged and forwarded to administrators for review.";
                return RedirectToAction("ForgotPassword", new { email = email, tab = "status" });
            }

            var roles = await UserManager.GetRolesAsync(user.Id);
            string role = roles.FirstOrDefault() ?? user.RequestedRole ?? "Student";

            // Check if there is an existing pending request
            var existingReq = await _db.PasswordResetRequests
                .Where(r => r.UserId == user.Id && r.Status == "Pending")
                .OrderByDescending(r => r.RequestDate)
                .FirstOrDefaultAsync();

            if (existingReq != null)
            {
                existingReq.RequestReason = model.Reason;
                existingReq.RequestDate = DateTime.UtcNow;
                existingReq.IsApproved = false;
                existingReq.Status = "Pending";
            }
            else
            {
                var resetRequest = new PasswordResetRequest
                {
                    UserId = user.Id,
                    Email = user.Email,
                    Role = role,
                    RequestReason = model.Reason,
                    RequestDate = DateTime.UtcNow,
                    Status = "Pending",
                    IsApproved = false,
                    ResetToken = Guid.NewGuid().ToString("N"),
                    TokenExpiry = DateTime.UtcNow.AddDays(3)
                };
                _db.PasswordResetRequests.Add(resetRequest);
            }

            await _db.SaveChangesAsync();

            // Notify Administrators
            try
            {
                var adminRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
                if (adminRole != null)
                {
                    var adminIds = await _db.Database.SqlQuery<string>(
                        "SELECT UserId FROM AspNetUserRoles WHERE RoleId = @p0", adminRole.Id
                    ).ToListAsync();

                    foreach (var adminId in adminIds)
                    {
                        _db.Notifications.Add(new Notification
                        {
                            UserId = adminId,
                            Title = "Password Reset Request",
                            Message = $"User '{user.FullName}' ({user.Email}, Role: {role}) requested password recovery. Reason: {model.Reason ?? "Not specified"}",
                            NotificationType = "PasswordReset",
                            IsRead = false,
                            CreatedDate = DateTime.UtcNow
                        });
                    }
                    await _db.SaveChangesAsync();
                }
            }
            catch { }

            TempData["SuccessMessage"] = "Your password recovery request was successfully submitted to the administrator. You can check the approval status below at any time.";
            return RedirectToAction("ForgotPassword", new { email = email, tab = "status" });
        }

        // POST: /Account/CheckResetStatus (AJAX endpoint)
        [HttpPost]
        [AllowAnonymous]
        public async Task<JsonResult> CheckResetStatus(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return Json(new { success = false, message = "Please provide a valid email address." });
            }

            email = email.Trim();
            var req = await _db.PasswordResetRequests
                .Where(r => r.Email == email)
                .OrderByDescending(r => r.RequestDate)
                .FirstOrDefaultAsync();

            if (req == null)
            {
                return Json(new
                {
                    success = true,
                    found = false,
                    message = "No password reset request found for this email. Please submit a request first."
                });
            }

            return Json(new
            {
                success = true,
                found = true,
                email = req.Email,
                status = req.Status,
                isApproved = req.IsApproved || req.Status == "Approved",
                reason = req.RequestReason ?? "No reason specified",
                adminNotes = req.AdminNotes ?? "",
                requestDate = req.RequestDate.ToString("MMM dd, yyyy hh:mm tt"),
                token = req.ResetToken ?? ""
            });
        }

        // POST: /Account/SetNewPassword (When approved by admin)
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SetNewPassword(PasswordRecoveryFlowViewModel model)
        {
            model.ActiveTab = "status";

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError("", "Email is required.");
                return View("ForgotPassword", model);
            }

            if (string.IsNullOrWhiteSpace(model.NewPassword) || model.NewPassword.Length < 6)
            {
                ModelState.AddModelError("NewPassword", "New password must be at least 6 characters long.");
                return View("ForgotPassword", model);
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "The password and confirmation password do not match.");
                return View("ForgotPassword", model);
            }

            var email = model.Email.Trim();
            var req = await _db.PasswordResetRequests
                .Include(r => r.User)
                .Where(r => r.Email == email)
                .OrderByDescending(r => r.RequestDate)
                .FirstOrDefaultAsync();

            if (req == null || (!req.IsApproved && req.Status != "Approved"))
            {
                ModelState.AddModelError("", "Your request is not approved by an administrator yet. Please check again after administrator approval.");
                return View("ForgotPassword", model);
            }

            var user = req.User ?? await UserManager.FindByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "User account could not be found.");
                return View("ForgotPassword", model);
            }

            var removeResult = await UserManager.RemovePasswordAsync(user.Id);
            var addResult = await UserManager.AddPasswordAsync(user.Id, model.NewPassword);

            if (addResult.Succeeded)
            {
                req.Status = "Completed";
                req.IsApproved = false;

                _db.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Title = "Password Changed",
                    Message = "Your account password has been successfully updated via administrator-approved recovery.",
                    NotificationType = "Security",
                    IsRead = false,
                    CreatedDate = DateTime.UtcNow
                });

                await _db.SaveChangesAsync();

                // Automatically log the user in!
                await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);

                TempData["SuccessMessage"] = $"Your password has been reset successfully! Welcome back, {user.FullName}.";

                var roles = await UserManager.GetRolesAsync(user.Id);
                if (roles.Contains("Admin")) return RedirectToAction("Index", "Admin");
                if (roles.Contains("Instructor")) return RedirectToAction("Index", "Instructor");
                return RedirectToAction("Index", "Student");
            }

            foreach (var err in addResult.Errors)
            {
                ModelState.AddModelError("", err);
            }

            return View("ForgotPassword", model);
        }

        // GET: /Account/ForgotPasswordConfirmation
        [AllowAnonymous]
        public ActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        // GET: /Account/ResetPassword
        [AllowAnonymous]
        public async Task<ActionResult> ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Login");
            }

            var request = await _db.PasswordResetRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.ResetToken == token);

            if (request == null)
            {
                ViewBag.ErrorMessage = "The password reset link is invalid or does not exist.";
                return View("ResetPasswordError");
            }

            if (request.Status == "Pending")
            {
                ViewBag.ErrorMessage = "Your password reset request is still pending administrator review. Once verified and approved by the admin/director, this link will be activated.";
                return View("ResetPasswordError");
            }

            if (request.Status != "Approved")
            {
                ViewBag.ErrorMessage = $"This password reset request has already been {request.Status.ToLower()} or completed.";
                return View("ResetPasswordError");
            }

            if (request.TokenExpiry.HasValue && request.TokenExpiry.Value < DateTime.UtcNow)
            {
                ViewBag.ErrorMessage = "This password reset link has expired. Please submit a new request.";
                return View("ResetPasswordError");
            }

            var model = new ResetPasswordViewModel
            {
                Email = request.Email,
                Code = token
            };

            return View(model);
        }

        // POST: /Account/ResetPassword
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var request = await _db.PasswordResetRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.ResetToken == model.Code && r.Status == "Approved");

            if (request == null || (request.TokenExpiry.HasValue && request.TokenExpiry.Value < DateTime.UtcNow))
            {
                ViewBag.ErrorMessage = "This password reset token is invalid, expired, or not yet approved by an administrator.";
                return View("ResetPasswordError");
            }

            var user = request.User ?? await UserManager.FindByIdAsync(request.UserId);
            if (user == null)
            {
                ViewBag.ErrorMessage = "User account not found.";
                return View("ResetPasswordError");
            }

            // Remove old password and set new password securely
            var removeResult = await UserManager.RemovePasswordAsync(user.Id);
            var addResult = await UserManager.AddPasswordAsync(user.Id, model.Password);

            if (addResult.Succeeded)
            {
                request.Status = "Completed";
                _db.Notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Title = "Password Changed Successfully",
                    Message = "Your account password was successfully updated via administrator-approved reset link.",
                    NotificationType = "Security",
                    IsRead = false,
                    CreatedDate = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                TempData["SuccessMessage"] = "Your password has been changed successfully! You can now sign in with your new credentials.";
                return RedirectToAction("Login");
            }

            foreach (var err in addResult.Errors)
            {
                ModelState.AddModelError("", err);
            }

            return View(model);
        }

        // GET: /Account/Profile
        public new async Task<ActionResult> Profile()
        {
            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user == null) return HttpNotFound();

            var roles = await UserManager.GetRolesAsync(user.Id);

            var model = new ProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Bio = user.Bio,
                ProfilePicture = user.ProfilePicture,
                Role = roles.FirstOrDefault() ?? "Student",
                SignaturePath = user.SignaturePath
            };

            return View(model);
        }

        // POST: /Account/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public new async Task<ActionResult> Profile(ProfileViewModel model, HttpPostedFileBase signatureFile)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
            if (user == null) return HttpNotFound();

            user.FullName = model.FullName;
            user.PhoneNumber = model.PhoneNumber;
            user.Bio = model.Bio;
            user.ProfilePicture = model.ProfilePicture;

            // Handle Drawn Signature (Base64 PNG from Canvas)
            if (!string.IsNullOrWhiteSpace(model.SignatureData) && model.SignatureData.StartsWith("data:image"))
            {
                try
                {
                    var base64Data = model.SignatureData.Substring(model.SignatureData.IndexOf(",") + 1);
                    var imageBytes = Convert.FromBase64String(base64Data);
                    var uploadDir = Server.MapPath("~/Content/uploads/signatures");
                    if (!System.IO.Directory.Exists(uploadDir))
                    {
                        System.IO.Directory.CreateDirectory(uploadDir);
                    }
                    var fileName = "signature_" + user.Id + "_" + DateTime.UtcNow.Ticks + ".png";
                    var filePath = System.IO.Path.Combine(uploadDir, fileName);
                    System.IO.File.WriteAllBytes(filePath, imageBytes);
                    user.SignaturePath = "/Content/uploads/signatures/" + fileName;
                }
                catch { }
            }
            // Handle Uploaded Signature File
            else if (signatureFile != null && signatureFile.ContentLength > 0)
            {
                try
                {
                    var ext = System.IO.Path.GetExtension(signatureFile.FileName).ToLower();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".svg")
                    {
                        var uploadDir = Server.MapPath("~/Content/uploads/signatures");
                        if (!System.IO.Directory.Exists(uploadDir))
                        {
                            System.IO.Directory.CreateDirectory(uploadDir);
                        }
                        var fileName = "signature_" + user.Id + "_" + DateTime.UtcNow.Ticks + ext;
                        var filePath = System.IO.Path.Combine(uploadDir, fileName);
                        signatureFile.SaveAs(filePath);
                        user.SignaturePath = "/Content/uploads/signatures/" + fileName;
                    }
                }
                catch { }
            }

            var result = await UserManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Profile and official credentials updated successfully!";
                return RedirectToAction("Profile");
            }

            AddErrors(result);
            return View(model);
        }

        // GET: /Account/ChangePassword
        public ActionResult ChangePassword()
        {
            return View();
        }

        // POST: /Account/ChangePassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await UserManager.ChangePasswordAsync(User.Identity.GetUserId(), model.OldPassword, model.NewPassword);
            if (result.Succeeded)
            {
                var user = await UserManager.FindByIdAsync(User.Identity.GetUserId());
                if (user != null)
                {
                    await SignInManager.SignInAsync(user, isPersistent: false, rememberBrowser: false);
                }
                TempData["SuccessMessage"] = "Your password has been changed successfully.";
                return RedirectToAction("Profile");
            }

            AddErrors(result);
            return View(model);
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error);
            }
        }

        private async Task<ActionResult> RedirectToUserDashboardAsync(ApplicationUser user, string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            if (user != null)
            {
                if (!user.IsApproved)
                {
                    AuthenticationManager.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                    return RedirectToAction("PendingApproval", new { email = user.Email });
                }

                var roles = await UserManager.GetRolesAsync(user.Id);
                if (roles.Contains("Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                if (roles.Contains("Instructor"))
                {
                    return RedirectToAction("Index", "Instructor");
                }
            }

            return RedirectToAction("Index", "Student");
        }

        private ActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            string userId = User.Identity.GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                var roles = UserManager.GetRoles(userId);
                if (roles.Contains("Admin"))
                {
                    return RedirectToAction("Index", "Admin");
                }
                if (roles.Contains("Instructor"))
                {
                    return RedirectToAction("Index", "Instructor");
                }
            }

            return RedirectToAction("Index", "Student");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_userManager != null)
                {
                    _userManager.Dispose();
                    _userManager = null;
                }
                if (_signInManager != null)
                {
                    _signInManager.Dispose();
                    _signInManager = null;
                }
                if (_db != null)
                {
                    _db.Dispose();
                    _db = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
