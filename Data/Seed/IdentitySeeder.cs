using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoviesMafia.Domain;
using MoviesMafia.Domain.Entities;
using MoviesMafia.Services.Options;

namespace MoviesMafia.Data.Seed;

/// <summary>Applies pending migrations and seeds roles plus the initial admin account.</summary>
public static class IdentitySeeder
{
    public static async Task MigrateAndSeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(ct);

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var admin = sp.GetRequiredService<IOptions<AdminSeedOptions>>().Value;
        if (string.IsNullOrWhiteSpace(admin.UserName) || string.IsNullOrWhiteSpace(admin.Password))
        {
            return; // No admin configured (e.g. fresh checkout without secrets); skip seeding.
        }

        var userManager = sp.GetRequiredService<UserManager<AppUser>>();
        if (await userManager.FindByNameAsync(admin.UserName) is not null)
        {
            return;
        }

        var user = new AppUser
        {
            UserName = admin.UserName,
            Email = admin.Email,
            EmailConfirmed = true,
            LockoutEnabled = false,
            ProfilePictureFileName = admin.ProfilePictureFileName,
        };

        var result = await userManager.CreateAsync(user, admin.Password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, Roles.Admin);
        }
    }
}
