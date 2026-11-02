# برنامهٔ فنی: مدیریت و انتشار کنترل‌شدهٔ پست‌های وبلاگ

**Feature / Branch خروجی Spec Kit**: `001-blog-post-management` | **تاریخ**: 2026-10-12 | **مشخصات**: [spec.md](spec.md)

شاخهٔ Git ایجاد نشده است؛ پوشهٔ پروژه Git ندارد. ورودی برنامه مشخصات مورد تأیید کاربر برای این مرحله است؛ عنوان وضعیت قبلی Spec در این فرمان تغییر نکرده است. این سند پیشنهاد طراحی است، نه گزارش پیاده‌سازی یا تصویب Baseline.

## خلاصه

API مدیریت، بررسی، تأیید محتوای مشخص و انتشار با مجوز فعلی ارائه می‌شود. Post انتقال وضعیت و ثبات محتوای منتشرشده را کنترل می‌کند. Application مجوز و مالکیت را بررسی و موارد کاربرد را هماهنگ می‌کند. تغییر Post و تصمیم بررسی در Persistence اتمی ثبت می‌شوند. ETag/If-Match از فرمان بر مشاهدهٔ قدیمی جلوگیری می‌کند. مسیر عمومی فقط Published را نشان می‌دهد.

## زمینهٔ فنی

| مورد | طراحی |
| --- | --- |
| زبان و نسخه | C# / .NET 10؛ الزام موجود Constitution |
| وابستگی اصلی | ASP.NET Core 10، EF Core 10 SQLite و ابزار migrations، Microsoft.AspNetCore.OpenApi 10 |
| ذخیره‌سازی | SQLite فایل‌محور محلی؛ اجرای آموزشی تک‌نمونه‌ای |
| آزمون | xUnit با runner و Microsoft.NET.Test.Sdk پایدار سازگار؛ Microsoft.AspNetCore.Mvc.Testing 10؛ SQLite واقعی |
| بستر هدف | اجرای آموزشی محلی با .NET 10 و HTTPS؛ مقصد production در Spec تعیین نشده |
| نوع پروژه | HTTP API یکپارچه با ساختار الزام‌شدهٔ Constitution؛ بدون صفحهٔ وب |
| هدف کارایی | SLA، throughput و latency تعیین نشده‌اند؛ هدف ساختگی اضافه نمی‌شود |
| محدودیت | مجوز فعلی، ممنوعیت خودتأییدی، تأیید وابسته به محتوا، ثبات انتشار، عدم افشای محتوای خصوصی، Commit اتمی |
| مقیاس و دامنه | FR-001 تا FR-016؛ پذیرش انسانی با حداقل پنج Author و پنج Editor؛ ظرفیت عملیاتی تعیین نشده |

مجهولات اولیهٔ ذخیره‌سازی، هویت، رقابت، سبک API و آزمون در [research.md](research.md) حل شدند. ابهام طراحی لازم برای این Feature باقی نمانده است. نبود ظرفیت و مقصد production محدودیت ورودی است، نه تصمیم معوق لازم برای این نسخه.

## کنترل Constitution

مرجع [Constitution نسخهٔ 1.0.0](../../.specify/memory/constitution.md) است؛ جدول شاهد انطباق است و اصول جدید تعریف نمی‌کند.

| دروازه | پیش از پژوهش | پس از طراحی / شاهد |
| --- | --- | --- |
| اصل اول: SDD | قبول؛ دستور کاربر برای Plan مشخصات فعال | قبول؛ مدل، قرارداد و پذیرش مطابق FRها؛ بدون Tasks و کد |
| اصل دوم: معماری | قبول؛ ساختار موجود ثابت | قبول؛ Domain مستقل، Application فقط Domain، DataAccess به Core، Api به Application/DataAccess برای Composition |
| اصل سوم: امنیت | قبول؛ مجوز و تأیید شرط موارد کاربرد | قبول؛ هویت در HTTP، مجوز در Application، ناورداها در Domain، ذخیرهٔ اتمی در DataAccess |
| اصل چهارم: آزمون‌پذیری | قبول؛ قواعد بدون سرویس بیرونی | قبول؛ Unit و آزمون HTTP/provider و رقابت در quickstart |
| اصل پنجم: سادگی | قبول؛ API بدون AI | قبول؛ چهار پروژهٔ لازم و یک Tests؛ بدون AI/Qdrant/Modules/Framework اضافی |
| اصل ششم: حاکمیت | قبول؛ Constitution ثابت | قبول؛ تصمیم‌ها پیشنهاد Plan و کاندید Baseline نیازمند بازبینی انسانی |

نقض یا استثنای نیازمند توجیه وجود ندارد. Credential اپراتوری این طراحی پیش‌نیاز هویت را فراهم می‌کند؛ وجود IdP قبلی فرض نشده است.

## ساختار پروژه

### اسناد این Feature

```text
specs/001-blog-post-management/
├── spec.md
├── checklists/requirements.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/http-api.md
```

### ساختار هدف کد — اکنون ایجاد نشده

```text
BlogSpecLab.slnx
src/
├── 01.Core/
│   ├── Domain/BlogSpecLab.Core.Domain.csproj
│   └── Application/BlogSpecLab.Core.Application.csproj
├── 02.Infra/
│   └── DataAccess/BlogSpecLab.Infra.DataAccess.csproj
└── 03.Endpoints/
    └── Api/BlogSpecLab.Endpoints.Api.csproj
tests/
└── BlogSpecLab.Tests/
    ├── BlogSpecLab.Tests.csproj
    ├── Domain/
    ├── Application/
    └── Integration/
```

ساختار از Constitution گرفته می‌شود. Api فقط Binding، Authentication، تبدیل نتیجه به HTTP و Composition دارد. Domain شامل Post و انتقال‌ها؛ Application شامل Create/List/Get/Edit/Delete/Submit/RequestChanges/Approve/Publish و PublicRead است. قراردادهای حداقلی ذخیرهٔ پست و خواندن هویت/مجوز در Application قرار می‌گیرند؛ EF و transaction در DataAccess پنهان‌اند. DbContext یا IQueryable از Core بیرون داده نمی‌شود. Handler از قرارداد Credential verifier استفاده می‌کند؛ hash و خواندن داده در DataAccess است. Tests به پروژه‌های لازم برای آزمون رفرنس دارد. AI و Qdrant لازم نیستند.

## خروجی پژوهش و طراحی

Phase 0: [research.md](research.md)، تصمیم/دلیل/جایگزین هر انتخاب جدید. C#، ASP.NET Core، .NET 10 و معماری موجود انتخاب مجدد نشده‌اند.

Phase 1: [data-model.md](data-model.md)، ناورداها و انتقال‌ها؛ [contracts/http-api.md](contracts/http-api.md)، مسیرها و DTOها و خطاها؛ [quickstart.md](quickstart.md)، راهنمای استفاده و پذیرش آینده.

| نیازمندی | محل طراحی و اعتبارسنجی |
| --- | --- |
| FR-001، FR-013 | UserAccess/Credential، کنترل Application، جعل و لغو دسترسی |
| FR-002 تا FR-005 | Post و مسیرهای Author؛ ایجاد، اصلاح، حذف، ارسال |
| FR-006، FR-007 | صف Editor و ReviewDecision؛ برگشت و تأیید |
| FR-008، FR-009 | ContentRevision و Snapshot، PublicationInfo؛ تغییر محتوا و لغو مجوز |
| FR-010، FR-012 | Version و Commit اتمی و ProblemDetails؛ رقابت و تکرار |
| FR-011 | PublicPostView و فیلتر Published؛ شناسهٔ خصوصی |
| FR-014، FR-015 | ممنوعیت خودتأییدی و ثبات Published؛ سناریوی دو نقش |
| FR-016 | قرارداد و quickstart؛ پذیرش SC-004 و SC-005 |

## پیگیری پیچیدگی

موردی ندارد؛ استثنایی از Constitution لازم نشده است. تثبیت Baseline و رفتن به Tasks یا Implementation خارج از این فرمان است.
