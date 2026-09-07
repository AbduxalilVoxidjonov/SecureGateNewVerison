using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SecureGate.Domain.Auth;

namespace SecureGate.Data
{
    /// <summary>
    /// Rollarni va birlamchi SuperAdmin akkauntini yaratadi.
    /// Parol HECH QACHON kodda saqlanmaydi — konfiguratsiyadan olinadi
    /// (<c>Seeder:SuperAdminPassword</c> yoki <c>Seeder__SuperAdminPassword</c> env),
    /// berilmagan bo'lsa kriptografik jihatdan xavfsiz parol generatsiya qilinib,
    /// log'ga BIR MARTA chiqariladi.
    /// </summary>
    public static class IdentitySeeder
    {
        /// <summary>Konfiguratsiyada email berilmasa ishlatiladigan qiymat.</summary>
        public const string DefaultSuperAdminEmail = "superadmin@securegate.local";

        /// <summary>appsettings: "Seeder:SuperAdminEmail" / env: Seeder__SuperAdminEmail</summary>
        public const string SuperAdminEmailConfigKey = "Seeder:SuperAdminEmail";

        /// <summary>appsettings: "Seeder:SuperAdminPassword" / env: Seeder__SuperAdminPassword</summary>
        public const string SuperAdminPasswordConfigKey = "Seeder:SuperAdminPassword";

        public static async Task SeedAsync(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IConfiguration configuration,
            ILogger? logger = null)
        {
            ArgumentNullException.ThrowIfNull(userManager);
            ArgumentNullException.ThrowIfNull(roleManager);
            ArgumentNullException.ThrowIfNull(configuration);

            // 1) Rollarni yaratish
            foreach (var role in new[] { Roles.SuperAdmin, Roles.Admin })
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                    if (!roleResult.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"'{role}' rolini yaratib bo'lmadi: {Describe(roleResult)}");
                    }
                }
            }

            // 2) Idempotentlik EMAIL bo'yicha emas, ROL bo'yicha tekshiriladi.
            //    Aks holda mavjud SuperAdmin emailini o'zgartirgach, seeder eski
            //    email bilan yangi "backdoor" akkaunt yaratib qo'yardi.
            var existingSuperAdmins = await userManager.GetUsersInRoleAsync(Roles.SuperAdmin);
            if (existingSuperAdmins.Count > 0)
            {
                return;
            }

            var email = configuration[SuperAdminEmailConfigKey];
            if (string.IsNullOrWhiteSpace(email))
            {
                email = DefaultSuperAdminEmail;
            }
            email = email.Trim();

            // Rol egasi yo'q, lekin shu emailli foydalanuvchi bor bo'lsa — yangi akkaunt
            // yaratmaymiz, mavjudiga rolni qaytaramiz.
            var existingByEmail = await userManager.FindByEmailAsync(email);
            if (existingByEmail != null)
            {
                var repair = await userManager.AddToRoleAsync(existingByEmail, Roles.SuperAdmin);
                if (!repair.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Mavjud '{email}' foydalanuvchisiga SuperAdmin roli berilmadi: {Describe(repair)}");
                }

                logger?.LogWarning(
                    "SuperAdmin roli mavjud '{Email}' foydalanuvchisiga qayta biriktirildi.", email);
                return;
            }

            var password = configuration[SuperAdminPasswordConfigKey];
            var passwordWasGenerated = false;
            if (string.IsNullOrWhiteSpace(password))
            {
                password = GenerateStrongPassword();
                passwordWasGenerated = true;
            }

            var superAdmin = new AppUser
            {
                UserName = email,
                Email = email,
                FullName = "Super Admin",
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await userManager.CreateAsync(superAdmin, password);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    "SuperAdmin seed muvaffaqiyatsiz: " + Describe(result));
            }

            var addToRole = await userManager.AddToRoleAsync(superAdmin, Roles.SuperAdmin);
            if (!addToRole.Succeeded)
            {
                throw new InvalidOperationException(
                    "SuperAdmin'ga rol berish muvaffaqiyatsiz: " + Describe(addToRole));
            }

            if (passwordWasGenerated)
            {
                WriteOneTimePasswordBanner(logger, email, password);
            }
            else
            {
                logger?.LogInformation(
                    "SuperAdmin akkaunti '{Email}' konfiguratsiyadagi parol bilan yaratildi.", email);
            }
        }

        private static string Describe(IdentityResult result) =>
            string.Join("; ", result.Errors.Select(e => e.Description));

        // Identity'ning standart parol siyosatiga kafolatlangan mos keladi:
        // katta harf + kichik harf + raqam + maxsus belgi, uzunlik 20.
        private static string GenerateStrongPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";   // I, O tushirilgan (chalkashmasin)
            const string lower = "abcdefghijkmnopqrstuvwxyz";  // l tushirilgan
            const string digits = "23456789";                  // 0, 1 tushirilgan
            const string special = "!@#$%^&*()-_=+?";
            const string all = upper + lower + digits + special;
            const int length = 20;

            var chars = new char[length];
            chars[0] = Pick(upper);
            chars[1] = Pick(lower);
            chars[2] = Pick(digits);
            chars[3] = Pick(special);
            for (var i = 4; i < length; i++)
            {
                chars[i] = Pick(all);
            }

            // Fisher-Yates — kafolatlangan belgilar boshida turib qolmasligi uchun
            for (var i = chars.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);

            static char Pick(string source) => source[RandomNumberGenerator.GetInt32(source.Length)];
        }

        private static void WriteOneTimePasswordBanner(ILogger? logger, string email, string password)
        {
            var banner = new StringBuilder()
                .AppendLine()
                .AppendLine("============================================================")
                .AppendLine("  SUPERADMIN AKKAUNTI YARATILDI")
                .AppendLine("------------------------------------------------------------")
                .AppendLine($"  Email : {email}")
                .AppendLine($"  Parol : {password}")
                .AppendLine("------------------------------------------------------------")
                .AppendLine("  BU PAROLNI HOZIROQ SAQLANG — U BOSHQA KO'RSATILMAYDI!")
                .AppendLine("  Parolni oldindan belgilash uchun konfiguratsiyaga qo'ying:")
                .AppendLine($"    {SuperAdminPasswordConfigKey}  (env: Seeder__SuperAdminPassword)")
                .AppendLine("============================================================")
                .ToString();

            if (logger != null)
            {
                logger.LogWarning("{Banner}", banner);
            }
            else
            {
                Console.WriteLine(banner);
            }
        }
    }
}
