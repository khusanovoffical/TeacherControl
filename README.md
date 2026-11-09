# SchoolControl · TeacherControl & Server

Professional maktab kompyuterlarini boshqarish ekotizimi — O‘qituvchi boshqaruv markazi va Central API Serveri.

Ushbu tizim 1 ta o‘qituvchi kompyuteri va 20 ta o‘quvchi kompyuterini yagona Ethernet lokal tarmog‘ida (internet bo‘lmagan taqdirda ham) to‘liq boshqarish, ekranlarni kuzatish, qulflash, fayllar tarqatish va ilovalar nazoratini amalga oshirish uchun mo‘ljallangan.

---

## Asosiy Imkoniyatlar

1. **Dashboard & Statistika:**
   - Jami qurilmalar, onlayn, oflayn va qulflangan kompyuterlar soni.
   - Tezkor qidiruv va server aloqasi holati.
2. **Jonli Ekranlar (4 × 5 Grid):**
   - Barcha 20 ta kompyuterning jonli ekranlarini bitta sahifada ko‘rish.
   - Har bir kartani bosganda alohida katta oynada ochish, Fullscreen rejimi.
   - **Masofaviy Boshqaruv (Remote Control):** O‘quvchi ruxsati va ko‘rinadigan sessiya doirasida sichqoncha va klaviatura bilan boshqarish.
3. **Qurilmalarni Boshqarish:**
   - Bir yoki bir nechta kompyuterni tanlash (Multi-select).
   - Qulflash (Lock) va Qulfdan chiqarish (Unlock).
   - Qayta ishga tushirish (Restart) va O‘chirish (Shutdown) — tasdiqlash dialogi bilan.
4. **Fayllar Almashinuvi:**
   - Barcha yoki tanlangan o‘quvchilarga topshiriq va materiallarni tarqatish.
   - O‘quvchilar yuborgan fayllarni qabul qilish va yuklab olish.
5. **Ilovalar Siyosati (Policy Enforcer):**
   - Dars paytida taqiqlangan dasturlarni (o‘yinlar, messenjerlar) bloklash va ularni darhol barcha kompyuterlarga qo‘llash.
6. **Zero-Configuration Lokal Server:**
   - O‘qituvchi kompyuterida server ishga tushishi bilan UDP Beacon tarqatiladi va o‘quvchi kompyuterlari serverni avtomatik topadi. Internet shart emas!

---

## Ishga Tushirish

### 1. Serverni ishga tushirish:
```powershell
dotnet run --project Server/SchoolControl.Server.csproj --urls "http://0.0.0.0:5123"
```

### 2. O‘qituvchi dasturini ishga tushirish:
```powershell
dotnet run --project TeacherControl.csproj
```

---

## Build va Publish

GitHub Actions orqali avtomatik ravishda Windows x64 uchun mustaqil (Self-Contained) `.exe` fayllar generatsiya qilinadi.

Qo‘lda build qilish uchun:
```powershell
dotnet publish TeacherControl.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
dotnet publish Server/SchoolControl.Server.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Xavfsizlik va Maxfiylik

- Barcha ekran kuzatuvlari o‘quvchi kompyuterida aniq ko‘rinib turuvchi yashil indikator ostida amalga oshiriladi.
- Yashirin josuslik, yashirin screenshot yoki yashirin boshqaruv mexanizmlari mavjud emas.
- Maktab tarmog‘ida xavfsiz va barqaror ishlash uchun optimallashtirilgan.
