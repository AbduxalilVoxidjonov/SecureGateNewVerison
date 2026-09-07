using SecureGate.Domain;
using SecureGate.Domain.Auth;
using System.ComponentModel.DataAnnotations;

namespace SecureGate.Infrastructure.ViewModels.Admin
{
    public class AdminListItemViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsSuperAdmin { get; set; }
        public int PermissionCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminCreateViewModel
    {
        [Required(ErrorMessage = "Xodimni tanlash majburiy")]
        [Display(Name = "Xodim")]
        public int? StaffId { get; set; }

        [Required(ErrorMessage = "Parol majburiy")]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "Parol kamida 8 belgi bo'lishi kerak")]
        [Display(Name = "Parol")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "Parollar mos kelmadi")]
        [Display(Name = "Parolni tasdiqlash")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Ruxsatlar")]
        public List<Permission> SelectedPermissions { get; set; } = new();

        // Tanlangan kamera guruhlari â€” admin shularni ko'radi.
        // Bo'sh ro'yxat = barcha guruhlar (cheklov qo'yilmagan).
        [Display(Name = "Kamera guruhlari")]
        public List<int> SelectedCameraGroupIds { get; set; } = new();

        // Tanlash uchun ro'yxatlar â€” POST'da to'ldirilmaydi, faqat GET'da ko'rsatiladi
        public List<Staff> AvailableStaff { get; set; } = new();
        public List<CameraGroup> AvailableCameraGroups { get; set; } = new();
    }

    public class AdminEditViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [Display(Name = "To'liq ism")]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Faol")]
        public bool IsActive { get; set; }

        public bool IsSuperAdmin { get; set; }

        [Display(Name = "Ruxsatlar")]
        public List<Permission> SelectedPermissions { get; set; } = new();

        // Tanlangan kamera guruhlari â€” admin shularni ko'radi.
        // Bo'sh ro'yxat = barcha guruhlar (cheklov qo'yilmagan).
        [Display(Name = "Kamera guruhlari")]
        public List<int> SelectedCameraGroupIds { get; set; } = new();

        public List<CameraGroup> AvailableCameraGroups { get; set; } = new();
    }

    public class AdminResetPasswordViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yangi parol majburiy")]
        [DataType(DataType.Password)]
        [MinLength(8, ErrorMessage = "Parol kamida 8 belgi bo'lishi kerak")]
        [Display(Name = "Yangi parol")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "Parollar mos kelmadi")]
        [Display(Name = "Tasdiqlash")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public static class PermissionGroups
    {
        public static IReadOnlyDictionary<string, Permission[]> All { get; } = new Dictionary<string, Permission[]>
        {
            ["Foydalanuvchilar"] = new[] { Permission.UsersView, Permission.UsersManage, Permission.UsersDelete },
            ["Xodimlar"] = new[] { Permission.StaffView, Permission.StaffManage },
            ["Kameralar"] = new[]
            {
                Permission.CameraView, Permission.CameraManage,
                Permission.CameraUserView, Permission.CameraUserManage
            },
            ["Turniketlar"] = new[] { Permission.TurnstileView, Permission.TurnstileManage },
            ["Kuzatuv va hisobot"] = new[] { Permission.AccessLogsView, Permission.RecordingsView, Permission.ReportsView },
            ["Yuzni tanish & Blok"] = new[] { Permission.FaceRecognitionManage, Permission.BlockedManage },
            ["Sozlamalar"] = new[] { Permission.SettingsManage },
        };

        public static string LabelFor(Permission p) => p switch
        {
            Permission.UsersView => "Foydalanuvchilarni ko'rish",
            Permission.UsersManage => "Foydalanuvchilarni boshqarish (yaratish/tahrir/blok)",
            Permission.UsersDelete => "Foydalanuvchilarni o'chirish",
            Permission.StaffView => "Xodimlarni ko'rish",
            Permission.StaffManage => "Xodimlarni boshqarish",
            Permission.CameraView => "Kameralarni ko'rish",
            Permission.CameraManage => "Kameralarni boshqarish",
            Permission.CameraUserView => "Kameradagi odamlarni ko'rish",
            Permission.CameraUserManage => "Kameradagi odamlarni boshqarish",
            Permission.TurnstileView => "Turniketlarni ko'rish",
            Permission.TurnstileManage => "Turniketlarni boshqarish",
            Permission.AccessLogsView => "Kirish jurnali",
            Permission.RecordingsView => "Yozuvlar",
            Permission.ReportsView => "Hisobotlar",
            Permission.FaceRecognitionManage => "Yuzni tanish",
            Permission.BlockedManage => "Bloklanganlar",
            Permission.SettingsManage => "Sozlamalar",
            Permission.AdminsManage => "Adminlarni boshqarish (faqat SuperAdmin)",
            _ => p.ToString()
        };
    }
}
