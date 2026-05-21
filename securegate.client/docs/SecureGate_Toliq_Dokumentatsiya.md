# 🎥 SecureGate - IP Camera Management System
## Toʻliq Proyekt Dokumentatsiyasi

---

## 📋 Loyiha Haqida Umumiy Maʼlumot

**SecureGate** - bu IP kameralar va turnikiletlarni boshqarish uchun zamonaviy veb-platforma. Tizim AI texnologiyasi orqali yuzlarni tanib olish, turniketlarni avtomatik boshqarish va batafsil tarixlarni saqlash imkoniyatini taqdim etadi.

### Asosiy Xususiyatlari:
- 🎥 IP kameralarni real-time monitoring
- 🤖 AI orqali yuzlarni aniqlash (Face Recognition)
- 🚪 Turniket boshqaruvi va Access Control
- 📊 Batafsil hisobotlar va statistika
- 👥 Rol-bazali aksess (RBAC)
- 🔒 Xavfsizlik va autentifikatsiya

---

## 🏗️ Arxitektura

```
┌─────────────────────────────────────────────────────────────┐
│                    Frontend (React.js)                       │
│  (Dashboard, Kameralar, Turniketlar, Hisobotlar, Sozlamalar)│
└─────────────────────────────────────────────────────────────┘
                              ↕
                    REST API + WebSocket
                              ↕
┌─────────────────────────────────────────────────────────────┐
│              Backend (Python + FastAPI)                      │
│  (Authentication, Camera Management, Face Recognition, DB)  │
└─────────────────────────────────────────────────────────────┘
                              ↕
┌─────────────────────────────────────────────────────────────┐
│     Database (PostgreSQL) + File Storage (S3/Local)         │
│              AI Model (Face Recognition)                     │
└─────────────────────────────────────────────────────────────┘
```

---

## 1️⃣ NAVIGATION (Menyu Tuzilishi)

```
📱 SecureGate Dashboard
├── 🏠 Bosh Sahifa (Dashboard)
│   ├── Umumiy statistika
│   ├── Oxirgi faoliyatlar
│   └── Tizim xolati
│
├── 🎥 Kameralar
│   ├── Kamera ro'yxati (Grid/List view)
│   ├── ➕ Yangi kamera qoʻshish
│   ├── ✏️ Kamera tahrirlash
│   ├── 🗑️ Kamera oʻchirish
│   ├── 🔍 Kamera izlash
│   ├── 👁️ Kamera kattalashtirilgan koʻrinish
│   ├── 📦 Kameraları guruhlar boʻyicha guruhlash
│   ├── 🎬 Grid rejimi (3x3, 4x4, vb.)
│   └── 🤖 AI orqali yuzlarni tanib olish
│
├── 📹 Camera Yozuvlari Tarixi
│   ├── Sana boʻyicha qidirish
│   ├── Yozuvlarni koʻrish
│   ├── 📥 Arxivdan yuklab olish
│   └── 🔍 Yozuvlar boʻyicha qidirish
│
├── 🚪 Turniketlar
│   ├── Turniket ro'yxati
│   ├── ➕ Turniket qoʻshish
│   ├── ✏️ Turniket tahrirlash
│   ├── 🗑️ Turniket oʻchirish
│   ├── 📊 Kirish-chiqish tarixi
│   ├── 🔒 Turniketni blokirovka qilish
│   ├── 🔓 Turniketni ochish (Admin)
│   ├── 🚨 Hammasi ochish (Emergency)
│   ├── ⚠️ Nomalum yuzlarni aniqlash
│   └── 📸 Nomalum yuzlarning rasmlarini saqlash
│
├── 👤 Yuzlarni Aniqlash (Face Database)
│   ├── Yuzlarni roʻyxati
│   ├── ➕ Yangi yuz qoʻshish
│   ├── ✏️ Yuzni tahrirlash
│   ├── 🗑️ Yuzni oʻchirish
│   ├── 🤖 Model oʻqitish (Avtomatik)
│   └── 📊 Model sifati statistikasi
│
├── 👥 Foydalanuvchilar
│   ├── Foydalanuvchilar roʻyxati
│   ├── ➕ Yangi foydalanuvchi qoʻshish
│   │   ├── F.I.Sh (Familiya, Ism, Sharifi)
│   │   ├── Telefon raqami
│   │   ├── Guruhi
│   │   ├── Tugulgan kuni
│   │   ├── Jinsi
│   │   ├── Ota-ona raqami
│   │   ├── Manzili
│   │   └── Yuzning rasmi (Face)
│   ├── ✏️ Foydalanuvchi tahrirlash
│   ├── 🗑️ Foydalanuvchini oʻchirish (Arxivga → tahrirlash)
│   ├── 🔒 Foydalanuvchini blokirovka qilish
│   ├── 🔍 Qidirish va filterlashtirish
│   └── 📊 Guruhlari boʻyicha filtr
│
├── 👔 Rahbariyat (Management)
│   ├── Rahbariyat roʻyxati
│   ├── ➕ Yangi rahbariyat qoʻshish
│   │   ├── F.I.Sh
│   │   ├── Telefon raqami
│   │   ├── Lavozimi
│   │   ├── Tugulgan kuni
│   │   ├── Rasm (Face)
│   │   ├── Rol tayini
│   │   └── Kamera guruhlari huquqi
│   ├── ✏️ Rahbariyat tahrirlash
│   ├── 🗑️ Rahbariyat oʻchirish
│   └── 🔑 Roli boʻyicha huquqlar
│
├── 🚫 Bloklangan Foydalanuvchilar
│   ├── Bloklangan roʻyxati
│   ├── FISH, Bloklash sababini koʻrish
│   ├── Kim bloklagani va qachon
│   ├── Bloklash muddati
│   ├── 🔓 Blokdan chiqarish
│   ├── ⏱️ Muddatni uzaytirish
│   └── ✅ Avtomatik blok ocharish (Vaqti tugaganida)
│
├── 📊 Hisobotlar
│   ├── Face ID Hisoboti
│   │   ├── Foydalanuvchi/Xodim nomi
│   │   ├── Turniket/Kamera raqami
│   │   ├── Kirish/Chiqish vaqti
│   │   ├── Usuli (Face/Karta)
│   │   ├── Rasm/Video
│   │   └── Sana boʻyicha filter
│   ├── Turniket Hisoboti
│   ├── Kamera Yozuvlari
│   ├── Faoliyat Jurnali
│   └── 📥 PDF/Excel yuklab olish
│
├── ⚙️ Sozlamalar
│   ├── 🔔 Bildirishnomalar
│   │   ├── Bildirishnomalarni yoqish/oʻchirish
│   │   ├── Email bildirishnomalar
│   │   ├── SMS bildirishnomalar
│   │   └── Push bildirishnomalar
│   ├── 🔌 Integratsiyalar
│   │   ├── Third-party servislari
│   │   └── API kalitlarini boshqarish
│   ├── 🌐 API va Webhooks
│   │   ├── API dokumentatsiyasi
│   │   ├── API kaliti generatsiyasi
│   │   └── Webhook konfiguratsiyasi
│   ├── 📖 Dokumentatsiya
│   │   ├── Teknik hujjat
│   │   ├── IP kamera ulash qoʻllanmasi
│   │   └── Turniket integratsiyasi
│   └── 👤 Profil sozlamalari
│
└── 🔐 Rollar va Huquqlar
    ├── 👑 Super Admin
    │   └── Barcha huquqlar
    ├── 👨‍💼 Admin (O'ziga xos)
    │   ├── Kamera guruhlari boʻyicha huquqlar
    │   ├── Turniketlar boshqaruvi
    │   ├── Hisobotlarni koʻrish
    │   └── Foydalanuvchilar boshqaruvi
    ├── 👨‍💻 Xodim
    │   ├── Turniketni ochish/yopish
    │   ├── Kameralarni real-time koʻrish
    │   └── Hisobotlarni koʻrish
    └── 👁️ Koʻrish huquqi (Viewer)
        └── Faqat kameralarni koʻrish
```

---

## 2️⃣ BOSH SAHIFA (Dashboard)

### 2.1 Maʼlumotlar va Elementi

```
┌─────────────────────────────────────────────────────────┐
│  SecureGate Dashboard                    👤 Foydalanuvchi│
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│  📊 STATISTIKA KARTOCHKALARI                            │
├─────────────────────────────────────────────────────────┤
│  🎥 Faol Kameralar      🚪 Faol Turniketlar            │
│  24 (5 oʻchirilgan)     8 (2 blokirovkalashtirilgan)   │
├─────────────────────────────────────────────────────────┤
│  👥 Foydalanuvchilar    🚫 Bloklangan Foydalanuvchilar│
│  156 (3 yangi bugun)    12 (2 vaqti tugagan)          │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│  🔥 REAL-TIME FAOLIYAT                                 │
├─────────────────────────────────────────────────────────┤
│  14:32 - Ahmadov A. Turniket #1 orqali kirit (Face)  │
│  14:31 - Kamera #5 - Nomalum yuz aniqlandi             │
│  14:28 - Turniket #3 blokirovka qilingan (Admin)      │
│  14:25 - Anorova M. Turniket #2 orqali chiqdi        │
│  14:22 - Kamera #7 yozuv boshlanadi                   │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│  📈 BUGUNGI STATISTIKA                                 │
├─────────────────────────────────────────────────────────┤
│  Jami kiritish: 87        │  Jami chiqish: 85         │
│  Face: 82  Karta: 5       │  Face: 80  Karta: 5       │
│  Nomalum yuzlar: 3        │  Xatolar: 1              │
└─────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────┐
│  🎥 KAMERA DUROMI                                      │
├─────────────────────────────────────────────────────────┤
│  Faol: 22  │  Offline: 2  │  Xato: 1  │  Xizmatda: 1  │
└─────────────────────────────────────────────────────────┘
```

---

## 3️⃣ KAMERALAR BOSHQARUVI

### 3.1 Kamera Roʻyxati

```
┌─────────────────────────────────────────────────────────┐
│  🎥 Kameralar                                           │
├─────────────────────────────────────────────────────────┤
│  [➕ Yangi kamera] [🔍 Izlash: _________] [👁️ View: Grid]│
│  [Filter] Guruhi: [Barcha ▼]  Status: [Faol ▼]        │
│  [3x3] [4x4] [Ro'yxat]                                 │
└─────────────────────────────────────────────────────────┘

┌────────────┐  ┌────────────┐  ┌────────────┐
│ 🎥 Koridor │  │ 🎥 Lobiya  │  │ 🎥 Qabul   │
│  Cam-1     │  │  Cam-2     │  │  Cam-3     │
│ 192.168... │  │ 192.168... │  │ 192.168... │
│  Faol ●    │  │  Faol ●    │  │  Offline ○ │
│ [👁️ Katta] │  │ [👁️ Katta] │  │ [👁️ Katta] │
│ [✏️] [🗑️]  │  │ [✏️] [🗑️]  │  │ [✏️] [🗑️]  │
└────────────┘  └────────────┘  └────────────┘
```

### 3.2 Yangi Kamera Qoʻshish

```
FORM: Kamera Qoʻshish
─────────────────────────────
Kamera Nomi:        [IP Camera Entrance     ]
IP Adresi:          [192.168.1.100          ]
Port:               [8080                   ]
Foydalanuvchi:      [admin                  ]
Parol:              [••••••••••             ]
Guruhi:             [Asosiy ▼               ]
Lokatsiyasi:        [Kirish eshiği          ]
Turi:               [PTZ/Fixed ▼            ]
Tavsifi:            [Asosiy kirish          ]
                    [Veb-interfeys URL]     
                    [Status tekshirish]     
─────────────────────────────
[✅ Qoʻshish]  [❌ Bekor]
```

### 3.3 Kameraları Guruhlash

```
KAMERA GURUHLARI
────────────────────────────────────
Guruh: "Asosiy Qabul" (3 kamera)
├─ Cam-1 (Kirish)
├─ Cam-2 (Lobiya)
└─ Cam-3 (Qabul stoli)

Guruh: "Koridor" (2 kamera)
├─ Cam-4 (1-Koridor)
└─ Cam-5 (2-Koridor)

ROLLAR BOʻYICHA AKSESS:
─────────────────────────
👑 Super Admin    → Barcha guurhlar
👨‍💼 Admin (Lobiya) → "Asosiy Qabul" + "Koridor"
👨‍💼 Admin (Xavf)   → "Koridor"
👨‍💻 Xodim          → "Asosiy Qabul"
```

### 3.4 AI orqali Yuz Tanib Olish

```
KAMERA: Kirish (Cam-1)
┌─────────────────────────────────────────┐
│                                         │
│  [Live Video Stream]                   │
│  ┌──────────────────────────────────┐  │
│  │  👤 Ahmadov Ali                  │  │
│  │  Aniqlik: 98.5%                  │  │
│  │  Vaqti: 14:32:15                 │  │
│  │  [Foto]                          │  │
│  └──────────────────────────────────┘  │
│                                         │
│  BIRINCHI 5TA ANIQLANGAN:              │
│  1. Ahmadov Ali      - 98.5%           │
│  2. Qodirova Zaynab  - 87.3%           │
│  3. Bobov Farrux     - 76.2% (⚠️ Past) │
│                                         │
└─────────────────────────────────────────┘
```

---

## 4️⃣ CAMERA YOZUVLARI TARIXI

### 4.1 Yozuvlarni Koʻrish

```
KAMERA: Kirish (Cam-1) - Yozuv Tarixi
─────────────────────────────────────────
Sana: [📅 2024-01-15] - [📅 2024-01-22]
[Bugun] [Kecha] [Bu hafta] [Bu oy] [Shaxsi]

Yozuvlar:
┌────────────────────────────────────────┐
│ 15-Jan, 08:00-09:30 (1.5 soat - 350MB)  │ [▶️] [📥]
│ 15-Jan, 14:30-16:45 (2.25 soat - 520MB)│ [▶️] [📥]
│ 15-Jan, 18:00-19:15 (1.25 soat - 280MB)│ [▶️] [📥]
│ 16-Jan, 07:45-09:00 (1.25 soat - 290MB)│ [▶️] [📥]
└────────────────────────────────────────┘

TOTAL: 6.25 soat, 1.44GB

[◀ Oldingi]  [Keyingi ▶]  [PDF-ga] [Excel-ga]
```

### 4.2 Yozuvlarni Yuklab Olish

```
ARXIVLASH VA YUKLAB OLISH
───────────────────────────
Turlanish:   [Zip Format ▼]
Sifati:      [Original ▼] (H.264, 25fps)
Sifati:      [Takomil ▼]  (H.264, 15fps)
             [Tekin ▼]    (H.264, 5fps)

Qabul shaklari:
✓ Direct yuklab olish (< 2GB)
✓ Cloud saqlash (Google Drive/Yandex)
✓ FTP yuklash
✓ Email jonatish (kichik fayllar)

[📥 YUKLAB OLISH]  [☁️ CLOUD]  [📧 EMAIL]
```

---

## 5️⃣ TURNIKETLAR BOSHQARUVI

### 5.1 Turniket Roʻyxati va Tarixni

```
TURNIKETLAR BOSHQARUVI
─────────────────────────────────────────
[➕ Yangi Turniket] [🔍 Izlash]

┌─────────────────────────────────────┐
│ Turniket #1 - "Asosiy kirish"      │
│ Lokatsiya: Bosh qabul               │
│ Kamera: Cam-1 (Live)                │
│ Status: ✅ Faol                      │
│ Bugungi ma'lumot:                   │
│   ↪️ Kirish: 45   ← Chiqish: 42     │
│   Face: 40+5 Karta: 5               │
│ [👁️] [✏️] [🗑️] [🔒 Blok] [🔓 Och]  │
└─────────────────────────────────────┘

┌─────────────────────────────────────┐
│ Turniket #2 - "3-Qavat Chiqqash"   │
│ Lokatsiya: 3-Qavat                  │
│ Kamera: Cam-7                       │
│ Status: 🔒 Blokirovka               │
│ Sababi: "Texnik xizmatda"           │
│ Muddati: 2 soat (14:32 amaliy)     │
│ [👁️] [✏️] [🗑️] [🔓 Ochish]          │
└─────────────────────────────────────┘
```

### 5.2 Turniket Kirish-Chiqish Tarixi

```
TURNIKET #1 - Kirish-Chiqish Tarixi
──────────────────────────────────────────
Sana: [2024-01-15]  Vaqt: [Barcha]

KIRISH:
────────────────────────────────────────
14:35 | Ahmadov Ali      | Face (99%) | [📸]
14:32 | Qodirova Z.      | Karta      | [📸]
14:28 | Bobov F.         | Face (87%) | [📸]
14:25 | Anorova M.       | Face (95%) | [📸]
14:20 | ⚠️ NOMALUM YUZ    | Face (34%) | [📸]
14:18 | Xasanov R.       | Karta      | [📸]

CHIQISH:
────────────────────────────────────────
14:40 | Bobov F.         | Face (88%) | [📸]
14:38 | Ahmadov Ali      | Face (98%) | [📸]
14:30 | Qodirova Z.      | Karta      | [📸]

UMUMIY: Kirish=45  Chiqish=42  Farqi=+3
Face=40  Karta=5  Xatolar=0
```

### 5.3 Turniket Emergency Ochish

```
EMERGENCY HAMMASI OCHISH
───────────────────────────────────────
⚠️ SHUNI EHTIYOTKORLIK BILAN FOYDALANING!

[🚨 HAMMASI OCHISH]

Status:
✓ Turniket #1 - Ochildi (Admin: Xasanov R. 14:35)
✓ Turniket #2 - Ochildi (Admin: Xasanov R. 14:35)
✓ Turniket #3 - Ochildi (Admin: Xasanov R. 14:35)
✓ Turniket #4 - Ochildi (Admin: Xasanov R. 14:35)

Avtomati yopilish vaqti: 5 daqiqa (14:40)
[🔄 Vaqtni uzaytirish]  [🔒 Hammasi yopish]
```

### 5.4 Nomalum Yuzlarni Aniqlash va Bildirishnoma

```
⚠️ NOMALUM YUZ ANIQLANDI
──────────────────────────────────
Vaqt:     14:20:35
Kamera:   Cam-1 (Asosiy kirish)
Turniket: Turniket #1
Aniqlik:  34% (PAST)

[📸 Turli burchakdagi rasmlar]
[Rasmni saqlash]  [Arkiv+Qoʻshish]  [X Yopish]

EMAIL BILDIRISHNOMA: ✅ Yuborildi
SMS BILDIRISHNOMA:   ✅ Yuborildi
PUSH BILDIRISHNOMA:  ✅ Yuborildi
```

---

## 6️⃣ YUZ ANIQLASH (Face Recognition Database)

### 6.1 Yuzlarni Boshqaruvi

```
YUZ MA'LUMOTBAZASI
──────────────────────────────────────
[➕ Yangi Yuz] [🔍 Izlash] [🤖 Model Status]

Yuzlarni Ro'yxati:

┌────────────────────────────────────┐
│ ID: 1                              │
│ Foydalanuvchi: Ahmadov Ali         │
│ Yuz rasmlar: 12 shlok             │
│ Model ma'lumot: ✅ Tuzilgan       │
│ Aniqlik: 97.2%                     │
│ [👁️ Ko'rish] [✏️] [🗑️] [🔄 O'qitish]│
└────────────────────────────────────┘

Jami yuzlar: 156
O'qitilgan: 156
Aniqlik o'rtacha: 94.3%
Xatolar: 2
```

### 6.2 Yangi Yuz Qoʻshish va Oʻqitish

```
YANGI YUZ QOʻSHISH
─────────────────────────────────────────
Foydalanuvchi: [Ahmadov Ali    ]
Telefo:        [+998901234567 ]
Ota-ona:       [Asqar Ahmadov ]

RASMLARNI YUKLASH:
[📤 Rasm yuklash (10+ shlok tavsiya etiladi)]

Shloklarni tahlil qilish:
✅ Rasm 1 - Yuz aniqlandi (Front, Yaxshi)
✅ Rasm 2 - Yuz aniqlandi (45°, Yaxshi)
✅ Rasm 3 - Yuz aniqlandi (Left, Yaxshi)
✓ Rasm 4 - Qo'shimcha rasmlar

[🤖 MODEL O'QITISH]  [❌ Bekor]

O'QITISH JARAYONI:
████████████████░░░░░  75% tugallandi
Qolgan vaqti: ~10 soniya
```

---

## 7️⃣ FOYDALANUVCHI BOSHQARUVI

### 7.1 Foydalanuvchi Qoʻshish

```
YANGI FOYDALANUVCHI QOʻSHISH
──────────────────────────────────────
F.I.Sh:           [Ahmadov Ali Askarovich    ]
Telefon:          [+998901234567             ]
Tugulgan kuni:    [📅 1995-05-15             ]
Jinsi:            [Erkak ▼]
Manzili:          [Tashkent, shahar         ]
Ota-ona raqami:   [+998901111111             ]
Guruhi:           [3-A sinf ▼]
Status:           [⭕ Faol]

YUZ RASMI:
[📤 Rasm yuklash] - Avtomati AI orqali ishlaydi

YUZ ANIQLASH:
✅ 1 ta yuz aniqlandi
   │
   └─ Aniqlik: 98.5%

[✅ QOʻSHISH]  [❌ BEKOR]
```

### 7.2 Foydalanuvchi Blokirovka

```
FOYDALANUVCHINI BLOKIROVKA QILISH
──────────────────────────────────────
Foydalanuvchi: Ahmadov Ali
Telefon:       +998901234567

BLOKLASH SABABINI TANLANG:
☐ Toʻlov qilmagan
☐ Intizomiy tadbir
☐ Ish qugalashgan
☐ Boshqa (tushuntirib bering)

Bloklash muddati:
☐ 1 kun
☐ 1 hafta
☐ 1oy
☐ Doimiy

Izoh:
[Tarixiy sharhi yoki sababi...]

[✅ BLOKIROVKA]  [❌ BEKOR]

AVTOMATI BLOK OCHARISH: ✅ Vaqti tugaganida
```

---

## 8️⃣ RAHBARIYAT

### 8.1 Rahbariyat Qoʻshish va Huquqlar

```
YANGI RAHBARIYAT / XODIM QOʻSHISH
──────────────────────────────────────────
F.I.Sh:          [Xasanov Ravshan Qadir   ]
Telefon:         [+998901234567           ]
Lavozimi:        [Xavfsizlik Mudiri ▼]
Tugulgan kuni:   [📅 1980-03-20           ]
Rasm (Face):     [📤 Yuklash]

ROL TAYINI:
○ 👑 Super Admin        (Barcha huquqlar)
○ 👨‍💼 Admin             (O'ziga xos)
○ 👨‍💻 Xodim             (Cheklangan)
● ☑️ Kustomli rol       (Tanlang)

KAMERA GURUHLARI HUQUQI:
☑️ "Asosiy Qabul"      (Koʻrish, Live)
☑️ "Koridor"           (Koʻrish, Yozuv)
☐ "Xarita xonasi"      
☐ "Boblardan"

TURNIKET HUQUQI:
☑️ Turniket ochish/yopish
☑️ Emergency ochish
☑️ Blokirovka qilish
☐ Hisobotlarni koʻrish

[✅ QOʻSHISH]  [❌ BEKOR]
```

---

## 9️⃣ BLOKLANGAN FOYDALANUVCHILAR

### 9.1 Bloklangan Roʻyxat

```
BLOKLANGAN FOYDALANUVCHILAR
──────────────────────────────────────
Jami: 12 (2 vaqti tugasiga yaqin)

┌───────────────────────────────────────┐
│ Ahmadov Ali                           │
│ Sababy: "Intizomiy tadbir"           │
│ Kim bloklagani: Xasanov R.           │
│ Vaqti: 2024-01-10 10:00              │
│ Muddati: 7 kun                       │
│ Qolgan: 2 kun ⏰ (14 soat)           │
│ [🔓 Ochish] [⏱️ Muddatni uzaytirish] │
└───────────────────────────────────────┘

┌───────────────────────────────────────┐
│ Qodirova Zaynab                       │
│ Sababy: "Toʻlov qilmagan"            │
│ Kim bloklagani: Admin Panel           │
│ Vaqti: 2024-01-05 14:30              │
│ Muddati: 30 kun                      │
│ Qolgan: 23 kun ⏰                    │
│ [🔓 Ochish] [⏱️ Muddatni uzaytirish] │
└───────────────────────────────────────┘
```

### 9.2 Muddatni Uzaytirish

```
MUDDATNI UZAYTIRISH
────────────────────────────────────
Foydalanuvchi: Ahmadov Ali
Hozirgi muddat: 7 kun (2 kun qolgan)
Yangi muddati:  [30 ▼] kun

Yangi blok achalishi: 2024-01-31 10:00
[✅ SAQLASH]
```

---

## 🔟 HISOBOTLAR

### 10.1 Face ID Hisoboti

```
FACE ID HISOBOTI
──────────────────────────────────────────
Sana: [2024-01-15] - [2024-01-22]  
Jami kiritish-chiqish: 847

┌──────────────────────────────────────┐
│ Foydalanuvchi: Ahmadov Ali          │
│ Vaqti: 2024-01-15 14:32:15          │
│ Turniket: #1 "Asosiy kirish"        │
│ Usuli: Face Recognition              │
│ Aniqlik: 98.5%                       │
│ [📸 Rasm]                            │
├──────────────────────────────────────┤
│ Foydalanuvchi: Qodirova Zaynab      │
│ Vaqti: 2024-01-15 14:28:45          │
│ Turniket: #2 "3-Qavat"              │
│ Usuli: Kartalik                      │
│ [📸 Rasm]                            │
├──────────────────────────────────────┤
│ ⚠️ NOMALUM: Tanib olinmagan yuz     │
│ Vaqti: 2024-01-15 14:20:35          │
│ Kamera: #1 "Asosiy kirish"          │
│ Aniqlik: 34% (PAST)                 │
│ [📸 Rasm]                            │
└──────────────────────────────────────┘

STATISTIKA:
───────────────────────────────────────
Jami kiritish: 400  (Face: 380, Karta: 20)
Jami chiqish:  397  (Face: 380, Karta: 17)
Noma'lum yuzlar: 5
Xatolar: 2
Ma'lumot: 98.3%

[📊 GRAFIK] [📥 PDF] [📊 EXCEL] [🖨️ CHOP]
```

---

## 1️⃣1️⃣ SOZLAMALAR

### 11.1 Bildirishnomalar

```
BILDIRISHNOMALAR SOZLAMALARI
──────────────────────────────────────

🔔 EMAIL BILDIRISHNOMALAR
☑️ Nomalum yuzlar aniqlandi
☑️ Turniket xatosi
☑️ Kamera offline
☐ Bugungi faoliyat
Emai: admin@securegate.com

📱 SMS BILDIRISHNOMALAR
☑️ Nomalum yuzlar
☑️ Emergency situatsiya
☐ Bugungi xulosa
Raqam: +998901234567

🔔 PUSH BILDIRISHNOMALAR
☑️ Faol xatolar
☑️ Xavfli vaziyatlar
☐ Ma'lumotli xabarlar

🔊 OVOZ BILDIRISHNOMALAR
☑️ Turniket alarm
☐ Kamera offline

[💾 SAQLASH]
```

### 11.2 API va Webhooks

```
API VA WEBHOOKS
───────────────────────────────────────
Siz o'zining IP kameralarini va turniketlarni
integratsiya qilish uchun API ishlating:

API KALITLARI:
────────────────────────────────────────────
Kaliti: sk_live_abc123xyz789...
Yaratilgan: 2024-01-10 10:00
Oxirgi foydalanish: 2024-01-15 14:32
[Yangi kaliti]  [O'chirish]

WEBHOOK URLS:
────────────────────────────────────────────
URL: https://yourserver.com/webhook
Sobytlar:
  ☑️ Face Detected
  ☑️ Unknown Face
  ☑️ Door Opened
  ☑️ Error
[Sinab koʻrish]  [O'chirish]

API DOKUMENTATSIYASI:
────────────────────────────────────────────
Base URL: https://api.securegate.com/v1
Authentication: Bearer {API_KEY}

Endpoints:
1. GET /cameras - Kameraların roʻyxati
2. POST /cameras - Yangi kamera qoʻshish
3. GET /turnstiles - Turniketlarni roʻyxati
4. POST /access-log - Kiritish/Chiqish tarixini saqlash
5. GET /unknown-faces - Noma'lum yuzlar
6. POST /notifications - Bildirishnoma yuborish
```

---

## 1️⃣2️⃣ ROLLAR VA HUQUQLAR

### 12.1 Role Matritsa

```
ROLE HUQUQLARI MATRITSA
──────────────────────────────────────────────

FUNKSIYA              │ Super  │ Admin  │ Xodim │ Viewer
                      │ Admin  │        │       │
──────────────────────┼────────┼────────┼───────┼────────
Dashboard koʻrish     │   ✓    │   ✓    │  ✓    │   -
Kameraları qoʻshish   │   ✓    │   ✓    │  -    │   -
Kameraları tahrirlash │   ✓    │   ✓    │  -    │   -
Kameraları live koʻr. │   ✓    │   ✓    │  ✓    │   ✓
Yozuvlarni koʻrish    │   ✓    │   ✓    │  ✓    │   -
Turniket ochish       │   ✓    │   ✓    │  ✓    │   -
Emergency ochish      │   ✓    │  ⚠️    │  -    │   -
Foydalanuvchi qoʻsh.  │   ✓    │   ✓    │  -    │   -
Foydalanuvchi tahrir. │   ✓    │   ✓    │  -    │   -
Hisobotlarni koʻr.    │   ✓    │   ✓    │  ✓    │   -
Sozlamalani tahrirlash│   ✓    │   -    │  -    │   -
Admin yarratish       │   ✓    │   -    │  -    │   -

Legend: ✓=Faol, ⚠️=Cheklangan, -=Yo'q
```

### 12.2 Custom Admin Roli Yaratish

```
CUSTOM ADMIN ROLI YARATISH
──────────────────────────────────────

Rol nomi: [Lobiya Admin              ]
Tavsifi: [Asosiy qabul va korridor]

BOʻLIMLAR:
☑️ Dashboard          [○ Oqish ● Oqish+Tahrirish]
☑️ Kameralar          [● Oqish ○ Oqish+Tahrirish]
☑️ Yozuv Tarixi       [● Oqish ○ Tahrirish]
☑️ Turniketlar        [● Oqish+Ochish ○ Barcha]
☑️ Hisobotlar         [● Oqish ○ Tahrirish]
☐ Foydalanuvchilar   
☐ Rahbariyat         
☐ Bloklangan         
☐ Sozlamalar         

KAMERA GURUHLARI:
☑️ "Asosiy Qabul" - Barcha huquq
☑️ "Koridor"      - Faqat koʻrish
☐ "Boshqa"

TURNIKET HUQUQI:
☑️ Turniket ochish/yopish
☐ Emergency ochish
☐ Blokirovka qilish

[✅ SAQLASH]  [❌ BEKOR]
```

---

## 1️⃣3️⃣ TEXNIK XULOSA

### 13.1 Texnik Stack

```
BACKEND:
├── Python 3.11+
├── FastAPI (Web Framework)
├── PostgreSQL (Database)
├── Redis (Caching & Sessions)
├── OpenCV + Face Recognition (AI)
├── SQLAlchemy (ORM)
├── JWT (Authentication)
├── Celery (Async Tasks)
└── Swagger/OpenAPI (API Docs)

FRONTEND:
├── React 18+
├── TypeScript
├── Tailwind CSS (Styling)
├── Socket.IO (Real-time)
├── Redux (State Management)
├── React Query (Data Fetching)
├── Chart.js (Reports)
└── Axios (HTTP Client)

INFRASTRUCTURE:
├── Docker & Docker-Compose
├── Nginx (Reverse Proxy)
├── S3/MinIO (File Storage)
├── Linux Server
├── SSL Certificate
└── CI/CD Pipeline
```

### 13.2 Database Schema (O'zaking)

```
ASOSIY JADVALLAR:

1. users (Foydalanuvchilar)
   ├── id, username, password_hash
   ├── full_name, phone, email
   ├── date_of_birth, gender
   ├── group_id, status, created_at
   └── face_embedding (AI model uchun)

2. cameras (Kameralar)
   ├── id, name, ip_address, port
   ├── username, password, location
   ├── group_id, status, created_at
   └── rtsp_stream_url

3. turnstiles (Turniketlar)
   ├── id, name, location, camera_id
   ├── status, block_reason, blocked_until
   └── created_at

4. access_logs (Kiritish-Chiqish Tarixı)
   ├── id, user_id, turnstile_id
   ├── access_type (IN/OUT)
   ├── method (FACE/CARD)
   ├── timestamp, image_path
   └── accuracy_score

5. unknown_faces (Noma'lum Yuzlar)
   ├── id, image_path, camera_id
   ├── detected_at, embedding
   └── processed

6. admins (Rahbariyat)
   ├── id, user_id, role
   ├── permissions (JSON)
   ├── camera_groups_access
   └── created_at

7. blocked_users (Bloklangan)
   ├── id, user_id, admin_id
   ├── reason, block_until
   └── created_at
```

---

## 1️⃣4️⃣ XAVFSIZLIK VA PRIVACYLIK

### 14.1 Xavfsizlik Tadbiri

```
XAVFSIZLIK MEXANIZMLARI:
─────────────────────────────────────
✓ JWT Token Autentifikatsiya
✓ Role-Based Access Control (RBAC)
✓ Barcha kiritishlar jurnallashtiriladi
✓ SSL/TLS Encryption
✓ Password Hashing (bcrypt)
✓ API Rate Limiting
✓ SQL Injection Himoyasi
✓ CSRF Token Himoyasi
✓ Yuz rasm shifrlashtirilgan saqlashtiriladi
✓ Log fayl saqlashtiriladi
✓ Backup kunlik qilinadi
✓ 2FA (2-Factor Authentication)
```

### 14.2 Foydalanuvchi Ma'lumotlari

```
FOYDALANUVCHI MA'LUMOTLARI TADBIRI:
────────────────────────────────────
✓ GDPR/O'zbekistan Mamlakat Qonuniga Muvofiq
✓ Yuz rasmlarI mos Shaklda shifrlashtirilgan
✓ Foydalanuvchining kafolati mavjud
✓ Ma'lumotlarni oʻchirish mumkin
✓ Export/Download huquqi
✓ Audit jurnali saqlashtiriladi
```

---

## 1️⃣5️⃣ AMALGA OSHIRISH RO'YXATI (Implementation Checklist)

```
FASE 1: ASOSIY LOYIHA
└─ [✓] Database dizayni
   [✓] API Endpoints
   [✓] Authentication tizimi
   [✓] Frontend Layout
   
FASE 2: KAMERA BOSHQARUVI
└─ [✓] Kamera CRUD operatsiyalari
   [✓] Real-time video streaming
   [✓] Kamera guruhlashtirlash
   [✓] Grid koʻrinish (3x3, 4x4)

FASE 3: YOZUV TARIXI
└─ [✓] Yozuvlarni saqlash
   [✓] Vaqt boʻyicha filterlashtirish
   [✓] Arxivdan yuklab olish
   [✓] Video qayta ishlash

FASE 4: TURNIKET TIZIMI
└─ [✓] Turniket CRUD
   [✓] Kiritish-Chiqish tarixı
   [✓] Status boshqaruvi
   [✓] Emergency ochish

FASE 5: FACE RECOGNITION
└─ [✓] Face Detection (OpenCV)
   [✓] Face Embedding
   [✓] Model oʻqitish
   [✓] Noma'lum yuzlarni aniqlash

FASE 6: FOYDALANUVCHI BOSHQARUVI
└─ [✓] CRUD operatsiyalari
   [✓] Blokirovka tizimi
   [✓] Rol ta'yini
   [✓] Guruhlash

FASE 7: HISOBOTLAR
└─ [✓] Face ID hisoboti
   [✓] Turniket hisoboti
   [✓] PDF/Excel export
   [✓] Grafiklar va statistika

FASE 8: SOZLAMALAR
└─ [✓] Bildirishnomalar
   [✓] API va Webhooks
   [✓] Integrations

FASE 9: TESTING va DEPLOY
└─ [✓] Unit testlar
   [✓] Integration testlar
   [✓] Performance testlar
   [✓] Xavfsizlik auditı
   [✓] Production deploy
```

---

## 🎯 QOʻSHIMCHA TAFSILOT

### Texnik Talablar:
- **Server**: Linux (Ubuntu 20.04+)
- **Xotira**: Minimal 8GB RAM, Recommended 16GB
- **Saqlash**: 1TB+ (Kamera yozuvlarni guya saqlash)
- **Tarmoq**: 100Mbps+ (Video streaming uchun)
- **Processsor**: 4-core+ (AI Model uchun)

### Qo'shimcha Xizmatlar:
- Email xizmati (SMTP)
- SMS xizmati (Twilio/Nexmo)
- Cloud saqlash (AWS S3/Google Cloud)
- Backup xizmati (Automated)
- Monitoring xizmati (New Relic/DataDog)

---

**SecureGate - Zamonaviy, Xavfsizlik va Ishonch Bilan** 🔐🎥

