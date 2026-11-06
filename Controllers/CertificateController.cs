using System.Web.Mvc;
using LMS.Data;
using LMS.Services;

namespace LMS.Controllers
{
    public class CertificateController : Controller
    {
        private readonly ApplicationDbContext _db = new ApplicationDbContext();
        private readonly CertificateService _certificateService;

        public CertificateController()
        {
            _certificateService = new CertificateService(_db);
        }

        // GET: Certificate/ViewCertificate/5
        public ActionResult ViewCertificate(int id)
        {
            var model = _certificateService.GetCertificateViewModel(id);
            if (model == null) return HttpNotFound();

            return View(model);
        }

        // GET: Certificate/DownloadPdf/5
        public ActionResult DownloadPdf(int id)
        {
            var model = _certificateService.GetCertificateViewModel(id);
            if (model == null) return HttpNotFound();

            return new Rotativa.ViewAsPdf("CertificatePdf", model)
            {
                FileName = $"Certificate-{model.CertificateNumber}.pdf",
                PageOrientation = Rotativa.Options.Orientation.Landscape,
                PageSize = Rotativa.Options.Size.A4,
                PageMargins = new Rotativa.Options.Margins(5, 5, 5, 5)
            };
        }

        // GET: Certificate/DownloadCertificate/5
        public ActionResult DownloadCertificate(int id)
        {
            return DownloadPdf(id);
        }

        // GET: Certificate/Verify?certNumber=LMS-2026-123456
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Verify(string certNumber = null, string certificateNumber = null, string id = null)
        {
            var target = !string.IsNullOrWhiteSpace(certificateNumber)
                ? certificateNumber
                : (!string.IsNullOrWhiteSpace(certNumber) ? certNumber : id);

            if (target == null)
            {
                return View("VerifyCertificate");
            }

            if (string.IsNullOrWhiteSpace(target))
            {
                ViewBag.ErrorMessage = "Please provide a valid certificate number.";
                return View("VerifyCertificate");
            }

            var cert = _certificateService.GetCertificateByNumber(target.Trim());
            if (cert == null)
            {
                ViewBag.ErrorMessage = $"Certificate #{target.Trim()} was not found or is invalid.";
                return View("VerifyCertificate");
            }

            var model = _certificateService.GetCertificateViewModel(cert.CertificateId);
            return View("ViewCertificate", model);
        }

        // GET: Certificate/VerifyCertificate?certificateNumber=LMS-2026-123456
        [HttpGet]
        [AllowAnonymous]
        public ActionResult VerifyCertificate(string certNumber = null, string certificateNumber = null, string id = null)
        {
            return Verify(certNumber, certificateNumber, id);
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
