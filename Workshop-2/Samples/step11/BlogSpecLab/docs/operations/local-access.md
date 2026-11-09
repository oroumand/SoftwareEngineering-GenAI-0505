# ابزار محلی دسترسی

این ابزار مربوط به T009 است؛ endpoint مدیریت حساب ندارد و پیش از شروع HTTP Host
اجرا می‌شود. کد آماده است؛ اجرای موفق روی Schema واقعی و آزمون لغو هویت/مجوز
تا ایجاد و اعمال Migration اولیه در T017 معوق است. فرمان‌ها Schema نمی‌سازند.

از ریشهٔ مخزن و با تنظیم همان `ConnectionStrings:BlogDatabase` میزبان اجرا کنید.
مسیر نسبی SQLite نسبت به پوشهٔ اجرای فرایند است؛ برای چند ابزار، مسیر مطلق یکسان
را در تنظیمات محیط به‌کار ببرید. در صورت نیاز، مقدار تنظیم را از secret storage
وارد محیط کنید؛ آن را در تاریخچهٔ فرمان یا خروجی ثبت نکنید.

```powershell
dotnet run --no-launch-profile --project src/03.Endpoints/Api -- access provision 2026-12-31T23:59:59Z
dotnet run --no-launch-profile --project src/03.Endpoints/Api -- access revoke-credential <CredentialId>
dotnet run --no-launch-profile --project src/03.Endpoints/Api -- access revoke-permissions <UserId>
```

زمان انقضا ورودی صریح اپراتور و UTC است و باید در آینده باشد؛ تاریخ بالا فقط مثال
قالب است. Provision در هر اجرا یک batch جدید شامل پنج Author، پنج Editor و یک
AuthorEditor با شناسه‌های مستقل و tokenهای حاصل از ۳۲ بایت تصادف رمزنگارانه می‌سازد.
نگاشت نقش به مجوز فقط در این ابزار سروری است. لغو مجوز تمام Permissionهای عملیاتی
را خاموش می‌کند؛ Credential همچنان معتبر می‌ماند. لغو Credential مستقل است؛ هیچ
کاربر یا رابطهٔ تاریخی حذف نمی‌شود.

## تحویل محرمانه

ابزار Windows بستهٔ هر batch را فقط یک‌بار در
`%LOCALAPPDATA%/BlogSpecLab/Secrets/<random-id>.protected` تحویل می‌دهد؛ token خام
در Console، DB یا فایل مخزن نوشته نمی‌شود. بسته شامل `UserId`، `CredentialId`
و `Token` است و تمام JSON با ASP.NET Core Data Protection رمزگذاری می‌شود.
کلیدها در زیرپوشهٔ `keys` با Windows DPAPI همان حساب سیستم محافظت می‌شوند.
مسیر مخزن و مسیر دارای junction/symlink پذیرفته نمی‌شود. فرایند باید با همان
حساب Windows خوانندهٔ secret اجرا شود؛ ابزار قابلیت بازیابی token از DB ندارد.

مصرف‌کنندهٔ محلی دارای ASP.NET Core shared framework، با همان حساب Windows و
`SetApplicationName("BlogSpecLab.LocalAccess")`، provider را روی پوشهٔ `keys`
می‌سازد و با `CreateProtector("BlogSpecLab.LocalAccess.v1").Unprotect(payload)`
JSON را فقط در حافظه می‌خواند. Credential کاربر مربوط را مستقیماً به header
Authorization ابزار HTTP یا secret storage شخصی او منتقل کنید؛ JSON و token
را چاپ یا در فایل معمولی export نکنید.

تحویل پیش از Commit انجام می‌شود تا شکست secret storage باعث rollback کل batch
شود؛ شکست Commit نیز فایل تحویل را پاک می‌کند. قطع ناگهانی فرایند میان این دو
مرحله می‌تواند بسته‌ای از Credentialهای ثبت‌نشده باقی بگذارد؛ آن بسته هویت معتبر
ایجاد نمی‌کند و پس از بررسی اپراتور باید حذف شود. این تحویل فایل و DB یک تراکنش
توزیع‌شده ندارند.

## اعتبارسنجی معوق

پس از T017، روی دیتابیس Migrationشده Provision و Authentication واقعی، انقضا و
لغو Credential، خواندن تازهٔ Permission و لغو Permission با هویت همچنان معتبر
بررسی شوند. این سند شاهد موفقیت اجرایی آن مسیرها نیست.
