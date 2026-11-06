using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace LMS.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
        public string ProfilePicture { get; set; }
        public string Bio { get; set; }
        public bool IsApproved { get; set; } = false;
        public string ApprovalStatus { get; set; } = "Pending"; // Pending, Approved, Declined
        public string RequestedRole { get; set; }
        public string RejectionReason { get; set; }
        public DateTime RegisteredDate { get; set; } = DateTime.UtcNow;
        public string SignaturePath { get; set; }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            if (!string.IsNullOrEmpty(FullName))
            {
                userIdentity.AddClaim(new Claim("FullName", FullName));
            }
            return userIdentity;
        }
    }
}
