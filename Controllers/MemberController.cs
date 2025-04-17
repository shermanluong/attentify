
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GoogleLogin.Services;
using GoogleLogin.Models;
using WebSocketSharp;

namespace GoogleLogin.Controllers
{
    [Authorize]
    public class MemberController : Controller
    {
        private readonly IServiceScopeFactory       _serviceScopeFactory;
        private readonly ILogger<HomeController>    _logger;
        private SignInManager<AppUser>              _signInManager;
        private UserManager<AppUser>                _userManager;
        private readonly EMailService               _emailService;
        private readonly IConfiguration             _configuration;
        private readonly CompanyService _companyService;
        private readonly MemberService _memberService;

        public MemberController(
            SignInManager<AppUser>  signinMgr,
            UserManager<AppUser> userMgr,
            IServiceScopeFactory serviceScopeFactory,
            EMailService emailService,
            ILogger<HomeController> logger,
            IConfiguration configuration,
            CompanyService companyService,
            MemberService memberService)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _signInManager = signinMgr;
            _userManager = userMgr;
            _logger = logger;
            _emailService = emailService;
            _configuration = configuration;
            _companyService = companyService;
            _memberService = memberService;
          }

        public async Task<IActionResult> index()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            string userEmail = user?.Email ?? "";

            ViewBag.menu = "setting";
            ViewBag.subMenu = "member";
            ViewBag.companyList = _companyService.getCompanies(userEmail);
            return View("View_Members");
        }

        [HttpPost("/getMembers")]
        public IActionResult getMembers(long companyIdx)
        {
            ViewBag.memberList = _memberService.getMembers(companyIdx);
            return PartialView("View_MemberList");
        }

        [HttpPost("/addMember")]
        public async Task<IActionResult> addMember(long companyIdx, string memberEmail, int memberRole)
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            string userEmail = user?.Email ?? "";

            if (userEmail.IsNullOrEmpty())
            {
                return Json(new { status = -201, message = "Adding failed" });
            }

            if (memberEmail.IsNullOrEmpty())
            {
                return Json(new { status = -201, message = "Email is required!" });
            }


            if (_memberService.getMember(memberEmail, companyIdx) != null)
            {
                return Json(new { status = -201, message = "Email already exists." });
            }

            long memberIdx = _memberService.addMember(memberEmail, companyIdx, memberRole);

            if (memberIdx > 0)
            {
                return Json(new { status = 201, message = "Adding success" });
            }
            else
            {
                return Json(new { status = -201, message = "Adding failed" });
            }
        }

        [HttpPost("/editMember")]
        public IActionResult editMember()
        {
            return Json(new { status = 201, message = "Updated the user info" });
        }

        [HttpPost("/deleteMember")]
        public IActionResult deleteMemeber(long memberIdx)
        {
            var _one = _memberService.getMemberByIdx(memberIdx);

            if (_one == null)
            {
                return Json(new { status = -201, message = "Deleting is failed" });
            }

            int nRet = _memberService.deleteMember(_one);

            if (nRet > 0)
            {
                return Json(new { status = 201, message = "Deleting Success" });
            }
            else
            {
                return Json(new { status = -201, message = "Deleting is failed" });
            }
        }
    }
}