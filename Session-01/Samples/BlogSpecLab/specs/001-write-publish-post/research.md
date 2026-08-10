# پژوهش فنی: نگارش و انتشار پست وبلاگ

## وضعیت Repository

Repository در زمان طراحی فاقد Solution، پروژهٔ .NET، Package، Persistence یا کد اجرایی
است و فقط مصنوعات Spec Kit را دارد. بنابراین هیچ فناوری موجودی برای حفظ سازگاری تحمیل
نمی‌شود. همهٔ انتخاب‌های زیر نقطهٔ شروع طراحی هستند و نصب آن‌ها به مرحلهٔ پیاده‌سازی
موکول می‌شود.

## تصمیم ۱: Persistence Provider

**تصمیم**: PostgreSQL با Entity Framework Core 10 و
`Npgsql.EntityFrameworkCore.PostgreSQL` هم‌نسخه با خط اصلی EF Core 10.

**دلیل**:

- Provider رسمی اکوسیستم PostgreSQL برای EF Core است و Concurrency Token بومی `xmin` را
  پشتیبانی می‌کند.
- رفتار Concurrency لازم در FR-018 با یک کنترل اتمیک در محل Commit و بدون افزودن فناوری
  Persistence به Domain قابل تحقق است.
- PostgreSQL برای زمان انتشار، UUID، Query مرتب‌شده و Transactionهای این قابلیت امکانات
  رابطه‌ای لازم را دارد.
- آزمون یکپارچه می‌تواند همان Provider واقعی Production را در Container اجرا کند و از
  اختلاف رفتار Provider آزمایشی جلوگیری کند.

**گزینه‌های بررسی‌شده**:

- **SQL Server + `rowversion`**: راهکار قدرتمند و خودکار Concurrency است، اما وابستگی
  عملیاتی و مجوز/تصویر سنگین‌تری نسبت به PostgreSQL ایجاد می‌کند و Repository فعلی نیز
  الزام Microsoft SQL Server ندارد.
- **SQLite**: برای نمونهٔ کوچک ساده است، اما Provider آن توکن Concurrency تولیدشده توسط
  پایگاه داده را پشتیبانی نمی‌کند و برخی محدودیت‌های Migration و نوع داده دارد؛ بنابراین
  برای اثبات FR-018 انتخاب اصلی مناسبی نیست.
- **EF Core InMemory**: پایگاه دادهٔ رابطه‌ای و Provider Production نیست و رفتار واقعی
  Transaction، Query و Optimistic Concurrency را اثبات نمی‌کند.
- **Document Database**: دامنه به آن نیاز ندارد و مدل رابطه‌ای سادهٔ Post را بدون مزیت
  مشخص پیچیده می‌کند.

**منابع**:

- [مستند Npgsql دربارهٔ Concurrency Token و `xmin`](https://www.npgsql.org/efcore/modeling/concurrency.html)
- [محدودیت‌های Provider SQLite در EF Core](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)

## تصمیم ۲: Optimistic Concurrency مبتنی بر ETag و If-Match

**تصمیم**: API در پاسخ ایجاد Draft یک ETag strong و opaque برمی‌گرداند. EditDraft و
PublishDraft باید دقیقاً یک Header `If-Match` با همان ETag ارائه کنند. Infrastructure برای
هر Post یک Shadow Property تصادفی به نام `version_token` نگه می‌دارد، آن را هنگام ایجاد و
هر Mutation موفق می‌چرخاند و به ETag تبدیل می‌کند. `version_token` به هیچ مدل Domain یا
بدنهٔ HTTP راه پیدا نمی‌کند. `xmin` نیز Shadow Concurrency Token دوم برای حفاظت از فاصلهٔ
بین بررسی `If-Match` و Commit است.

درخواست بدون `If-Match`، پس از تأیید قابل‌دسترس‌بودن Draft برای عامل، `428 Precondition
Required` می‌گیرد. درخواست با ETag نامنطبق یا عملیاتی که پس از بررسی ETag توسط Commit
دیگری پیشی گرفته است، `412 Precondition Failed` می‌گیرد. وضعیت نامعتبر Domain همچنان
`409 Conflict` است. هیچ Retry خودکار، Merge یا Last-Write-Wins انجام نمی‌شود.

**دلیل**:

- Client به‌جای نسخهٔ تازه‌ای که سرور هنگام رسیدن درخواست بارگذاری می‌کند، نسخه‌ای را که
  واقعاً مشاهده کرده شرط می‌گذارد؛ بنابراین درخواست تأخیری با نسخهٔ قدیمی موفق نمی‌شود.
- `version_token` ETag را opaque نگه می‌دارد و از افشای `xmin`، شمارنده یا جزئیات
  PostgreSQL جلوگیری می‌کند.
- `xmin` تضمین می‌کند دو درخواست با ETag معتبر و یکسان که از Precondition عبور کرده‌اند
  نیز فقط یک Commit موفق داشته باشند.
- Domain فقط رفتار کسب‌وکار را می‌بیند و وابسته به HTTP، ETag، EF Core، PostgreSQL یا
  `xmin` نمی‌شود.

**گزینه‌های بررسی‌شده**:

- **ارسال مستقیم `xmin` یا شمارنده در Header**: شرط کلاینت را فراهم می‌کند، اما ETag
  opaque نیست و جزئیات ماندگاری را افشا می‌کند.
- **ETag امضاشده با Payload قابل‌خواندن**: جعل را دشوار می‌کند، اما مقدار نسخهٔ داخلی را
  همچنان برای Client قابل مشاهده می‌گذارد.
- **فقط `xmin` در EF Core**: تعارض Commit هم‌زمان را تشخیص می‌دهد، اما درخواست تأخیری
  می‌تواند نسخهٔ جدید را بارگذاری و موفق شود؛ بنابراین FR-018 را کامل پوشش نمی‌دهد.
- **Version عددی Domain**: Domain را با نگرانی ماندگاری/هماهنگی کلاینت آلوده می‌کند و به
  ETag استاندارد HTTP نیاز باقی می‌ماند.
- **Timestamp زمانی یا Pessimistic Locking**: به‌ترتیب دقت Version قابل اعتماد ندارند یا
  درخواست‌ها را Block و طراحی را پیچیده می‌کنند.
- **Last-Write-Wins یا Client-Wins**: مستقیماً FR-018 را نقض می‌کند.

**منابع**:

- [If-Match و 412 در RFC](https://datatracker.ietf.org/doc/rfc7232/)
- [428 Precondition Required در RFC](https://datatracker.ietf.org/doc/rfc6585/)
- [ETag و Headerهای پاسخ در ASP.NET Core](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.headers.responseheaders?view=aspnetcore-10.0)

## تصمیم ۳: کتابخانه‌ها و سطوح آزمون

**تصمیم**:

- `xunit.v3` به‌عنوان Framework آزمون و Microsoft Testing Platform از طریق .NET 10
- Assertionهای داخلی xUnit؛ بدون افزودن کتابخانهٔ Assertion جداگانه
- `ArchUnitNET.xUnit` برای آزمون قواعد معماری
- `Microsoft.AspNetCore.Mvc.Testing` برای اجرای Host در آزمون قرارداد/یکپارچه
- `Testcontainers.PostgreSql` برای PostgreSQL واقعی و ایزوله

نسخهٔ Stable سازگار با `net10.0` در زمان پیاده‌سازی Pin می‌شود و مدیریت نسخه‌ها به‌صورت
مرکزی انجام خواهد شد؛ این مرحله هیچ Package نصب نمی‌کند.

**دلیل**:

- xUnit v3 با .NET جدید و Microsoft Testing Platform سازگار است و آزمون‌های Unit، Theory
  و Fixtureهای Integration را پوشش می‌دهد.
- ArchUnitNET قواعد Project/Namespace را به Assertionهای CI تبدیل می‌کند.
- WebApplicationFactory قرارداد واقعی Controller، Authorization و Error Mapping را
  بررسی می‌کند.
- Testcontainers همان PostgreSQL و `xmin` واقعی را اجرا می‌کند؛ Double یا SQLite قادر به
  اثبات کامل رفتار Provider-specific نیست.

**گزینه‌های بررسی‌شده**:

- **MSTest یا NUnit**: هر دو قابل استفاده‌اند، اما انتخاب یک Framework کافی است و xUnit
  Fixtureهای رایج برای ASP.NET Core و Testcontainers دارد.
- **NetArchTest**: قواعد ساده را پوشش می‌دهد؛ ArchUnitNET برای تعریف و ترکیب Ruleهای
  معماری Onion صریح‌تر انتخاب شد.
- **Mock-only Persistence Tests**: سریع‌اند اما Atomicity و `xmin` را اثبات نمی‌کنند؛ فقط
  برای Application orchestration مناسب‌اند، نه آزمون FR-018.
- **SQLite Integration Tests**: رفتار Provider Production و توکن `xmin` را بازتولید
  نمی‌کنند.

**منابع**:

- [راهنمای xUnit v3](https://xunit.net/docs/getting-started/v3/getting-started)
- [اجرای آزمون با .NET 10](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test)
- [راهنمای ArchUnitNET](https://archunitnet.readthedocs.io/en/stable/guide/)
- [ماژول PostgreSQL در Testcontainers for .NET](https://dotnet.testcontainers.org/modules/postgres/)

## تصمیم ۴: قرارداد خطا

**تصمیم**: همهٔ پاسخ‌های خطای HTTP از `ProblemDetails` با Content Type برابر
`application/problem+json` استفاده می‌کنند. فیلدهای استاندارد `type`، `title`، `status`
و `traceId` ارائه می‌شوند؛ Validation می‌تواند مجموعهٔ خطاهای فیلدی کنترل‌شده داشته باشد.
Stack Trace، Exception، SQL، شناسهٔ داخلی زیرساخت و محتوای Draft هرگز برگردانده نمی‌شود.

Mapping طراحی:

| وضعیت | HTTP | نوع مسئله |
|---|---:|---|
| ورودی خالی یا فقط فاصله | 400 | `validation-error` |
| نبود هویت معتبر در عملیات نویسنده | 401 | `authentication-required` |
| منبع وجود ندارد یا برای عامل قابل مشاهده نیست | 404 | `post-not-found` |
| نبود `If-Match` برای Draft قابل‌دسترس | 428 | `precondition-required` |
| `If-Match` نامنطبق یا Commit بر پایهٔ نسخهٔ قدیمی | 412 | `precondition-failed` |
| وضعیت Domain نامعتبر | 409 | `post-state-conflict` |
| خطای پیش‌بینی‌نشده | 500 | `internal-error` بدون جزئیات داخلی |

برای نویسندهٔ غیرمالک و Reader درخواست‌کنندهٔ Draft، پاسخ غیرقابل تمایز از منبع
غیرقابل مشاهده است تا وجود یا محتوای Draft افشا نشود. این تصمیم نوع پاسخ فنی را مشخص
می‌کند و قاعدهٔ محصولی تازه‌ای دربارهٔ دسترسی ایجاد نمی‌کند.

**گزینه‌های بررسی‌شده**:

- **Envelope اختصاصی خطا**: نیازمند قرارداد و نگهداری موازی است، در حالی که ASP.NET Core
  قالب استاندارد و قابل توسعه فراهم می‌کند.
- **Exception خام یا متن آزاد**: ماشین‌خوان و یکنواخت نیست و خطر افشای جزئیات داخلی دارد.
- **403 برای هر دسترسی غیرمالک**: وجود Draft را قابل استنباط می‌کند؛ 404 همسان برای منبع
  غیرقابل مشاهده از الزام عدم افشا بهتر محافظت می‌کند.
- **409 برای نسخهٔ قدیمی**: با معنای استاندارد شکست If-Match سازگار نیست و Client را از
  تشخیص Precondition ناموفق و وضعیت نامعتبر Domain بازمی‌دارد.
- **412 بدون 428 برای Header مفقود**: شرط پیش‌نیاز و مقدار نامنطبق را یکی می‌کند؛ 428 به
  Client می‌گوید درخواست باید مشروط و همراه `If-Match` ارسال شود.

**منابع**:

- [مدیریت خطا در ASP.NET Core 10](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
- [Web API مبتنی بر Controller و ProblemDetails](https://learn.microsoft.com/en-us/aspnet/core/web-api/?view=aspnetcore-10.0)

## تصمیم ۵: هویت و زمان قابل‌جایگزینی

**تصمیم**: API هویت احرازشده را از `ICurrentAuthorAccessor` استخراج و `AuthorId` را به
Use Case می‌دهد. صدور و اعتبارسنجی Credential جزو این قابلیت نیست. Application زمان را
از Port به نام `IClock` دریافت و به رفتار انتشار Domain می‌دهد؛ پیاده‌سازی زمان UTC در
Infrastructure است.

**دلیل**: Controller از سازوکار خاص احراز هویت جدا می‌ماند، تست‌ها هویت و زمان قطعی تزریق
می‌کنند، Application به ASP.NET Core وابسته نمی‌شود و Domain بدون دسترسی مستقیم به Clock
Invariant خود را حفظ می‌کند.

**گزینه‌های بررسی‌شده**:

- خواندن مستقیم Claims در Application: جهت وابستگی را نقض می‌کند.
- پیاده‌سازی ثبت‌نام یا صدور Token: صریحاً خارج از دامنه است.
- استفادهٔ مستقیم از زمان سیستم در Domain: آزمون‌پذیری و قطعیت زمان انتشار را کاهش می‌دهد.

## نتیجهٔ پژوهش

همهٔ تصمیم‌های لازم برای Phase 1 مشخص شده‌اند. هیچ ابهام حل‌نشده و هیچ تعارضی با
Constitution باقی نمانده است.
