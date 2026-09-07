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

        [StringLength(2000)]
        public string? Value { get; set; }

        [Display(Name = "Tavsif")]
        [StringLength(500)]
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
