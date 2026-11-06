using System;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;

namespace LMS
{
    public class AntiForgeryExceptionHandlerAttribute : FilterAttribute, IExceptionFilter
    {
        public void OnException(ExceptionContext filterContext)
        {
            if (filterContext == null || filterContext.ExceptionHandled)
                return;

            var ex = filterContext.Exception;
            var baseEx = ex?.GetBaseException();

            bool isAntiForgery = ex is HttpAntiForgeryException ||
                                 baseEx is HttpAntiForgeryException ||
                                 (ex?.Message != null && ex.Message.IndexOf("anti-forgery", StringComparison.OrdinalIgnoreCase) >= 0) ||
                                 (baseEx?.Message != null && baseEx.Message.IndexOf("anti-forgery", StringComparison.OrdinalIgnoreCase) >= 0);

            if (isAntiForgery)
            {
                filterContext.ExceptionHandled = true;

                try
                {
                    var authManager = filterContext.HttpContext.GetOwinContext()?.Authentication;
                    authManager?.SignOut(DefaultAuthenticationTypes.ApplicationCookie);
                }
                catch { }

                filterContext.Controller.TempData["ErrorMessage"] = "Your security token expired or was created for a different session. Please try signing in again.";
                filterContext.Result = new RedirectResult("~/Account/Login?antiforgery=renew");
            }
        }
    }

    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new AntiForgeryExceptionHandlerAttribute(), 0);
            filters.Add(new HandleErrorAttribute());
        }
    }
}
