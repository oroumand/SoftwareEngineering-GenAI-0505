# طرح پیاده‌سازی: نگارش و انتشار پست وبلاگ

**Branch**: `001-write-publish-post` | **تاریخ**: 2026-08-10 | **Spec**: [spec.md](./spec.md)

**ورودی**: Specification قابلیت از `specs/001-write-publish-post/spec.md`

## خلاصه

این قابلیت یک Web API مبتنی بر Controller برای ایجاد، ویرایش، انتشار و خواندن پست‌های
وبلاگ فراهم می‌کند. `Post` به‌عنوان Aggregate اصلی در یک معماری Onion چهارلایه طراحی
می‌شود. Domain همهٔ Invariantهای محتوا، مالکیت و چرخهٔ وضعیت را محافظت می‌کند؛ Application
Use Caseها و Portها را هماهنگ می‌کند؛ Infrastructure ماندگاری PostgreSQL و Optimistic
Concurrency را پیاده‌سازی می‌کند؛ و API فقط مرز HTTP و Composition Root است.

عملیات مبتنی بر نسخهٔ قدیمیِ کلاینت با ETag opaque و Header اجباری `If-Match` محافظت
می‌شود. Infrastructure توکن نسخهٔ مستقل از Domain را نگه می‌دارد و `xmin` PostgreSQL نیز
رقابت پس از بررسی Precondition را اتمیک می‌کند. نخستین Commit موفق حفظ و هر درخواست با
نسخهٔ قدیمی، بدون بازنویسی داده، با `412 Precondition Failed` رد می‌شود. جزئیات تصمیم‌ها
در [research.md](./research.md) ثبت شده‌اند.

## زمینهٔ فنی

**زبان/نسخه**: C# روی .NET 10؛ همهٔ پروژه‌ها با `net10.0`

**وابستگی‌های اصلی**: ASP.NET Core MVC Controllers، Entity Framework Core 10،
`Npgsql.EntityFrameworkCore.PostgreSQL` سازگار با EF Core 10، OpenAPI داخلی ASP.NET Core

**ذخیره‌سازی**: PostgreSQL؛ Mapping مربوط به `Post` و Migrationها در Infrastructure

**آزمون**: xUnit v3، `Microsoft.AspNetCore.Mvc.Testing`، `Testcontainers.PostgreSql` و
`ArchUnitNET.xUnit`

**بستر هدف**: Web API قابل اجرا روی محیط سرور دارای .NET 10؛ PostgreSQL و Docker برای
آزمون‌های یکپارچه

**نوع پروژه**: Web API مبتنی بر Controller و معماری Onion

**اهداف کارایی**: Specification هدف عددی کارایی تعیین نکرده است؛ طرح هیچ آستانهٔ
خودسرانه‌ای اضافه نمی‌کند. Query فهرست باید همهٔ پست‌های منتشرشده را با ترتیب نزولی زمان
انتشار برگرداند.

**قیود**: عدم افشای پیش‌نویس؛ عدم تغییر جزئی در عملیات ناموفق؛ `If-Match` اجباری برای
ویرایش و انتشار؛ ETag برای Client opaque و بدون افشای `xmin`؛ نخستین Commit هم‌زمان موفق
حفظ شود؛ پست منتشرشده قابل ویرایش یا بازگشت نباشد؛ Domain فاقد Package خارجی باشد؛
Minimal API ممنوع است.

**مقیاس/دامنه**: حجم یا نرخ درخواست در Specification مشخص نشده است. دامنه فقط یک Aggregate
به نام `Post` و پنج عملیات HTTP را پوشش می‌دهد؛ فهرست مطابق FR-015 همهٔ پست‌های
منتشرشده را برمی‌گرداند و Pagination به این قابلیت افزوده نمی‌شود.

## بررسی Constitution

*Gate پیش از Phase 0: قبول. بررسی پس از Phase 1 نیز در انتهای همین بخش ثبت شده است.*

| اصل | طراحی و شاهد انطباق | وضعیت پیش از پژوهش | وضعیت پس از طراحی |
|---|---|---|---|
| بستر فنی یکپارچه | `global.json` و تنظیمات مشترک Build برای SDK سازگار با .NET 10؛ همهٔ پروژه‌ها `net10.0` | قبول | قبول |
| وابستگی‌ها فقط به سمت هسته | Domain بدون Project/Package Reference؛ Application فقط Domain؛ Infrastructure فقط Application و Domain؛ API فقط Composition Root | قبول | قبول |
| ساختار Aggregate‌محور | مصنوعات هر لایه ابتدا زیر `Posts/` و سپس بر اساس نوع فنی سازمان می‌یابند | قبول | قبول |
| دامنهٔ غنی و رفتارمحور | `Post` ایجاد، ویرایش و انتشار را با Value Objectهای عنوان و متن و کنترل مالکیت/وضعیت انجام می‌دهد | قبول | قبول |
| Application هماهنگ‌کنندهٔ Use Case | Handlerها Aggregate را از Port بارگذاری، Domain را فراخوانی و Commit را هماهنگ می‌کنند؛ قاعدهٔ کسب‌وکار ندارند | قبول | قبول |
| Controllerهای استاندارد و نازک | تمام Endpointهای کسب‌وکاری در `PostsController : ControllerBase`؛ بدون Minimal API و دسترسی مستقیم به Persistence | قبول | قبول |
| قواعد معماری قابل‌آزمون | Domain Unit Tests، Architecture Tests، Integration Tests و سناریوی Concurrency در راهنمای اعتبارسنجی الزامی‌اند | قبول | قبول |
| مرزهای ورودی و خروجی امن | هویت از انتزاع قابل‌جایگزینی، Authorization صریح، Validation مرزی، `ProblemDetails` کنترل‌شده و عدم افشای Draft | قبول | قبول |

هیچ تخطی یا استثنای نیازمند توجیه وجود ندارد. طراحی Phase 1 نیز هیچ وابستگی معکوس، منطق
Domain در لایهٔ بیرونی، Minimal API یا جزئیات حساس در پاسخ خطا اضافه نکرده است.

## ساختار پروژه

### مستندات این قابلیت

```text
specs/001-write-publish-post/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── openapi.yaml
└── checklists/
    └── requirements.md
```

`tasks.md` در این مرحله ایجاد نمی‌شود.

### Source Code برنامه‌ریزی‌شده در ریشهٔ Repository

```text
BlogSpecLab.slnx
global.json
Directory.Build.props
src/
├── BlogSpecLab.Domain/
│   └── Posts/
│       ├── Entities/
│       ├── ValueObjects/
│       └── Errors/
├── BlogSpecLab.Application/
│   └── Posts/
│       ├── Ports/
│       ├── CreateDraft/
│       ├── EditDraft/
│       ├── PublishDraft/
│       ├── ListPublished/
│       └── GetPublished/
├── BlogSpecLab.Infrastructure/
│   └── Posts/
│       ├── Persistence/
│       ├── Repositories/
│       └── Time/
└── BlogSpecLab.Api/
    └── Posts/
        ├── Controllers/
        ├── Models/
        ├── Mapping/
        ├── Identity/
        └── Errors/
tests/
├── BlogSpecLab.Domain.Tests/
│   └── Posts/
├── BlogSpecLab.Application.Tests/
│   └── Posts/
├── BlogSpecLab.Architecture.Tests/
└── BlogSpecLab.Api.IntegrationTests/
    └── Posts/
```

**تصمیم ساختار**: چهار پروژهٔ Production مرزهای Onion را صریح نگه می‌دارند. نام `Posts`
در همهٔ لایه‌ها ثابت است و نوع مصنوع فقط داخل پوشهٔ Aggregate تفکیک می‌شود. Testها خارج
از گراف Production قرار دارند. Migrationهای پایگاه داده در Infrastructure و Composition
Root در API باقی می‌مانند.

## طراحی اجزا و جریان‌ها

### Domain

- `Post` تنها Aggregate Root است و عملیات `CreateDraft`، `EditDraft` و `Publish` را ارائه
  می‌کند.
- `PostTitle` و `PostContent` ایجاد مقدار خالی یا فقط فاصله را رد می‌کنند.
- `AuthorId` مالک ثابت Aggregate است؛ انتقال مالکیت عملیاتی ندارد.
- `PostStatus` فقط گذار `Draft → Published` را مجاز می‌کند.
- متدهای ویرایش و انتشار `AuthorId` عامل را دریافت و مالکیت را داخل Aggregate بررسی
  می‌کنند.
- `Publish` زمان قابل‌اعتماد را دریافت، `PublishedAt` را ثبت و انتشار مجدد را رد می‌کند.
- Domain از EF Core، ASP.NET Core، Persistence و Package خارجی آگاه نیست.

### Application

- Use Caseهای `CreateDraft`، `EditDraft`، `PublishDraft`، `ListPublished` و `GetPublished`
  به‌صورت Sliceهای مستقل زیر `Posts/` تعریف می‌شوند.
- Portهای `IPostRepository`، `IUnitOfWork` و `IClock` در Application تعریف می‌شوند.
- هویت استخراج‌شده از مرز HTTP به‌صورت `AuthorId` ورودی Use Caseهای محافظت‌شده است؛
  Application به Claims یا ASP.NET Core وابسته نمی‌شود.
- Handlerها فقط بارگذاری، بررسی Version Token موردانتظار، فراخوانی رفتار Domain، Commit و
  Mapping نتیجه را هماهنگ می‌کنند.
- `PostVersion` یک مقدار opaque در قرارداد Application است. API فقط ETag معتبر را به آن
  تبدیل و Infrastructure آن را بدون افشای سازوکار ماندگاری اعتبارسنجی یا تولید می‌کند.
- نتیجه‌های قابل‌انتظار مانند Validation، Not Found، Precondition Required، Precondition
  Failed و Invalid State به خطای کنترل‌شدهٔ Application تبدیل می‌شوند؛ Exception زیرساختی
  از مرز خارج نمی‌شود.

### Infrastructure

- EF Core Mapping، `DbContext`، Repository و Unit of Work برای PostgreSQL پیاده می‌شوند.
- `version_token` یک Shadow Property تصادفی و opaque است که هنگام ایجاد و هر Mutation
  موفق می‌چرخد. Infrastructure آن را با Header ETag متناظر می‌کند؛ `xmin` یا نوع Provider
  هرگز به API یا Domain راه پیدا نمی‌کند.
- Repository ابتدا Version Token موردانتظار را فقط پس از احراز قابل‌مشاهده‌بودن Draft برای
  عامل بررسی می‌کند. نبود Header برای منبع قابل‌دسترس به `PreconditionRequired` و Token
  نامنطبق به `PreconditionFailed` تبدیل می‌شود؛ برای منبع غیرقابل مشاهده پاسخ 404 حفظ
  می‌شود.
- `xmin` به‌عنوان Shadow Concurrency Token نگاشت می‌شود تا رقابتی که بعد از بررسی Token
  رخ می‌دهد نیز اتمیک بماند. `DbUpdateConcurrencyException` به `PreconditionFailed` تبدیل
  می‌شود و هرگز Client Wins یا بازنویسی خودکار انجام نمی‌دهد.
- Query خوانندگان فقط `Published` را فیلتر می‌کند، همهٔ رکوردهای منتشرشده را می‌آورد و
  با `PublishedAt` نزولی مرتب می‌کند. برای زمان‌های برابر ترتیب ثانویه تحمیل نمی‌شود.
- `IClock` با زمان UTC پیاده می‌شود.

### API

- `PostsController : ControllerBase` پنج Endpoint قرارداد
  [OpenAPI](./contracts/openapi.yaml) را ارائه می‌کند.
- `ICurrentAuthorAccessor` در مرز API یک انتزاع قابل‌جایگزینی برای خواندن هویت معتبر است؛
  پیاده‌سازی اولیه هویت را از Context احراز هویت‌شده استخراج و به `AuthorId` تبدیل می‌کند.
- Controller فقط Binding و Validation مرزی، استخراج نویسنده، فراخوانی Use Case و Mapping
  نتیجه به HTTP را انجام می‌دهد.
- `ProblemDetails` قالب یکنواخت خطا است. نبود `If-Match` برای منبع قابل‌دسترس 428 و
  ETag قدیمی/نامنطبق 412 است؛ وضعیت نامعتبر Domain مانند ویرایش Post منتشرشده 409 باقی
  می‌ماند. برای جلوگیری از افشای Draft، مشاهدهٔ Draft از مسیر خواننده و دسترسی غیرمالک
  به Draft پاسخ همسان با منبع غیرقابل مشاهده می‌گیرد.
- ثبت‌نام، ورود، صدور Token و مدیریت حساب در این طرح وجود ندارد.

## راهبرد آزمون

- Domain Unit Tests تمام مسیرهای معتبر و نامعتبر ایجاد، ویرایش، مالکیت، انتشار و
  Invariantهای وضعیت را پوشش می‌دهند.
- Application Tests هماهنگی Portها، عدم Commit پس از خطا و Mapping تعارض را بررسی می‌کنند.
- Architecture Tests جهت همهٔ Project Referenceها، نبود Package/Namespace خارجی در
  Domain، عدم وابستگی Application به لایه‌های بیرونی، و نبود Infrastructure Concrete در
  Controller را محافظت می‌کنند.
- API Integration Tests روی PostgreSQL واقعی در Testcontainer قرارداد HTTP، پنهان‌بودن
  Draft، ترتیب فهرست، Atomicity، ETag opaque و If-Match را اثبات می‌کنند.
- آزمون Concurrency دو Edit یا Publish با ETag اولیهٔ مشترک می‌فرستد؛ دقیقاً یکی موفق و
  ETag جدید دریافت می‌کند و دیگری `412 Precondition Failed` می‌گیرد، حتی اگر پس از تغییر
  نخست به سرور برسد. دادهٔ نهایی برابر نتیجهٔ درخواست موفق است.

## ردیابی نیازمندی‌ها به طراحی

| نیازمندی‌ها | جزء اصلی | شاهد طراحی/آزمون |
|---|---|---|
| FR-001 تا FR-004 | CreateDraft، `Post.CreateDraft`، Current Author | Domain و API Integration Tests |
| FR-005 تا FR-008 | EditDraft، `Post.EditDraft` | Domain و Application Tests |
| FR-009 تا FR-013 | PublishDraft، `Post.Publish`، `IClock` | Domain و Integration Tests |
| FR-014 تا FR-017 | Published Queries و Reader Controllers | Integration/Contract Tests |
| FR-018 | ETag/If-Match، `PostVersion` opaque، `version_token`، `xmin` و Precondition Mapping | API و PostgreSQL Concurrency Integration Test |

## پیگیری پیچیدگی

هیچ تخطی از Constitution وجود ندارد؛ جدول توجیه پیچیدگی لازم نیست.
