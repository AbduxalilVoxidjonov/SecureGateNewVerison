using Microsoft.AspNetCore.Identity;
using SecureGate.Domain.Auth;

namespace SecureGate.Data
{
    public static class IdentitySeeder
    {
        // Har bir deployment o'zining default SuperAdmin akkauntiga ega bo'ladi.
        // Birinchi kirishdan keyin parolni almashtirish tavsiya etiladi.
        public const string DefaultSuperAdminEmail = "superadmin@securegate.local";
        public const string DefaultSuperAdminPassword = "ChangeMe123!";

        public static async Task SeedAsync(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // 1) Rollarni yaratish
            foreach (var role in new[] { Roles.SuperAdmin, Roles.Admin })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2) Default SuperAdmin akkaunti
            var superAdmin = await userManager.FindByEmailAsync(DefaultSuperAdminEmail);
            if (superAdmin == null)
            {
                superAdmin = new AppUser
                {
                    UserName = DefaultSuperAdminEmail,
                    Email = DefaultSuperAdminEmail,
                    FullName = "Super Admin",
                    EmailConfirmed = true,
                    IsActive = true
                };

                var result = await userManager.CreateAsync(superAdmin, DefaultSuperAdminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(superAdmin, Roles.SuperAdmin);
                }
            }
            else if (!await userManager.IsInRoleAsync(superAdmin, Roles.SuperAdmin))
            {
                await userManager.AddToRoleAsync(superAdmin, Roles.SuperAdmin);
            }
        }
    }
}
