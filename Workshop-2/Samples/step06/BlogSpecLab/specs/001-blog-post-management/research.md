# پژوهش طراحی Feature

**تاریخ**: 2026-10-12. خروجی پیشنهادی Plan؛ مرجع رفتار [spec.md](spec.md) و مرجع اصول [Constitution](../../.specify/memory/constitution.md) است.

## تصمیم‌های موجود

C#، ASP.NET Core، .NET 10، معماری یکپارچه، ساختار پروژه‌ها و جهت وابستگی‌ها از Constitution گرفته شده‌اند و انتخاب مجدد نمی‌شوند. API بدون صفحهٔ وب، منع خودتأییدی و ثبات انتشار از Spec هستند. سند Technical Baseline دیگری در مخزن موجود نیست؛ پایگاه داده در Constitution صریحاً تعیین نشده است.

## R1 — ذخیره‌سازی

- **تصمیم**: EF Core 10 با SQLite فایل‌محور در DataAccess؛ migrations برای اجرای محلی آموزشی تک‌نمونه‌ای.
- **دلیل انتخاب**: دادهٔ پایدار، تراکنش و آزمون provider واقعی لازم‌اند؛ نصب سرویس دیتابیس خارجی برای دامنهٔ فعلی ضروری نیست.
- **گزینه‌های جایگزین بررسی‌شده**: PostgreSQL و SQL Server با هزینهٔ provision سرویس برای مقیاسی که درخواست نشده؛ Dapper/SQL مستقیم با نگاشت دستی بیشتر؛ حافظه یا EF InMemory بدون دوام و شاهد معتبر تراکنش.
- **محدودیت**: SQLite یک نویسندهٔ هم‌زمان و بدون rowversion خودکار است. زمان‌های Persistence به DateTime UTC نگاشت می‌شوند. busy/locked پس از timeout محدود، خطای زیرساخت است، نه موفقیت یا لزوماً تعارض نسخه. برخی migrations جدول را بازسازی می‌کنند؛ migration روی دیتابیس مشترک هم‌زمان اجرا نمی‌شود. این تصمیم ظرفیت production یا چندنمونه‌ای را تضمین نمی‌کند.
- **منابع**: [EF Core 10](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-10.0/whatsnew)، [محدودیت provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)، [Isolation SQLite](https://www.sqlite.org/isolation.html)، [خطاهای SQLite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/database-errors).
- **کاندید Technical Baseline پروژه**: EF Core برای Persistence رابطه‌ای؛ SQLite فقط انتخاب محلی همین Feature است.

## R2 — رقابت و Commit اتمی

- **تصمیم**: Version از نوع Guid با IsConcurrencyToken؛ تعویض در هر تغییر Aggregate و ETag/If-Match در HTTP. تغییر Post و تصمیم/انتشار در یک Commit اتمی. خواندن UserAccess و Post برای فرمان تغییردهنده داخل تراکنش کوتاه نوشتن SQLite، با acquiring write transaction پیش از خواندن؛ بدون cache مجوز. DbContext مستقل برای هر درخواست.
- **دلیل انتخاب**: نوشتن مشروط مکمل بررسی وضعیت Application است؛ rollback مانع تصمیم یتیم می‌شود. تراکنش کوتاه اجازه نمی‌دهد تغییر مجوز اپراتوری میان خواندن مجوز و Commit از کنترل عبور کند. Version همچنان فرمان کاربر بر مشاهدهٔ قدیمی را رد می‌کند.
- **گزینه‌های جایگزین بررسی‌شده**: rowversion مختص SQL Server؛ قفل حافظه محدود به فرایند؛ تراکنش از زمان مشاهده تا تصمیم انسان طولانی؛ ذخیره‌های مستقل فاقد اتمیسیته؛ retry فرمان روی وضعیت تازه ناقض تصمیم مشاهده‌شده.
- **جزئیات**: SaveChanges واحد برای همهٔ تغییرات؛ transaction صریح برای پوشش خواندن مجوز نیز لازم است. تغییر اپراتوری مجوز از همان DB و تراکنش نوشتن استفاده می‌کند. retry محدود فقط برای گرفتن lock پیش از تصمیم و بدون اثر ذخیره‌شده است؛ خطای نهایی 503. retry تجاری انجام نمی‌شود؛ استثنای محدود بازخوانی Publish ثبت‌شده در قرارداد آمده است.
- **منابع**: [Concurrency EF](https://learn.microsoft.com/en-us/ef/core/saving/concurrency)، [تراکنش EF](https://learn.microsoft.com/en-us/ef/core/saving/transactions)، [تراکنش Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions).
- **کاندید Technical Baseline پروژه**: Version Aggregate، Commit اتمی و رد فرمان بر مشاهدهٔ قدیمی؛ قفل SQLite مخصوص provider است.

## R3 — محتوای تأییدشده و حذف

- **تصمیم**: ContentRevision مستقل از Version؛ Snapshot عنوان/متن در ReviewDecision؛ Publication به تأیید جاری اشاره می‌کند. حذف منطقی، بدون سیاست purge جدید.
- **دلیل انتخاب**: انتقال وضعیت محتوا را تغییر نمی‌دهد، ولی Version را عوض می‌کند؛ revision و Snapshot محتوای دقیق بررسی را روشن می‌کنند. حذف منطقی خروج از گردش کار و حفظ روابط تصمیم‌ها را ساده می‌کند.
- **گزینه‌های جایگزین بررسی‌شده**: تنها Version رقابت اعتبار تأیید را اشتباه از بین می‌برد؛ hash محتوا نیازمند الگوریتم/نرمال‌سازی؛ تاریخچهٔ کامل نسخه‌ها و Event Sourcing فراتر از نیاز؛ حذف فیزیکی نیازمند مدیریت حذف روابط، بدون الزام مشخص.
- این تصمیم مخصوص گردش این Feature است. لغو مجوز بعدی تأییدکننده، تأیید تاریخی را باطل نمی‌کند؛ محتوای Published ثابت است.

## R4 — هویت بدون سامانهٔ قبلی فرضی

- **تصمیم**: برای محیط بستهٔ آموزشی، opaque bearer credential ازپیش‌تخصیص‌یافته. AuthenticationHandler در Api فقط هویت را استخراج می‌کند؛ verifier در DataAccess با قرارداد Application. مجوزهای جاری در UserAccess و کنترل هر مورد کاربرد Application هستند.
- **دلیل انتخاب**: هیچ IdP قبلی فرض نشده و ورود/ثبت‌نام تعریف نشده است. provision اپراتوری پذیرش محلی را بدون API مدیریت حساب فراهم می‌کند.
- **گزینه‌های جایگزین بررسی‌شده**: JWT از issuer معتبر خارجی مناسب اتصال عمومی آینده ولی نیازمند provision IdP/client/user واقعی؛ ASP.NET Identity با ورود/ثبت‌نام خارج از دامنه؛ JWT خودساخته و header نقش/مالکیت غیرقابل اعتماد؛ نقش داخل token به‌تنهایی فاقد تازگی لازم برای لغو دسترسی.
- **حدود**: حداقل ۳۲ بایت تصادف رمزنگارانه؛ ذخیرهٔ hash، UserId، ExpiresAt، RevokedAt. token خام خارج از repo/log، فقط هنگام provision تحویل و در secret storage نگهداری می‌شود. HTTPS، بدون Credential ثابت یا bypass. ابزار اپراتوری محدود provision/لغو مجوز در Implementation باید ساخته و مستند شود؛ endpoint حساب کاربری ایجاد نمی‌شود. پیکربندی ناقص محیط پذیرش باید راه‌اندازی را متوقف کند. Credential نامعتبر 401؛ مجوز لغوشده با Credential معتبر 403.
- **منابع**: [راهنمای JWT و محدودیت token خودساخته به محیط بسته/آزمون](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0)، [Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-10.0)، [RandomNumberGenerator](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.getbytes?view=net-10.0)، [Bearer و TLS](https://datatracker.ietf.org/doc/html/rfc6750).
- **کاندید Technical Baseline پروژه**: تفکیک هویت از مجوز جاری سروری؛ تفصیل فنی اصل موجود مرز مسئولیت‌ها، نه بازتعریف آن. نوع Credential فقط انتخاب محیط بسته است.

## R5 — قرارداد HTTP

- **تصمیم**: Minimal APIs نازک، DTO صریح و رد فیلد اضافه؛ ProblemDetails با code ثابت. قرارداد Markdown اکنون، تولید OpenAPI با Microsoft.AspNetCore.OpenApi 10 در Implementation؛ بدون UI یا Swagger UI package.
- **دلیل انتخاب**: HTTP تنها رابط این نسخه است؛ عملیات روشن و پیام خطای قابل فهم برای SC-005 لازم‌اند. امکانات استاندارد کافی‌اند و قواعد در Application باقی می‌مانند.
- **گزینه‌های جایگزین بررسی‌شده**: Controllerها معتبر ولی نیاز خاصی به آن‌ها نیست؛ قالب خطای اختصاصی قرارداد بیشتری می‌خواهد؛ صفحهٔ وب و مسیر تغییر مستقیم status با دامنه ناسازگارند.
- **منابع**: [OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0)، [ProblemDetails](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0).
- **کاندید Technical Baseline پروژه**: ProblemDetails با code ثابت؛ Minimal API فعلاً انتخاب Feature است.

## R6 — آزمون

- **تصمیم**: xUnit با runner و Microsoft.NET.Test.Sdk پایدار سازگار؛ Microsoft.AspNetCore.Mvc.Testing 10 و WebApplicationFactory. Unit بدون سرویس بیرونی؛ Integration با handler واقعی و SQLite موقت فایل‌محور. نسخهٔ دقیق بسته‌های آزمون هنگام restore و بررسی سازگاری تثبیت می‌شود.
- **دلیل انتخاب**: Unit به‌تنهایی عدم افشای HTTP و رفتار provider را اثبات نمی‌کند. دو اتصال و ترتیب Commit کنترل‌شده بدون sleep، رقابت را بازتولیدپذیر می‌کنند.
- **گزینه‌های جایگزین بررسی‌شده**: NUnit/MSTest توانمند ولی نیاز خاصی برای انتخاب آن‌ها نیست؛ EF InMemory شاهد تراکنش واقعی نیست؛ آزمون صرفاً دستی رگرسیون امنیتی را پوشش نمی‌دهد.
- **منابع**: [راهبرد آزمون EF](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy)، [Integration ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)، [xUnit](https://xunit.net/).
- **کاندید Technical Baseline پروژه**: xUnit و WebApplicationFactory، پس از بازبینی انسانی.

## نتیجه

مجهولات فنی لازم حل شده‌اند و تعارضی با Constitution یافت نشد. هیچ تصمیم خودکار به Baseline یا Constitution منتقل نشده است. ظرفیت، مقصد production و IdP عمومی آینده در دامنهٔ این Feature نیستند.
