using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Nights.Data;
using Nights.Infrastructure.Middleware;
using Nights.Services.Implementations;
using Nights.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı (MSSQL / SQL Server) ve Identity
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=NightsDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// 2. Cloudflare Tunnel Gerçek Ziyaretçi IP Okuyucu (Forwarded Headers)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// 3. İş Servisleri (DI) & Cloudflare Turnstile HTTP İstemcisi
builder.Services.AddHttpClient<ITurnstileService, TurnstileService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddScoped<IMemoryService, MemoryService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<INoteService, NoteService>();
builder.Services.AddScoped<IUserService, UserService>();

// 4. Performans: In-Memory Caching ve Sıkıştırma (Brotli & Gzip)
builder.Services.AddMemoryCache();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// 5. Rate Limiter (Public Upload Güvenliği - IP başına dakikada max 2 istek)
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
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// 6. MVC Controller & Views
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

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

// 9. Strict Güvenlik Header'ları (CSP, Nosniff, Frame-Options)
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "SAMEORIGIN");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://challenges.cloudflare.com; " +
        "frame-src 'self' https://challenges.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self';");
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
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// /Identity/Home veya ambient routing hatalı çağrılarını ana sayfaya yönlendir
app.MapGet("/Identity/Home", () => Results.Redirect("/"));
app.MapGet("/Identity/Home/{*path}", () => Results.Redirect("/"));

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
