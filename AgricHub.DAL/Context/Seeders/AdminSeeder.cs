using AgricHub.DAL.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AgricHub.DAL.Context.Seeders
{
    public static class AdminSeeder
    {
        public static async Task SeedAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

            // Same roles as DataSeeder.SeedRoles
            foreach (var role in new[] { "Admin", "Consultant", "Customer" })
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Seed admin account — skips if already present
            const string adminEmail = "admin@agrichub.io";
            const string adminUserName = "agrichub_admin";
            const string adminPassword = "Admin@AgricHub2026!";

            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    Id                 = Guid.NewGuid().ToString(),
                    UserName           = adminUserName,
                    NormalizedUserName = adminUserName.ToUpper(),
                    Email              = adminEmail,
                    NormalizedEmail    = adminEmail.ToUpper(),
                    EmailConfirmed     = true,
                    FirstName          = "Platform",
                    LastName           = "Admin",
                    CountryId          = "NG",
                    StateId            = "LA",
                    Address            = "AgricHub HQ",
                };

                var result = await userManager.CreateAsync(admin, adminPassword);
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}