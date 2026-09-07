using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SecureGate.Api.Models;

namespace SecureGate.Api.Filters
{
    /// <summary>
    /// Ma'lumotlar bazasi butunligi (integrity) buzilishini tushunarli javobga aylantiradi.
    ///
    /// Muammo: unique indeks buzilganda EF Core <see cref="DbUpdateException"/> tashlaydi,
    /// uning ichida <see cref="SqlException"/> (Number = 2601/2627) yotadi. Hech kim uni
    /// ushlamagani uchun klient bo'sh body bilan HTTP 500 olardi.
    ///
    /// Bu global filtr SQL Server xato matnidan indeks nomini ajratib olib, uni aniq
    /// o'zbekcha xabarga map qiladi va 409 Conflict qaytaradi. Har bir controllerni
    /// alohida o'zgartirish shart emas.
    ///
    /// MUHIM: filtr FAQAT o'ziga tegishli xatolarni ushlaydi. Boshqa har qanday istisno
    /// tegilmagan holda yuqoriga o'tadi va odatdagidek <c>UseExceptionHandler("/error")</c>
    /// tomonidan loglanadi.
    /// </summary>
    public sealed class DbConstraintExceptionFilter : IExceptionFilter
    {
        // ===== SQL Server xato raqamlari =====
        private const int UniqueIndexViolation = 2601;      // Cannot insert duplicate key row ... with unique index '...'
        private const int UniqueConstraintViolation = 2627; // Violation of UNIQUE KEY constraint '...'
        private const int ForeignKeyViolation = 547;        // DELETE/INSERT ... conflicted with ... constraint '...'

        private const string DuplicateFallbackMessage = "Bunday yozuv allaqachon mavjud.";

        private const string ForeignKeyDeleteMessage =
            "Bu yozuv boshqa ma'lumotlar bilan bog'langan — avval ularni o'chiring.";

        private const string ForeignKeyInsertMessage =
            "Bog'lanmoqchi bo'lgan yozuv topilmadi yoki o'chirilgan.";

        /// <summary>
        /// Indeks nomi → foydalanuvchiga ko'rsatiladigan xabar.
        /// Ro'yxat <c>AppDbContext.OnModelCreating</c> va migratsiyalardagi
        /// unique indekslardan olingan. Yangi unique indeks qo'shsangiz — shu yerga bitta qator qo'shing.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string> IndexMessages =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // ===== Cameras =====
                ["IX_Cameras_IpAddress_Port"] =
                    "Bu IP manzil va port bilan kamera allaqachon ro'yxatga olingan.",
                ["IX_Cameras_IpAddress_Port_ChannelNumber"] =
                    "Bu NVR ning ushbu kanali allaqachon qo'shilgan.",
                ["IX_Cameras_CameraCode"] =
                    "Bu kamera kodi allaqachon band.",

                // ===== Turnstiles =====
                ["IX_Turnstiles_Name"] =
                    "Bu nomdagi turniket allaqachon mavjud.",
                ["IX_Turnstiles_IpAddress_Port"] =
                    "Bu IP manzil va port bilan turniket allaqachon ro'yxatga olingan.",

                // ===== FaceData (bir shaxsga bitta yuz profili) =====
                ["IX_FaceData_StudentId"] =
                    "Bu o'quvchining yuz profili allaqachon mavjud.",
                ["IX_FaceData_TeacherId"] =
                    "Bu o'qituvchining yuz profili allaqachon mavjud.",
                ["IX_FaceData_StaffId"] =
                    "Bu xodimning yuz profili allaqachon mavjud.",

                // ===== People =====
                // Users entity "Students" jadvaliga yoziladi.
                ["IX_Students_StudentId"] =
                    "Bu o'quvchi ID raqami allaqachon band.",
                ["IX_BlockedUsers_StudentId"] =
                    "Bu o'quvchi allaqachon bloklangan.",

                // ===== Sozlamalar va ruxsatlar =====
                ["IX_Settings_Key"] =
                    "Bu sozlama kaliti allaqachon mavjud.",
                ["IX_UserPermissions_UserId_Permission"] =
                    "Bu foydalanuvchiga ushbu ruxsat allaqachon berilgan.",
                ["IX_UserCameraGroups_UserId_CameraGroupId"] =
                    "Bu admin uchun ushbu kamera guruhi allaqachon biriktirilgan.",

                // ===== ASP.NET Identity (nomlari EF konvensiyasidan farq qiladi) =====
                ["UserNameIndex"] =
                    "Bu login allaqachon band.",
                ["RoleNameIndex"] =
                    "Bu nomdagi rol allaqachon mavjud."
            };

        // 2601: ... in object 'dbo.Cameras' with unique index 'IX_Cameras_IpAddress_Port'.
        private static readonly Regex UniqueIndexNameRegex = new(
            @"unique index '([^']+)'",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(200));

        // 2627: Violation of UNIQUE KEY constraint 'IX_Turnstiles_Name'. ...
        private static readonly Regex ConstraintNameRegex = new(
            @"constraint '([^']+)'",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(200));

        private readonly ILogger<DbConstraintExceptionFilter> _logger;

        public DbConstraintExceptionFilter(ILogger<DbConstraintExceptionFilter> logger)
            => _logger = logger;

        public void OnException(ExceptionContext context)
        {
            var sql = FindSqlException(context.Exception);
            if (sql is null)
            {
                return; // Bizga tegishli emas — istisno yuqoriga o'tsin.
            }

            string message;
            int statusCode;

            switch (sql.Number)
            {
                case UniqueIndexViolation:
                case UniqueConstraintViolation:
                    var indexName = ExtractIndexName(sql.Message);
                    message = indexName is not null && IndexMessages.TryGetValue(indexName, out var mapped)
                        ? mapped
                        : DuplicateFallbackMessage;
                    statusCode = StatusCodes.Status409Conflict;

                    // Xom SQL matni foydalanuvchiga KO'RSATILMAYDI (jadval/ustun nomlari sizib
                    // chiqmasligi uchun), lekin diagnostika uchun to'liq loglanadi.
                    _logger.LogWarning(context.Exception,
                        "Unique constraint buzilishi. Indeks: {IndexName}, SQL xato: {SqlNumber}, Yo'l: {Path}",
                        indexName ?? "(aniqlanmadi)", sql.Number, context.HttpContext.Request.Path);
                    break;

                case ForeignKeyViolation:
                    // DELETE holati: "conflicted with the REFERENCE constraint" — bola qatorlar bor.
                    // INSERT/UPDATE holati: "conflicted with the FOREIGN KEY constraint" — ota qator yo'q.
                    var isDeleteConflict = sql.Message.Contains(
                        "REFERENCE constraint", StringComparison.OrdinalIgnoreCase);

                    message = isDeleteConflict ? ForeignKeyDeleteMessage : ForeignKeyInsertMessage;
                    statusCode = isDeleteConflict
                        ? StatusCodes.Status409Conflict
                        : StatusCodes.Status400BadRequest;

                    _logger.LogWarning(context.Exception,
                        "Foreign key buzilishi ({Kind}). Constraint: {ConstraintName}, Yo'l: {Path}",
                        isDeleteConflict ? "DELETE" : "INSERT/UPDATE",
                        ExtractConstraintName(sql.Message) ?? "(aniqlanmadi)",
                        context.HttpContext.Request.Path);
                    break;

                default:
                    return; // Boshqa SQL xatolari (timeout, deadlock, ulanish) — bizga tegishli emas.
            }

            context.Result = new ObjectResult(ApiResponse.Fail(message))
            {
                StatusCode = statusCode
            };
            context.ExceptionHandled = true;
        }

        /// <summary>
        /// Istisno zanjiridan <see cref="SqlException"/> ni topadi.
        /// Odatda EF Core uni <see cref="DbUpdateException"/> ichiga o'raydi, ammo
        /// ExecuteUpdate/ExecuteDelete va Identity chaqiruvlari uni to'g'ridan-to'g'ri ham tashlaydi.
        /// </summary>
        private static SqlException? FindSqlException(Exception? exception)
        {
            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (current is SqlException sql)
                {
                    return sql;
                }
            }

            return null;
        }

        private static string? ExtractIndexName(string sqlMessage)
            => Match(UniqueIndexNameRegex, sqlMessage) ?? Match(ConstraintNameRegex, sqlMessage);

        private static string? ExtractConstraintName(string sqlMessage)
            => Match(ConstraintNameRegex, sqlMessage);

        private static string? Match(Regex regex, string input)
        {
            try
            {
                var match = regex.Match(input);
                return match.Success ? match.Groups[1].Value : null;
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        }
    }
}
