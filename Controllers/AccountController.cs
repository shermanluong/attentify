using GoogleLogin.Helpers;
using GoogleLogin.Models;
using GoogleLogin.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ShopifyService = GoogleLogin.Services.ShopifyService;

namespace GoogleLogin.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private UserManager<AppUser>            _userManager;
        private SignInManager<AppUser>          _signInManager;
        private readonly EMailService           _emailService;
        private readonly EMailTokenService      _emailTokenService;
        private readonly ShopifyService         _shopifyService; 
        private readonly AppIdentityDbContext   _dbContext;
        private readonly SmsService             _smsService;
        private readonly string                 _phoneNumber;

        public AccountController(
            UserManager<AppUser>        userMgr, 
            SignInManager<AppUser>      signinMgr, 
            EMailService                emailService, 
            EMailTokenService           emailTokenSerivce,
            AppIdentityDbContext        dbContext, 
            ShopifyService              shopifyService,
            SmsService                  smsService, 
            IConfiguration configuration)
        {
            _userManager        =   userMgr;
            _signInManager      =   signinMgr;
            _emailService       =   emailService;
            _emailTokenService  =   emailTokenSerivce;
            _dbContext          =   dbContext;
            _shopifyService     =   shopifyService;
            _smsService         =   smsService;
            _phoneNumber        =   configuration["Twilio:PhoneNumber"] ?? "";
        }

        [AllowAnonymous]
        public IActionResult Login(string returnUrl)
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            await _signInManager.SignOutAsync();
            if (!ModelState.IsValid)
            {
                return Json(new { status = -201, redirectUrl = "/Account/Login" });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                return Json(new { status = -201, redicretUrl = "/Account/Login" });
            }

            var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, false);

            if (result.Succeeded)
            {
                return Json(new { status = 201, redirectUrl = "/home/index" });
            }

            if (result.RequiresTwoFactor)
            {
                return Json(new { status = 201, redirectUrl = "/home/index" });
            }

            bool emailStatus = await _userManager.IsEmailConfirmedAsync(user);
            if (emailStatus == false)
            {
                return Json(new { status = 201, redirectUrl = "/home/index" });
            }

            if (result.IsLockedOut)
                return Json(new { status = 201, redirectUrl = "/home/index" });
            
            return Json(new { status = -201, redicretUrl = "/Account/Login" });
        }

        //
        //GET: /Account/Register
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View("Register");
        }

        //
        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        public async Task<ActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new AppUser { UserName = model.Email, Email = model.Email };
                var result = await _userManager.CreateAsync(user, model.Password);

                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);

                    // For more information on how to enable account confirmation and password reset please visit http://go.microsoft.com/fwlink/?LinkID=320771
                    // Send an email with this link
                    // string code = await UserManager.GenerateEmailConfirmationTokenAsync(user.Id);
                    // var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, protocol: Request.Url.Scheme);
                    // await UserManager.SendEmailAsync(user.Id, "Confirm your account", "Please confirm your account by clicking <a href=\"" + callbackUrl + "\">here</a>");

                    return Json(new { status = 201, redirectUrl = "/home/index" });
                } else
                {
                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors)
                        {
                            return Json(new { status = -201, redirectUrl = "/", description = error.Description});
                        }
                    }
                }
            }

            return Json(new { status = -201, redirectUrl = "/" });
        }

        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult GoogleLogin()
        {
            string? redirectUrl = Url.Action("GoogleResponse", "Account");
            var properties = _signInManager.ConfigureExternalAuthenticationProperties("Google", redirectUrl);
            return new ChallengeResult("Google", properties);
        }

        [AllowAnonymous]
        public async Task<IActionResult> GoogleResponse()
        {
            ExternalLoginInfo? info = await _signInManager.GetExternalLoginInfoAsync();
            
            if (info == null)
                return RedirectToAction(nameof(Login));

            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, false);
            var access_token = info.AuthenticationTokens?.FirstOrDefault(t => t.Name == "access_token")?.Value;

            if (!string.IsNullOrEmpty(access_token))
            {
                _emailService.UpdateMailDatabaseAsync(access_token, info.Principal.FindFirst(ClaimTypes.Email)?.Value ?? "", 500);
                _shopifyService.OrderRequest();

                new Thread(async () => {
                    await _shopifyService.CustomersRequest();
                }).Start();

                new Thread(async () =>
                {
                    try
                    {
                        var     user        = await _userManager.GetUserAsync(User);
                        string  strPhone    = _phoneNumber;
                        if (user != null && string.IsNullOrEmpty(user.PhoneNumber))
                        {
                            user.PhoneNumber = strPhone;
                        }
                        else
                        {
                            strPhone = _phoneNumber;
                        }

                        await _smsService.GetMessages(strPhone);
                        await _smsService.SendSmsCountInfo(strPhone); 
                    }catch(Exception ex)
                    {
                        Console.WriteLine("in account/googleResponse thread" + ex.ToString());
                    }
                }).Start();

                HttpContext.Session.SetString("AccessToken", access_token);

                var request = HttpContext.Request;
                var hostUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
                HttpContext.Session.SetString("HostUrl", hostUrl);
            }
            Console.WriteLine(result.Succeeded);
            if (result.Succeeded) 
                return Redirect("/home/index");
            else
            {
                AppUser user = new AppUser
                {
                    Email    = info.Principal.FindFirst(ClaimTypes.Email)?.Value,
                    UserName = info.Principal.FindFirst(ClaimTypes.Name)?.Value
                };

                IdentityResult identResult = await _userManager.CreateAsync(user);
                if (identResult.Succeeded)
                {
                    identResult = await _userManager.AddLoginAsync(user, info);
                    if (identResult.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, false);
                        return Redirect("/home/index");
                    }
                }
                return AccessDenied();
            }
        }        

        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([Required] string email)
        {
            if (!ModelState.IsValid)
                return View(email);

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
                return RedirectToAction(nameof(ForgotPasswordConfirmation));

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var link = Url.Action("ResetPassword", "Account", new { token, email = user.Email }, Request.Scheme);

            EmailHelper emailHelper = new EmailHelper();

            if (!string.IsNullOrEmpty(user.Email) && !string.IsNullOrEmpty(link)) {
                bool emailResponse = emailHelper.SendEmailPasswordReset(user.Email, link);

                if (emailResponse)
                    return RedirectToAction("ForgotPasswordConfirmation");
            }
            return View(email);
        }

        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult ResetPassword(string token, string email)
        {
            var model = new ResetPassword { Token = token, Email = email };
            return View(model);
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(ResetPassword resetPassword)
        {
            if (!ModelState.IsValid)
                return View(resetPassword);
            
            if (string.IsNullOrEmpty(resetPassword.Email)) 
                return View(resetPassword);

            var user = await _userManager.FindByEmailAsync(resetPassword.Email);

            if (user == null) {
                RedirectToAction("ResetPasswordConfirmation");
            }

            if (string.IsNullOrEmpty(resetPassword.Token) || string.IsNullOrEmpty(resetPassword.Password)) {
                return View(resetPassword);
            }
            
            #pragma warning disable CS8604 // Possible null reference argument.
                var resetPassResult = await _userManager.ResetPasswordAsync(user, resetPassword.Token, resetPassword.Password);
            #pragma warning restore CS8604 // Possible null reference argument.

            if (!resetPassResult.Succeeded)
            {
                foreach (var error in resetPassResult.Errors)
                    ModelState.AddModelError(error.Code, error.Description);
                return View();
            }

            return RedirectToAction("ResetPasswordConfirmation");
        }

        public IActionResult ResetPasswordConfirmation()
        {
            return View();
        }
    }
}
