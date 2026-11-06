using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using LMS.Services;
using Microsoft.AspNet.Identity;

namespace LMS
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Initialize Centralized Secure Environment Configuration (.env & System Env)
            EnvConfig.Initialize();

            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            // Configure AntiForgery to bind exclusively to the Identity NameIdentifier claim
            AntiForgeryConfig.UniqueClaimTypeIdentifier = ClaimTypes.NameIdentifier;

            try
            {
                using (var db = new LMS.Data.ApplicationDbContext())
                {
                    LMS.Data.DbSeeder.Seed(db);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Database seeding error: " + ex.Message);
            }
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var exception = Server.GetLastError();
            var baseException = exception?.GetBaseException();
            if (exception is HttpAntiForgeryException || 
                baseException is HttpAntiForgeryException ||
                (exception?.Message != null && exception.Message.IndexOf("anti-forgery", StringComparison.OrdinalIgnoreCase) >= 0) ||
                (baseException?.Message != null && baseException.Message.IndexOf("anti-forgery", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                Server.ClearError();
                try
                {
                    var authManager = HttpContext.Current?.GetOwinContext()?.Authentication;
                    authManager?.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                }
                catch { }

                Response.Redirect("~/Account/Login?antiforgery=renew", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
        }
    }
}
