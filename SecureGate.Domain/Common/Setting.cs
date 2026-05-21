using System.ComponentModel.DataAnnotations;

namespace SecureGate.Domain.Common
{

    // ==================== SETTING ====================
    public class Setting
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Key { get; set; } = string.Empty;

        public string? Value { get; set; }

        [Display(Name = "Tavsif")]
        public string? Description { get; set; }

        [Display(Name = "Tur")]
        public SettingType Type { get; set; } = SettingType.Boolean;
    }

    public enum SettingType
    {
        Boolean,
        String,
        Integer,
        Select
    }
}
