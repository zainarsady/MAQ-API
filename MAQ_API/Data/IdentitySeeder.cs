using Microsoft.AspNetCore.Identity;

namespace MAQ_API.Data;

public static class IdentitySeeder
{
    public static async Task InitializeRoleAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));

        if (await roleManager.RoleExistsAsync(Roles.Admin)) return;

        var result = await roleManager.CreateAsync(new IdentityRole(Roles.Admin));
        foreach (var e in result.Errors) logger.LogError("Role creation failed: {Error}", e.Description);
    }

    public static async Task InitializeAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(IdentitySeeder));

        var email = config["SeedAdmin:Email"];
        var userName = config["SeedAdmin:UserName"];
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("SeedAdmin is not configured; skipping admin seeding.");
            return;
        }

        var admin = await userManager.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new IdentityUser { UserName = userName, Email = email, EmailConfirmed = true };
            var created = await userManager.CreateAsync(admin, password);
            if (!created.Succeeded)
            {
                foreach (var e in created.Errors) logger.LogError("Admin creation failed: {Error}", e.Description);
                return;
            }
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
        {
            var roleResult = await userManager.AddToRoleAsync(admin, Roles.Admin);
            foreach (var e in roleResult.Errors) logger.LogError("Role assignment failed: {Error}", e.Description);
        }
    }
}
