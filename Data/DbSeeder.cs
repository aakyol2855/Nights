using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nights.Models.Entities;

namespace Nights.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[DbSeeder] Veritabanı migration uygulanırken hata/uyarı oluştu. Devam ediliyor.");
        }

        // ---- Rol Seeding ----
        var roles = new[] { "Admin", "NoteContributor" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (result.Succeeded)
                    logger.LogInformation("[DbSeeder] '{Role}' rolü oluşturuldu.", role);
                else
                    logger.LogError("[DbSeeder] '{Role}' rolü oluşturulamadı: {Errors}",
                        role, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        // ---- Admin Kullanıcı Seeding ----
        const string adminEmail = "admin@nights.local";
        const string adminPassword = "Admin123!*";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new IdentityUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                logger.LogInformation("[DbSeeder] Admin kullanıcısı oluşturuldu: {Email}", adminEmail);
                await userManager.AddToRoleAsync(adminUser, "Admin");
            }
            else
            {
                logger.LogError("[DbSeeder] Admin kullanıcısı oluşturulamadı: {Errors}",
                    string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(adminUser, "Admin"))
            {
                await userManager.AddToRoleAsync(adminUser, "Admin");
                logger.LogInformation("[DbSeeder] Mevcut admin kullanıcısına 'Admin' rolü atandı.");
            }
        }

        // ---- Örnek Proje Seeding (Sadece boşsa) ----
        if (!await context.Projects.AnyAsync())
        {
            context.Projects.Add(new Project
            {
                Title = "DİTİB Yönetim Sistemi",
                Summary = "Dernek üye yönetimi, görevlendirme ve raporlama platformu.",
                Description = "ASP.NET Core 10 MVC ile geliştirilmiş, MSSQL destekli kapsamlı dernek yönetim sistemi.",
                BadgeText = "Canlı Demo",
                TechStack = ".NET 10, C#, EF Core, MSSQL, ASP.NET MVC",
                IsActive = true,
                IsFeatured = true,
                DisplayOrder = 1,
                CreatedAt = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
            logger.LogInformation("[DbSeeder] Örnek proje kaydı eklendi.");
        }
    }
}
