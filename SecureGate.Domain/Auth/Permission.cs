namespace SecureGate.Domain.Auth
{
    public enum Permission
    {
        UsersView = 1,
        UsersManage = 2,
        UsersDelete = 3,

        StaffView = 10,
        StaffManage = 11,

        CameraView = 20,
        CameraManage = 21,
        CameraUserView = 22,
        CameraUserManage = 23,

        TurnstileView = 30,
        TurnstileManage = 31,

        AccessLogsView = 40,
        RecordingsView = 41,
        ReportsView = 42,

        FaceRecognitionManage = 50,
        BlockedManage = 51,

        SettingsManage = 60,

        AdminsManage = 100
    }

    public static class Roles
    {
        public const string SuperAdmin = "SuperAdmin";
        public const string Admin = "Admin";
    }
}
