using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GoogleLogin.Services;
using GoogleLogin.Models;
using WebSocketSharp;
using ShopifySharp;

namespace GoogleLogin.Controllers
{
    [Authorize]
    public class CompanyController : Controller
    {
        private readonly IServiceScopeFactory       _serviceScopeFactory;
        private readonly ILogger<HomeController>    _logger;
        private SignInManager<AppUser>              _signInManager;
        private UserManager<AppUser>                _userManager;
        private readonly EMailService               _emailService;
        private readonly IConfiguration             _configuration;
        private readonly CompanyService             _companyService;
        private readonly MemberService              _memberService;

        public CompanyController(
            SignInManager<AppUser>  signinMgr,
            UserManager<AppUser>    userMgr,
            IServiceScopeFactory    serviceScopeFactory,
            EMailService            service,
            ILogger<HomeController> logger,
            IConfiguration          configuration,
            CompanyService          companyService,
            MemberService           memberSerivce)
        {
            _serviceScopeFactory    =   serviceScopeFactory;
            _signInManager          =   signinMgr;
            _userManager            =   userMgr;
            _logger                 =   logger;
            _emailService           =   service;
            _configuration          =   configuration;
            _companyService         =   companyService;
            _memberService          =   memberSerivce;
        }

        public IActionResult Index()
        {
            ViewBag.menu    = "setting";
            ViewBag.subMenu = "company";
            return View("View_Companies");
        }

        [HttpPost("/getCompanies")]
        public async Task<IActionResult> getCompanies()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            string userEmail = user?.Email ?? "";

            ViewBag.companyList = _companyService.getCompanies(userEmail);
            return PartialView("View_CompanyList");
        }

        [HttpPost("/addCompany")]
        public async Task<IActionResult> addCompany(string companyName, string companySite)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            string userEmail = user?.Email ?? "";

            if (userEmail.IsNullOrEmpty())
            {
                return Json(new { status = -201, message = "Adding failed" });
            }

            if ( companyName.IsNullOrEmpty() )
            {
                return Json(new { status = -201, message = "Company name is required!" });
            }


            if (_companyService.getCompany(companyName) != null )
            {
                return Json(new { status = -201, message = "Company name already exists." });
            }

            long companyIdx = _companyService.addCompany(companyName, companySite);

            if (companyIdx > 0)
            {
                _memberService.addMember(userEmail, companyIdx, 0);
                return Json(new { status = 201, message = "Adding success" });
            } else
            {
                return Json(new { status = -201, message = "Adding failed" });
            }
        }

        [HttpPost("/editCompany")]
        public IActionResult editCompany()
        {
            return Json(new { status = -201, message = "Updating failed" });
        }

        [HttpPost("/deleteCompany")]
        public IActionResult deleteCompany(long companyIdx)
        {
            var _one = _companyService.getCompanyByIdx(companyIdx);

            if (_one == null)
            {
                return Json(new { status = -201, message = "Deleting is failed" });
            }

            int nRet = _companyService.deleteCompany(_one);

            if (nRet > 0)
            {
                _memberService.deleteMemeberByCompanyId(companyIdx);
                return Json(new { status = 201, message = "Deleting Success" });
            } else
            {
                return Json(new { status = -201, message = "Deleting is failed" });
            }
        }
    }
}