using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SecureGate.Data.Migrations
{
    /// <summary>
    /// NVR (Network Video Recorder) kanallarini qo'llab-quvvatlash:
    ///  - Cameras.DeviceKind (int, NOT NULL, default 0 = Camera) — oddiy IP-kamera yoki NVR kanali
    ///  - Cameras.ChannelNumber (int, NULL) — NVR dagi kanal raqami (1 dan boshlab)
    ///  - IX_Cameras_IpAddress_Port filtri qayta yoziladi: endi u faqat DeviceKind = 0 (oddiy
    ///    kamera) qatorlariga tegishli. Bitta NVR ortida bir xil IP:Port da o'nlab kanal
    ///    turishi mumkin, eski filtr esa ikkinchi kanalni qo'shishga yo'l qo'ymas edi.
    ///  - IX_Cameras_IpAddress_Port_ChannelNumber — bitta NVR da bitta kanal ikki marta
    ///    ro'yxatga olinishining oldini oladi.
    /// </summary>
    /// <inheritdoc />
    public partial class NvrChannelSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eski indeks yangi ustunga tayanadigan filtr bilan qayta quriladi,
            // shuning uchun avval olib tashlanadi.
            migrationBuilder.DropIndex(
                name: "IX_Cameras_IpAddress_Port",
                table: "Cameras");

            migrationBuilder.AddColumn<int>(
                name: "ChannelNumber",
                table: "Cameras",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeviceKind",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_IpAddress_Port",
                table: "Cameras",
                columns: new[] { "IpAddress", "Port" },
                unique: true,
                filter: "[IpAddress] IS NOT NULL AND [DeviceKind] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_IpAddress_Port_ChannelNumber",
                table: "Cameras",
                columns: new[] { "IpAddress", "Port", "ChannelNumber" },
                unique: true,
                filter: "[IpAddress] IS NOT NULL AND [ChannelNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Cameras_IpAddress_Port_ChannelNumber",
                table: "Cameras");

            // DeviceKind ustuni filtrda ishlatilgani uchun ustunni tashlashdan oldin
            // indeks olib tashlanadi.
            migrationBuilder.DropIndex(
                name: "IX_Cameras_IpAddress_Port",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "ChannelNumber",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "DeviceKind",
                table: "Cameras");

            migrationBuilder.CreateIndex(
                name: "IX_Cameras_IpAddress_Port",
                table: "Cameras",
                columns: new[] { "IpAddress", "Port" },
                unique: true,
                filter: "[IpAddress] IS NOT NULL");
        }
    }
}
