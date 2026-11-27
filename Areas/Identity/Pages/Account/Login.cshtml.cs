using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Nights.Services.Interfaces;

namespace Nights.Areas.Identity.Pages.Account;

[EnableRateLimiting("auth-policy")]
[ValidateAntiForgeryToken]
public class LoginModel : PageModel
{
    private readonly SignInManager<IdentityUser> _signInManager;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ITurnstileService _turnstileService;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        ITurnstileService turnstileService,
        IAuditLogService auditLogService,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _turnstileService = turnstileService;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "E-posta adresi veya kullanıcı adı gereklidir.")]
        [Display(Name = "E-posta Adresi")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Parola gereklidir.")]
        [DataType(DataType.Password)]
        [Display(Name = "Parola")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Beni Hatırla")]
        public bool RememberMe { get; set; }

        // Honeypot alanı (Bot tuzağı)
        public string? WebsiteUrl { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        returnUrl ??= Url.Content("~/");

        // Açık Yönlendirme (Open Redirect) Koruması
        if (!Url.IsLocalUrl(returnUrl))
        {
            returnUrl = Url.Content("~/");
        }

        // Mevcut harici çerezleri temizle
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");
        if (!Url.IsLocalUrl(returnUrl))
        {
            returnUrl = Url.Content("~/");
        }

        ReturnUrl = returnUrl;

        // 1. Honeypot Bot Koruması
        if (!string.IsNullOrWhiteSpace(Input.WebsiteUrl))
        {
            _logger.LogWarning("[Auth-Security] Honeypot bot girişimi engellendi. IP: {Ip}", HttpContext.Connection.RemoteIpAddress);
            ModelState.AddModelError(string.Empty, "Güvenlik doğrulaması başarısız oldu.");
            return Page();
        }

        // 2. Cloudflare Turnstile Bot Koruması
        var turnstileToken = Request.Form["cf-turnstile-response"].ToString();
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var isTurnstileValid = await _turnstileService.VerifyTokenAsync(turnstileToken, remoteIp);
        if (!isTurnstileValid)
        {
            _logger.LogWarning("[Auth-Security] Giriş ekranında Turnstile doğrulaması başarısız oldu. IP: {Ip}", remoteIp);
            ModelState.AddModelError(string.Empty, "Güvenlik doğrulaması (Turnstile) başarısız oldu veya süresi doldu. Lütfen tekrar deneyin.");
            return Page();
        }

        if (ModelState.IsValid)
        {
            // E-posta veya kullanıcı adı ile kullanıcıyı bul (Kullanıcı numaralandırma / Enumeration saldırısına karşı tek tip yanıt)
            var normalizedInput = Input.Email.Trim();
            var user = await _userManager.FindByEmailAsync(normalizedInput)
                       ?? await _userManager.FindByNameAsync(normalizedInput);

            var userNameToSignIn = user?.UserName ?? normalizedInput;

            // lockoutOnFailure: true -> Brute-force saldırılarına karşı hesabı kilitler
            var result = await _signInManager.PasswordSignInAsync(
                userNameToSignIn,
                Input.Password,
                Input.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("[Auth-Security] Kullanıcı başarıyla giriş yaptı: {User}, IP: {Ip}", userNameToSignIn, remoteIp);
                await _auditLogService.LogAsync(
                    category: "AdminLoginSuccess",
                    summary: $"Başarılı oturum açma: {userNameToSignIn}",
                    targetEntityType: "IdentityUser",
                    targetEntityId: user?.Id,
                    logLevel: "Security",
                    username: userNameToSignIn,
                    ipAddress: remoteIp,
                    component: "LoginModel");
                return LocalRedirect(returnUrl);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("[Auth-Security] Kullanıcı hesabı çok sayıda başarısız deneme nedeniyle kilitlendi: {User}, IP: {Ip}", userNameToSignIn, remoteIp);
                await _auditLogService.LogAsync(
                    category: "AdminLoginFailed",
                    summary: $"Hesap çok sayıda başarısız deneme nedeniyle kilitlendi: {userNameToSignIn}",
                    targetEntityType: "IdentityUser",
                    targetEntityId: user?.Id,
                    logLevel: "Security",
                    username: userNameToSignIn,
                    ipAddress: remoteIp,
                    component: "LoginModel");
                return RedirectToPage("./Lockout");
            }

            _logger.LogWarning("[Auth-Security] Başarısız giriş denemesi: {User}, IP: {Ip}", normalizedInput, remoteIp);
            await _auditLogService.LogAsync(
                category: "AdminLoginFailed",
                summary: $"Başarısız giriş denemesi: {normalizedInput}",
                targetEntityType: "IdentityUser",
                targetEntityId: user?.Id,
                logLevel: "Security",
                username: normalizedInput,
                ipAddress: remoteIp,
                component: "LoginModel");
            ModelState.AddModelError(string.Empty, "Geçersiz kimlik bilgileri. Lütfen e-posta adresinizi ve parolanızı kontrol edin.");
        }

        return Page();
    }
}
