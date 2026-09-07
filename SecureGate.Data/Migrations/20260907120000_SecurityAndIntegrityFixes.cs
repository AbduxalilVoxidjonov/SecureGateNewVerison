using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureGate.Data.Migrations
{
    /// <summary>
    /// Xavfsizlik va ma'lumot yaxlitligi tuzatishlari:
    ///  - AccessLog -> Teacher/Staff/Camera FK'lari SET NULL (shaxs/kamera o'chirilishi bloklanmaydi)
    ///  - TurnstilePermission va FaceData -> shaxs FK'lari CASCADE (yetim yozuv qolmaydi)
    ///  - BlockedUsers -> Students CASCADE, AspNetUsers -> StaffMembers SET NULL,
    ///    Cameras -> CameraGroups SET NULL
    ///  - Students.StudentId ixtiyoriy (NULL) + filtrlangan unique indeks
    ///  - Hisobot/dashboard uchun indekslar: AccessLogs(Timestamp, Result), Alerts(IsRead, CreatedAt)
    ///  - Turniket/kamera identifikatorlari uchun unique indekslar
    ///  - nvarchar(max) ustunlarga real MaxLength berildi
    /// </summary>
    /// <inheritdoc />
    public partial class SecurityAndIntegrityFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ===================== 1) Eski FK'larni olib tashlash =====================
            migrationBuilder.DropForeignKey(
                name: "FK_AccessLogs_Cameras_CameraId",
                table: "AccessLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AccessLogs_StaffMembers_StaffId",
                table: "AccessLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AccessLogs_Teacher_TeacherId",
                table: "AccessLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_StaffMembers_StaffId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_BlockedUsers_Students_StudentId",
                table: "BlockedUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Cameras_CameraGroups_CameraGroupId",
                table: "Cameras");

            migrationBuilder.DropForeignKey(
                name: "FK_FaceData_StaffMembers_StaffId",
                table: "FaceData");

            migrationBuilder.DropForeignKey(
                name: "FK_FaceData_Students_StudentId",
                table: "FaceData");

            migrationBuilder.DropForeignKey(
                name: "FK_FaceData_Teacher_TeacherId",
                table: "FaceData");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnstilePermissions_StaffMembers_StaffId",
                table: "TurnstilePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnstilePermissions_Students_StudentId",
                table: "TurnstilePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnstilePermissions_Teacher_TeacherId",
                table: "TurnstilePermissions");

            // ===================== 2) Qayta yaratiladigan indekslar ===================
            migrationBuilder.DropIndex(
                name: "IX_FaceData_StaffId",
                table: "FaceData");

            migrationBuilder.DropIndex(
                name: "IX_Students_StudentId",
                table: "Students");

            // ===================== 3) Ustun turlarini toraytirish =====================
            migrationBuilder.AlterColumn<string>(
                name: "Details",
                table: "AccessLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CapturedImagePath",
                table: "AccessLogs",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "BlockedUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Duration",
                table: "BlockedUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BlockedBy",
                table: "BlockedUsers",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Cameras",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StreamUrl",
                table: "Cameras",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // DataProtection ciphertext (base64) saqlanadi — zaxira bilan 1000.
            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Cameras",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "Cameras",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AiStreamUrl",
                table: "Cameras",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                table: "FaceData",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "Settings",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Settings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoPath",
                table: "StaffMembers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "StaffMembers",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StudentId",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoPath",
                table: "Students",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ParentPhone",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Students",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoPath",
                table: "Teacher",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Teacher",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Teacher",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Uptime",
                table: "Turnstiles",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Turnstiles",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "Turnstiles",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            // Bo'sh satrlar filtrlangan unique indeksda to'qnashmasligi uchun NULL'ga o'tkaziladi.
            migrationBuilder.Sql(
                "UPDATE [Students] SET [StudentId] = NULL WHERE [StudentId] IS NOT NULL AND LTRIM(RTRIM([StudentId])) = N'';");

            // ===================== 4) Yangi indekslar ================================
            migrationBuilder.CreateIndex(
                name: "IX_AccessLogs_Timestamp_Result",
                table: "AccessLogs",
                columns: new[] { "Timestamp", "Result" });

            migrationBuilder.CreateIndex(
                name: "IX_Alerts_IsRead_CreatedAt",
                table: "Alerts",
                columns: new[] { "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_IpAddress_Port",
                table: "Cameras",
                columns: new[] { "IpAddress", "Port" },
                unique: true,
                filter: "[IpAddress] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FaceData_StaffId",
                table: "FaceData",
                column: "StaffId",
                unique: true,
                filter: "[StaffId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentId",
                table: "Students",
                column: "StudentId",
                unique: true,
                filter: "[StudentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Turnstiles_Name",
                table: "Turnstiles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Turnstiles_IpAddress_Port",
                table: "Turnstiles",
                columns: new[] { "IpAddress", "Port" },
                unique: true,
                filter: "[IpAddress] IS NOT NULL");

            // ===================== 5) FK'larni yangi xatti-harakat bilan qayta yaratish
            migrationBuilder.AddForeignKey(
                name: "FK_AccessLogs_Cameras_CameraId",
                table: "AccessLogs",
                column: "CameraId",
                principalTable: "Cameras",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AccessLogs_StaffMembers_StaffId",
                table: "AccessLogs",
                column: "StaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AccessLogs_Teacher_TeacherId",
                table: "AccessLogs",
                column: "TeacherId",
                principalTable: "Teacher",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_StaffMembers_StaffId",
                table: "AspNetUsers",
                column: "StaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BlockedUsers_Students_StudentId",
                table: "BlockedUsers",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Cameras_CameraGroups_CameraGroupId",
                table: "Cameras",
                column: "CameraGroupId",
                principalTable: "CameraGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FaceData_StaffMembers_StaffId",
                table: "FaceData",
                column: "StaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FaceData_Students_StudentId",
                table: "FaceData",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FaceData_Teacher_TeacherId",
                table: "FaceData",
                column: "TeacherId",
                principalTable: "Teacher",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TurnstilePermissions_StaffMembers_StaffId",
                table: "TurnstilePermissions",
                column: "StaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TurnstilePermissions_Students_StudentId",
                table: "TurnstilePermissions",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TurnstilePermissions_Teacher_TeacherId",
                table: "TurnstilePermissions",
                column: "TeacherId",
                principalTable: "Teacher",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AccessLogs_Cameras_CameraId",
                table: "AccessLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AccessLogs_StaffMembers_StaffId",
                table: "AccessLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AccessLogs_Teacher_TeacherId",
                table: "AccessLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_StaffMembers_StaffId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_BlockedUsers_Students_StudentId",
                table: "BlockedUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Cameras_CameraGroups_CameraGroupId",
                table: "Cameras");

            migrationBuilder.DropForeignKey(
                name: "FK_FaceData_StaffMembers_StaffId",
                table: "FaceData");

            migrationBuilder.DropForeignKey(
                name: "FK_FaceData_Students_StudentId",
                table: "FaceData");

            migrationBuilder.DropForeignKey(
                name: "FK_FaceData_Teacher_TeacherId",
                table: "FaceData");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnstilePermissions_StaffMembers_StaffId",
                table: "TurnstilePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnstilePermissions_Students_StudentId",
                table: "TurnstilePermissions");

            migrationBuilder.DropForeignKey(
                name: "FK_TurnstilePermissions_Teacher_TeacherId",
                table: "TurnstilePermissions");

            migrationBuilder.DropIndex(
                name: "IX_AccessLogs_Timestamp_Result",
                table: "AccessLogs");

            migrationBuilder.DropIndex(
                name: "IX_Alerts_IsRead_CreatedAt",
                table: "Alerts");

            migrationBuilder.DropIndex(
                name: "IX_Cameras_IpAddress_Port",
                table: "Cameras");

            migrationBuilder.DropIndex(
                name: "IX_FaceData_StaffId",
                table: "FaceData");

            migrationBuilder.DropIndex(
                name: "IX_Students_StudentId",
                table: "Students");

            migrationBuilder.DropIndex(
                name: "IX_Turnstiles_IpAddress_Port",
                table: "Turnstiles");

            migrationBuilder.DropIndex(
                name: "IX_Turnstiles_Name",
                table: "Turnstiles");

            // StudentId yana NOT NULL bo'ladi — NULL qiymatlar bo'sh satrga qaytariladi.
            migrationBuilder.Sql(
                "UPDATE [Students] SET [StudentId] = N'' WHERE [StudentId] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Details",
                table: "AccessLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CapturedImagePath",
                table: "AccessLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FullName",
                table: "AspNetUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "BlockedUsers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Duration",
                table: "BlockedUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "BlockedBy",
                table: "BlockedUsers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Username",
                table: "Cameras",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StreamUrl",
                table: "Cameras",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Cameras",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "Cameras",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(45)",
                oldMaxLength: 45,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AiStreamUrl",
                table: "Cameras",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ImagePath",
                table: "FaceData",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "Settings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Settings",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoPath",
                table: "StaffMembers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "StaffMembers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "StudentId",
                table: "Students",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoPath",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ParentPhone",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Address",
                table: "Students",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "PhotoPath",
                table: "Teacher",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "Teacher",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Teacher",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Uptime",
                table: "Turnstiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "Location",
                table: "Turnstiles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IpAddress",
                table: "Turnstiles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(45)",
                oldMaxLength: 45,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FaceData_StaffId",
                table: "FaceData",
                column: "StaffId");

            migrationBuilder.CreateIndex(
                name: "IX_Students_StudentId",
                table: "Students",
                column: "StudentId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AccessLogs_Cameras_CameraId",
                table: "AccessLogs",
                column: "CameraId",
                principalTable: "Cameras",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AccessLogs_StaffMembers_StaffId",
                table: "AccessLogs",
                column: "StaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AccessLogs_Teacher_TeacherId",
                table: "AccessLogs",
                column: "TeacherId",
                principalTable: "Teacher",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_StaffMembers_StaffId",
                table: "AspNetUsers",
                column: "StaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_BlockedUsers_Students_StudentId",
                table: "BlockedUsers",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Cameras_CameraGroups_CameraGroupId",
                table: "Cameras",
                column: "CameraGroupId",
                principalTable: "CameraGroups",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FaceData_StaffMembers_StaffId",
                table: "FaceData",
                column: "StaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FaceData_Students_StudentId",
                table: "FaceData",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_FaceData_Teacher_TeacherId",
                table: "FaceData",
                column: "TeacherId",
                principalTable: "Teacher",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnstilePermissions_StaffMembers_StaffId",
                table: "TurnstilePermissions",
                column: "StaffId",
                principalTable: "StaffMembers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnstilePermissions_Students_StudentId",
                table: "TurnstilePermissions",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TurnstilePermissions_Teacher_TeacherId",
                table: "TurnstilePermissions",
                column: "TeacherId",
                principalTable: "Teacher",
                principalColumn: "Id");
        }
    }
}
