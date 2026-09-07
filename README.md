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
| Konteyner | Docker (multi-stage), Docker Compose, Azure SQL Edge |

---

## ✅ Talablar

- **[.NET 8 SDK](https://dotnet.microsoft.com/download)**
- **[Node.js 18+](https://nodejs.org/)** (frontend uchun)
- **SQL Server LocalDB** (Visual Studio yoki [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads) bilan keladi)
- **OpenCvSharp native runtime** platformaga qarab avtomatik tanlanadi (`SecureGate.Server.csproj` dagi shartli `PackageReference`): Windows → `OpenCvSharp4.runtime.win`, Linux x64 → `OpenCvSharp4.official.runtime.linux-x64`, Linux arm64 → `OpenCvSharp4.runtime.linux-arm64`, macOS → `OpenCvSharp4.runtime.osx.*`.
- LocalDB faqat Windows'da bor. Boshqa O'T'da ulanish satrini almashtiring **yoki** quyidagi [Docker bo'limidan](#-docker-bilan-ishga-tushirish) foydalaning.

---

## 🚀 Ishga tushirish

Backend va frontend **ikkalasini** ham ishga tushiring (alohida terminallarda).

### 1) Backend (API)

> **Birinchi marta — JWT kalitini o'rnating.** `Jwt:Key` endi `appsettings.json` da
> saqlanmaydi (git'ga maxfiy kalit tushmasligi uchun). Kalit yo'q yoki 32 baytdan qisqa
> bo'lsa ilova ataylab ishga tushmaydi:
>
> ```bash
> cd SecureGate.Server
> dotnet user-secrets init
> dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
> ```
>
> Docker yo'lida bu shart emas — `docker-compose.yml` kalitni env orqali beradi.

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

## 🐳 Docker bilan ishga tushirish

Butun stack (**SPA + API + Swagger + SQL Server**) bitta buyruq bilan ko'tariladi.
Lokal mashinada .NET SDK, Node.js yoki LocalDB o'rnatilgan bo'lishi **shart emas** — faqat Docker kerak.

### Talab

- **Docker Desktop** (Compose v2) — macOS (Intel/Apple Silicon), Linux yoki Windows.

### 1) Muhit fayli (ixtiyoriy)

```bash
cp .env.example .env      # parol/JWT kalitini o'zgartirmoqchi bo'lsangiz
```

`.env` bo'lmasa ham ishlaydi — `docker-compose.yml` dagi standart qiymatlar olinadi.

### 2) Ko'tarish

```bash
docker compose up -d --build
```

Birinchi build ~5–10 daqiqa oladi (npm install + NuGet restore + OpenCV/ONNX native paketlar).

### 3) Ochish

| Manzil | Nima |
|--------|------|
| <http://localhost:3333> | React SPA |
| <http://localhost:3333/swagger> | Swagger UI |
| <http://localhost:3333/swagger/v1/swagger.json> | OpenAPI hujjati |
| <http://localhost:3333/api/...> | REST API |
| `localhost:14330` | SQL Server (SSMS / Azure Data Studio uchun, `sa` + `.env` dagi parol) |

> Tashqi port **3333** — konteyner ichida ham `ASPNETCORE_URLS=http://+:3333`, ya'ni 8080 umuman ishlatilmaydi.

### 4) Loglar / to'xtatish

```bash
docker compose logs -f app        # ilova loglari
docker compose logs -f db         # DB loglari
docker compose ps                 # holat + healthcheck

docker compose down               # to'xtatish (ma'lumotlar saqlanadi)
docker compose down -v            # to'xtatish + DB va yuklamalarni O'CHIRISH
```

### Qanday ishlaydi

- **Multi-stage `Dockerfile`:**
  1. `node:22-alpine` — `vite build` bilan React SPA quriladi.
  2. `mcr.microsoft.com/dotnet/sdk:8.0` — `dotnet publish`; SPA natijasi (`dist/`) `SecureGate.Server/wwwroot/` ga ko'chiriladi.
  3. `mcr.microsoft.com/dotnet/aspnet:8.0-noble` — runtime. **Ubuntu 24.04 (noble)** tanlangan, chunki OpenCvSharp native kutubxonasi `GLIBC_2.38` / `GLIBCXX_3.4.32` talab qiladi (Debian 12 da yo'q).
- **SPA fallback:** `app.MapFallbackToFile("index.html")` — React Router yo'llari to'g'ridan-to'g'ri ochilganda ham ishlaydi (`/api`, `/hubs`, `/swagger` buzilmaydi).
- **Ma'lumotlar bazasi:** `mcr.microsoft.com/azure-sql-edge` — `mssql/server` image'i faqat `linux/amd64` bo'lgani uchun Apple Silicon'da Azure SQL Edge ishlatiladi (native `arm64`, T-SQL mos, EF Core `UseSqlServer` o'zgarishsiz).
- **Migratsiya:** ilova startupda `Database.Migrate()` + SuperAdmin seed qiladi; DB tayyor bo'lguncha retry qiladi (`Startup__MigrationRetryCount`).
- **Native kutubxonalar:** `SecureGate.Server.csproj` OS/arxitekturaga qarab mos `OpenCvSharp4.*.runtime.*` paketini tanlaydi; ONNX Runtime native kutubxonasi (`Microsoft.ML.OnnxRuntime`) alohida qo'shilgan, chunki `FaceAiSharp` faqat managed wrapper'ga (`...OnnxRuntime.Managed`) bog'liq.
- **Volume'lar:** `securegate-mssql-data` (DB fayllari), `securegate-uploads` (`wwwroot/uploads` — yuklangan rasmlar) va `securegate-keys` (ASP.NET Core DataProtection kalitlari — kamera parollari shifri qayta ishga tushgandan keyin ham ochilishi uchun).

### Muhim env o'zgaruvchilari (`docker-compose.yml`)

| O'zgaruvchi | Standart | Izoh |
|-------------|----------|------|
| `MSSQL_SA_PASSWORD` | `SecureGate_Str0ng!Pass` | SQL `sa` paroli |
| `JWT_KEY` | `...ChangeMe...` | JWT imzo kaliti — production'da o'zgartiring |
| `ASPNETCORE_ENVIRONMENT` | `Development` | Swagger + batafsil xatolar |
| `FACE_RECOGNITION_ENABLED` | `true` | `false` — `CameraStreamWorker` ni o'chiradi |
| `SEEDER_SUPERADMIN_EMAIL` | `superadmin@securegate.local` | Birinchi ishga tushishda yaratiladigan SuperAdmin |
| `SEEDER_SUPERADMIN_PASSWORD` | `ChangeMe123!` | Bo'sh qoldirilsa tasodifiy parol generatsiya qilinadi va logga yoziladi |

> ⚠️ Konteynerda faqat **HTTP** listener bor, shuning uchun `UseHttpsRedirection()` avtomatik o'chiriladi (aks holda 307 redirect loop bo'lardi).

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
    // "Key" ATAYLAB yo'q — user-secrets yoki `Jwt__Key` env orqali beriladi.
    // Kalit yo'q / 32 baytdan qisqa bo'lsa ilova ishga tushmaydi (JwtSettings.Validate).
    "AccessTokenMinutes": 15,        // access token muddati
    "RefreshTokenDays": 14
  },
  "Cors": {
    "AllowedOrigins": [ "https://localhost:51985" ]
  }
}
```

Yuz tanish sozlamalari (ixtiyoriy, `FaceRecognition:` bo'limi): `MinSimilarity`, `DetectionIntervalMs`, `CameraRefreshSeconds` va h.k.

### Maxfiy qiymatlar

Hech qanday sir `appsettings.json` da saqlanmaydi. Ularni environment variable
(`__` ajratgichi bilan) yoki user-secrets orqali bering:

| Kalit | Env varianti | Izoh |
|-------|--------------|------|
| `Jwt:Key` | `Jwt__Key` | **Majburiy.** Kamida 32 bayt. `openssl rand -base64 48` |
| `Seeder:SuperAdminPassword` | `Seeder__SuperAdminPassword` | Berilmasa tasodifiy parol generatsiya qilinib logga yoziladi |
| `Seeder:SuperAdminEmail` | `Seeder__SuperAdminEmail` | Default: `superadmin@securegate.local` |
| `DataProtection:KeyRingPath` | `DataProtection__KeyRingPath` | Kamera parollari shifrlash kalitlari saqlanadigan doimiy katalog |

> ⚠️ `DataProtection:KeyRingPath` berilmasa kalit halqasi vaqtinchalik katalogda qoladi va
> restartdan keyin **barcha kamera parollari ochilmay qoladi**. Docker'da bu volume'ga ulangan.

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

