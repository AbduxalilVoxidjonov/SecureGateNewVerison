# 🛡️ SecureGate

**Yuz tanish (face recognition) asosidagi kirish-chiqish boshqaruv tizimi.**
IP-kameralar va turniketlarni real vaqtda kuzatadi, yuzlarni aniqlaydi, ruxsatlarni boshqaradi va kirish-chiqishlarni jurnalga yozadi.

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![React](https://img.shields.io/badge/React-19-61DAFB)
![Vite](https://img.shields.io/badge/Vite-8-646CFF)
![EF Core](https://img.shields.io/badge/EF%20Core-8-512BD4)
![SignalR](https://img.shields.io/badge/SignalR-realtime-FF6F61)

---

## ✨ Asosiy imkoniyatlar

- **🎥 Kameralar** — qo'shish, tahrirlash, o'chirish; **jonli video** (MJPEG oqim + grid uchun snapshot); saqlashdan oldin **"Test ulanib ko'rish"**.
- **🚪 Turniketlar** — ochish / yopish / bloklash, favqulodda ("EMERGENCY") barchasini ochish, ulanishni test qilish.
- **🧠 Yuz tanish** — RTSP oqimdan real vaqtda yuzlarni aniqlash (SCRFD), tanish bazasi bilan solishtirish (ArcFace) va turniketni avtomatik ochish.
- **👤 Foydalanuvchilar va rahbariyat** — yuz rasmi bilan ro'yxatga olish, bloklash/blokdan chiqarish.
- **🗂️ Kamera guruhlari va adminlar** — adminlarga faqat o'ziga biriktirilgan guruhlarni ko'rsatish.
- **📊 Hisobotlar va kirish jurnali** — kirish-chiqish statistikasi, ruxsat/rad yozuvlari.
- **🔐 Rollar va huquqlar** — permission asosidagi avtorizatsiya (JWT + Identity).
- **🌓 Yorug'/Qorong'i mavzu** — bir bosishda almashadigan theme (localStorage'da saqlanadi).
- **⚡ SignalR** — kamera/turniket/alert hodisalari uchun real-time hublar.

---

## 🏗️ Arxitektura

.NET 8 **Clean Architecture** — 4 ta backend loyiha + React SPA:

```
SecureGate.slnx
├── SecureGate.Domain          # Entity'lar, enum'lar, Permission'lar (bog'liqliksiz yadro)
├── SecureGate.Data            # EF Core + Identity, AppDbContext, migratsiyalar, IdentitySeeder
├── SecureGate.Infrastructure  # Servislar, SignalR hublar, yuz tanish dvigateli, CameraStreamWorker
├── SecureGate.Server          # ASP.NET Core Web API — controllerlar, Program.cs, JWT
└── securegate.client          # React 19 + Vite SPA (frontend)
```

**Bog'liqlik oqimi:** `Domain → Data → Infrastructure → Server`

---

## 🧰 Texnologiyalar

| Qatlam | Texnologiyalar |
|--------|----------------|
| Backend | .NET 8, ASP.NET Core Web API, EF Core 8, ASP.NET Identity, SignalR, JWT |
| Ma'lumotlar bazasi | SQL Server (LocalDB) |
| Yuz tanish | FaceAiSharp + FaceAiSharp.Bundle (ONNX **SCRFD** + **ArcFace**), OpenCvSharp4 |
| Frontend | React 19, Vite, Fetch API, CSS o'zgaruvchilari (theming) |
| Hujjat | Swagger / OpenAPI |

---

## ✅ Talablar

- **[.NET 8 SDK](https://dotnet.microsoft.com/download)**
- **[Node.js 18+](https://nodejs.org/)** (frontend uchun)
- **SQL Server LocalDB** (Visual Studio yoki [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads) bilan keladi)
- **Windows** — `OpenCvSharp4.runtime.win` (native RTSP) va LocalDB Windows uchun mo'ljallangan. Boshqa O'T'da SQL Server ulanish satrini almashtirish kerak.

---

## 🚀 Ishga tushirish

Backend va frontend **ikkalasini** ham ishga tushiring (alohida terminallarda).

### 1) Backend (API)

```bash
dotnet run --project SecureGate.Server
```

- API: **https://localhost:7194**
- Swagger: **https://localhost:7194/swagger**
- Birinchi ishga tushganda avtomatik: `Database.Migrate()` (migratsiyalar) + **SuperAdmin** seed qilinadi.

### 2) Frontend (SPA)

```bash
cd securegate.client
npm install
npm run dev
```

- Frontend: **https://localhost:51985**
- Vite `/api` va `/hubs` (WebSocket) so'rovlarini backendga proxy qiladi — brauzer bitta origin'da ishlaydi.

---

## 🔑 Standart hisob

Birinchi ishga tushganda quyidagi SuperAdmin yaratiladi:

| Email | Parol |
|-------|-------|
| `superadmin@securegate.local` | `ChangeMe123!` |

> ⚠️ Ishlab chiqarish (production) muhitida parolni va `appsettings.json` dagi JWT kalitini **albatta o'zgartiring**.

---

## ⚙️ Konfiguratsiya

Asosiy sozlamalar `SecureGate.Server/appsettings.json` da:

```jsonc
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=SecureGateDb;Trusted_Connection=True;..."
  },
  "Jwt": {
    "Issuer": "SecureGate.Api",
    "Audience": "SecureGate.Clients",
    "Key": "...ChangeMe...",         // production'da o'zgartiring
    "AccessTokenMinutes": 60
  },
  "Cors": {
    "AllowedOrigins": [ "https://localhost:51985" ]
  }
}
```

Yuz tanish sozlamalari (ixtiyoriy, `FaceRecognition:` bo'limi): `MinSimilarity`, `DetectionIntervalMs`, `CameraRefreshSeconds` va h.k.

---

## 🎥 Kamera oqimi haqida

- **Grid** (kameralar ro'yxati) har bir kamera uchun har ~6 soniyada **snapshot** (bitta kadr) yangilab turadi.
- **Detail modal** — to'liq **jonli MJPEG** oqim (`/api/cameras/{id}/stream`).
- `<img>` header yubora olmagani uchun JWT token `?access_token=` query orqali uzatiladi.
- Video faqat **tarmoqda mavjud, ulanadigan RTSP kamera** bo'lsa ko'rinadi. Kamera ulanmasa, "Test ulanib ko'rish" tugmasi sababini ko'rsatadi (port yopiq, login/parol xato va h.k.).

---

## 🗃️ Ma'lumotlar bazasi (EF Core)

Migratsiyalar `SecureGate.Data/Migrations` da. Yangi migratsiya qo'shish:

```bash
dotnet ef migrations add <Nom> --project SecureGate.Data --startup-project SecureGate.Server
dotnet ef database update --project SecureGate.Data --startup-project SecureGate.Server
```

> Ilova ishga tushganda migratsiyalar avtomatik qo'llanadi, shuning uchun odatda qo'lda `database update` shart emas.

---

## ⚠️ Muhim eslatma (ImageSharp litsenziyasi)

`SixLabors.ImageSharp` **3.1.10** versiyasida ushlab turilgan. **4.x** versiya pullik tijoriy litsenziyani talab qiladi va build'ni buzadi — shuning uchun yangilamang.

---

## 📁 Loyiha tuzilishi (qisqacha)

```
SecureGate.Server/
├── Controllers/        # Cameras, Turnstiles, Users, Auth, Reports, ...
├── Program.cs          # DI, auth, CORS, SignalR, seed
└── appsettings.json

SecureGate.Infrastructure/
├── Services/           # CameraService, TurnstileService, DeviceConnectionTester, CameraMjpegStreamer, ...
├── Hubs/               # CameraHub, TurnstileHub, AlertHub, DashboardHub
└── ...                 # FaceRecognitionEngine, CameraStreamWorker, KnownFaceCache

securegate.client/src/
├── api/                # client.js (fetch wrapper), endpoints.js
├── auth/               # AuthContext (JWT)
├── components/         # ui.jsx, Icon.jsx, state.jsx
├── screens/            # dashboard, cameras, turnstiles, users, reports, ...
└── theme.js            # yorug'/qorong'i mavzu
```

---

## 📜 Litsenziya

Ichki/xususiy loyiha. Litsenziya shartlari egasi tomonidan belgilanadi.
