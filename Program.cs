using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Nights.Data;
using Nights.Infrastructure.Middleware;
using Nights.Infrastructure.Proxy;
using Nights.Services.Background;
using Nights.Services.Games;
using Nights.Services.Games.Modules;
using Nights.Services.Implementations;
using Nights.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı (MSSQL / SQL Server) ve Identity
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=127.0.0.1,1433;Database=NightsDb;User Id=sa;Password=2855.Giresun;TrustServerCertificate=True;Encrypt=False;Connect Timeout=30;MultipleActiveResultSets=true;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        // Container açılışında veya geçici gecikmelerde otomatik yeniden deneme
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    }));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.User.RequireUniqueEmail = true;

    // Brute-Force & Hesap Kilitleme Koruması
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // Güçlendirilmiş Parola Politikası
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequiredUniqueChars = 3;

    // Rol ve kimlik iddialarının tam uyumluluğu
    options.ClaimsIdentity.RoleClaimType = System.Security.Claims.ClaimTypes.Role;
    options.ClaimsIdentity.UserNameClaimType = System.Security.Claims.ClaimTypes.Name;
    options.ClaimsIdentity.UserIdClaimType = System.Security.Claims.ClaimTypes.NameIdentifier;
    options.ClaimsIdentity.SecurityStampClaimType = "AspNet.Identity.SecurityStamp";
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// Parola Hashleme İş Faktörü (PBKDF2 210.000 İterasyon - OWASP Standartları)
builder.Services.Configure<PasswordHasherOptions>(options =>
{
    options.IterationCount = 210_000;
});

// Oturum Çerezi ve Anahtar Kalıcılığı (Oturumun yeniden girişe düşmesini engeller)
var dataProtectionKeysDir = Path.Combine(builder.Environment.ContentRootPath, "App_Data", "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionKeysDir);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDir))
    .SetApplicationName("NightsApp")
    .SetDefaultKeyLifetime(TimeSpan.FromDays(90));

// Güvenlik Damgası (SecurityStamp) Doğrulayıcı - Yetki/Şifre değişiminde aktif oturumları denetler
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(5);
});

// Anti-Forgery (CSRF) Çerez ve Header Güvenliği
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = ".Nights.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.HeaderName = "RequestVerificationToken";
    options.SuppressXFrameOptionsHeader = false;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".Nights.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.ReturnUrlParameter = "returnUrl";
});

// 2. Cloudflare Tunnel Gerçek Ziyaretçi IP & HTTPS Protokol Okuyucu (Forwarded Headers)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// 3. İş Servisleri (DI) & Cloudflare Turnstile HTTP İstemcisi
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient<ITurnstileService, TurnstileService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IMemoryService, MemoryService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISystemHealthService, SystemHealthService>();
builder.Services.AddScoped<ISettingService, SettingService>();

// 3a. Sistem Denetim Günlüğü, Çöp Kutusu (Soft-Delete) ve Veri Bakım Servisleri
builder.Services.AddSingleton<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<ITrashService, TrashService>();
builder.Services.AddScoped<IMaintenanceService, MaintenanceService>();
builder.Services.AddHostedService<DivanTrashPurgeWorker>();

// 3b. Docker On-Demand Demo Orkestrasyonu, 15 Dk Otomatik Uyku & YARP Reverse Proxy
builder.Services.AddSingleton<IDockerOrchestratorService, DockerOrchestratorService>();
builder.Services.AddHostedService<DemoCleanupHostedService>();
builder.Services.AddHttpForwarder();

// 3c. Modüler Oyunhane (Tak-Çıkar IGameModule, Sudoku & Misafir Oturum Desteği)
builder.Services.AddSingleton<IGameModule, SudokuGameModule>();
builder.Services.AddSingleton<IGameRegistryService, GameRegistryService>();
builder.Services.AddScoped<IGameScoreService, GameScoreService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Nights.GuestSession";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(4);
});

// 4. Performans: In-Memory Caching ve Sıkıştırma (Brotli & Gzip)
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// 5. Rate Limiter (Public Upload & Authentication Brute-Force Güvenliği)
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("upload-policy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Giriş ve Kayıt sayfaları için IP tabanlı Brute-Force / Credential Stuffing koruması
    options.AddPolicy("auth-policy", httpContext =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous-auth",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 2,
                QueueLimit = 0
            }));

    // Canlı Demo başlatma istekleri için IP tabanlı rate-limit (dakikada max 5 deneme)
    options.AddPolicy("demo-start-policy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous-demo",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Oyunhane skor gönderimi için IP tabanlı anti-spam rate-limit (dakikada max 12 gönderim)
    options.AddPolicy("game-score-policy", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous-game",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 12,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// 6. Çok Dilli Altyapı (Localization: tr-TR & en-US) ve MVC Controller & Views
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

var supportedCultures = new[] { new CultureInfo("tr-TR"), new CultureInfo("en-US") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("tr-TR");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider(),
        new QueryStringRequestCultureProvider()
    };
});

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
})
.AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
.AddDataAnnotationsLocalization();

builder.Services.AddRazorPages()
    .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
    .AddDataAnnotationsLocalization();

var app = builder.Build();

// 7. Veritabanı Migrasyon ve Seeding (Admin hesabı, örnek DİTİB projesi)
try
{
    await DbSeeder.SeedAsync(app.Services);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "Veritabanı başlatılırken hata oluştu.");
}

app.UseForwardedHeaders();

// 8. Zero-Trust Tailscale IP Whitelisting Middleware
// Yalnızca 100.64.0.0/10 ve Localhost üzerinden Admin alanına erişilebilir
app.UseMiddleware<TailscaleAdminIpFilterMiddleware>();

// 8b. Canlı Demo Alt Yol (Subpath) Yönlendirme Middleware'i
// Demo iframe içinden gelen kök-göreli CSS/JS/Form isteklerini UseStaticFiles'tan önce /live-demo/{slug}/ altına yönlendirir
app.UseLiveDemoSubpathRouting();

// 9. Strict Güvenlik Header'ları (CSP, Nosniff, Frame-Options, Permissions-Policy)
app.Use(async (context, next) =>
{
    var isLiveDemoProxy = context.Request.Path.StartsWithSegments("/live-demo", StringComparison.OrdinalIgnoreCase);

    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Permitted-Cross-Domain-Policies", "none");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "accelerometer=(), camera=(), geolocation=(), gyroscope=(), magnetometer=(), microphone=(), payment=(), usb=()");

    if (!isLiveDemoProxy)
    {
        context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
        context.Response.Headers.Append("Content-Security-Policy",
            "default-src 'self'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'self'; " +
            "object-src 'none'; " +
            "script-src 'self' 'unsafe-inline' https://challenges.cloudflare.com; " +
            "frame-src 'self' https://challenges.cloudflare.com https:; " +
            "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
            "img-src 'self' data: https:; " +
            "font-src 'self' data: https://fonts.gstatic.com;");
    }
    await next();
});

// 10. Pipeline Yapılandırması
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseResponseCompression();

// Statik Dosyalar (CSS, JS) ve 7 günlük tarayıcı önbelleği
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Cache-Control", "public,max-age=604800");
    }
});

app.UseRouting();
app.UseRequestLocalization();
app.UseRateLimiter();
app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

// /Identity/Home veya ambient routing hatalı çağrılarını ana sayfaya yönlendir
app.MapGet("/Identity/Home", () => Results.Redirect("/"));
app.MapGet("/Identity/Home/{*path}", () => Results.Redirect("/"));

// 10b. YARP Dinamik Canlı Demo Reverse Proxy (/live-demo/{slug}/)
app.MapLiveDemoProxy();

// 11. Gizli Admin Rotası (/yonetim-x9k2m)
app.MapAreaControllerRoute(
    name: "secret_admin_area",
    areaName: "Admin",
    pattern: $"{TailscaleAdminIpFilterMiddleware.SecretAdminPrefix.TrimStart('/')}/{{controller=Dashboard}}/{{action=Index}}/{{id?}}");

// Geriye dönük uyumluluk için standart /Admin rotası (Tailscale middleware arkasında korumalıdır)
app.MapAreaControllerRoute(
    name: "admin_area",
    areaName: "Admin",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}");

// Varsayılan Public Route
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();
