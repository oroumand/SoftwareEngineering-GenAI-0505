# برنامهٔ فنی: بررسی هوشمند و کمکی محتوای پست

**Feature / Branch خروجی Spec Kit**: `002-ai-content-review` | **تاریخ**: 2026-10-12 | **مشخصات**: [spec.md](spec.md)

مخزن فعلی Git ندارد؛ نام بالا شناسهٔ فعال Spec Kit است، نه شاخهٔ Git ایجادشده. ورودی این مرحله Specification تأییدشده طبق دستور کاربر است؛ عبارت وضعیت قدیمی داخل Spec تغییر نمی‌کند. این اسناد پیشنهاد طراحی برای بازبینی‌اند و قابلیت هنوز پیاده‌سازی نشده است.

## خلاصه

نویسنده با درخواست صریح، عنوان و متن ذخیره‌شدهٔ پست خودش در `Draft` یا `ChangesRequested` را بررسی می‌کند. Application مجوز فعلی، مالکیت و پیش‌شرط‌ها را کنترل می‌کند، snapshot می‌گیرد و قرارداد محدود سرویس بررسی را فراخوانی می‌کند. `Infra.AI` یک فراخوانی بدون ابزار به OpenAI با Structured Output strict انجام می‌دهد. گزارش فقط پس از اعتبارسنجی مستقل و بازخوانی تازهٔ دسترسی و پست تحویل می‌شود؛ محتوا و گردش کار تغییر نمی‌کنند. نتیجهٔ قدیمی برچسب قابل پردازش دارد و پایان مهلت حداکثر ۶۰ ثانیه است.

## زمینهٔ فنی

| مورد | طراحی |
| --- | --- |
| زبان / نسخه | C#، .NET 10 و ASP.NET Core موجود؛ بدون انتخاب دوباره |
| وابستگی اصلی | وابستگی‌های موجود حفظ می‌شوند؛ در اجرای آینده فقط Infra.AI با package رسمی OpenAI نسخهٔ 2.14.0 اضافه می‌شود |
| Provider / Model / API | انتخاب همین Feature: OpenAI API؛ gpt-6-luna برای ارزیابی اولیه؛ Responses API غیر streaming، یک درخواست با text.format از نوع json_schema، strict=true و store=false؛ بدون ابزار |
| ذخیره‌سازی | EF Core موجود و SQLite محلی قابلیت اول؛ گزارش فقط در حافظه و پاسخ، بدون جدول، migration یا آرشیو |
| آزمون | xUnit و WebApplicationFactory موجود، SQLite واقعی؛ fake Application و transport کنترل‌شدهٔ SDK؛ ارزیابی واقعی جدا و الزامی برای پذیرش |
| بستر هدف | HTTP API آموزشی موجود؛ مسیر AI نیازمند دسترسی مجاز به حساب و شبکهٔ Provider؛ مقصد production تعیین نشده |
| نوع پروژه | سامانهٔ یکپارچه با پروژه‌های موجود و یک پروژهٔ لازم Infra.AI؛ بدون صفحهٔ وب |
| هدف کارایی | FR-018 / SC-008: از دریافت درخواست تا آماده‌سازی پاسخ حداکثر ۶۰ ثانیه؛ throughput جدید تعریف نمی‌شود |
| محدودیت‌ها | از دادهٔ کاربر فقط عنوان و متن ارسال شود؛ خروجی غیرقابل اعتماد؛ بدون اصلاح، تأیید، انتشار، Tool Calling، MCP، RAG، Agent یا جست‌وجو |
| دامنه | FR-001 تا FR-018؛ فارسی؛ حداقل ۲۰ متن دارای ایراد، ۱۰ متن سالم و پنج نویسنده برای پذیرش |

### مجهولات فنی مرحلهٔ صفر

قرارداد Schema، تفکیک outcomeها، Provider/Model/SDK، حداقل‌سازی داده، cancellation، اعتبارسنجی، خواندن تازه و روش ارزیابی کیفیت در [research.md](research.md) ثبت شده‌اند. `gpt-6-luna` انتخاب اولیه است، نه تضمین عبور از Quality Gate؛ شکست آستانه‌های Specification مستلزم بازبینی مدل براساس Eval است، بدون fallback خودکار. Reasoning تنظیم Feature-level قابل ارزیابی است و مقدار آن تا benchmark/Eval قطعی نیست. snapshot نسخه‌دار مستقلی در منبع رسمی بررسی‌شده یافت نشد؛ نام حدسی ساخته نمی‌شود. مدل واقعی پاسخ Provider، تاریخ Eval، نسخهٔ Prompt/Schema و تنظیمات مؤثر باید در شواهد آینده ثبت شوند. دسترسی حساب، کیفیت، latency و کفایت بودجهٔ خروجی در این مرحله اندازه‌گیری نشده‌اند.

## کنترل Constitution

مرجع [Constitution نسخهٔ 1.0.0](../../.specify/memory/constitution.md) و [Technical Baseline](../../docs/architecture/technical-baseline.md) است. معماری، EF Core، مجوز جاری، ProblemDetails و ابزار آزمون موجود بازتعریف نشده‌اند.

| دروازه | پیش از Phase 0 | پس از Phase 1 و شاهد |
| --- | --- | --- |
| اصل اول، SDD | قبول؛ دستور Plan بر Specification تأییدشده | قبول؛ فقط Artifactهای Plan و نگاشت FRها؛ بدون Tasks و Implementation |
| اصل دوم، وابستگی‌ها | قبول؛ ساختار موجود و جایگاه AI در Constitution | قبول؛ Application فقط Domain؛ AI به Application؛ DataAccess به Core؛ Api به Application و Infra برای Composition؛ بدون وابستگی AI به DataAccess |
| اصل سوم، اعتماد و امنیت | قبول؛ ارسال بیرونی در Clarifications مجاز است | قبول؛ کنترل دسترسی در Application، validation مستقل، بدون ابزار و write session، حداقل‌سازی داده و عدم log محتوا |
| اصل چهارم، کیفیت | قبول؛ قواعد نرم‌افزار از معنای گزارش جدا هستند | قبول؛ آزمون deterministic جدا از ارزیابی واقعی و آستانه‌های SC-005 و SC-006 |
| اصل پنجم، سادگی | قبول؛ نیاز واقعی AI اکنون تعریف شده است | قبول؛ فقط Infra.AI و قرارداد کوچک؛ قابلیت اول بدون تنظیم AI اجرا می‌شود؛ بدون Qdrant یا orchestration framework |
| اصل ششم، حاکمیت | قبول؛ مراجع ثابت | قبول؛ Constitution ثابت؛ فقط چهار اصل AI Integration با تأیید صریح بازبینی انسانی در Baseline ثبت شدند؛ Provider/API/Model/SDK در سطح Feature باقی ماندند |

نقض یا استثنا وجود ندارد. تولید Plan به‌معنای تصویب سراسری انتخاب‌ها یا پذیرش کیفیت مدل نیست.

## ساختار پروژه

### اسناد این قابلیت

```text
specs/002-ai-content-review/
├── spec.md                         # ورودی موجود، بدون تغییر
├── checklists/requirements.md      # موجود، بدون تغییر
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
└── contracts/
    ├── http-api.md
    ├── application-ai.md
    └── model-output.schema.json
```

### ساختار هدف کد — اکنون ایجاد نمی‌شود

```text
src/
├── 01.Core/
│   ├── Domain/Posts/                         # قواعد موجود حفظ می‌شوند
│   └── Application/ContentReview/           # هماهنگی، قراردادها و validator
├── 02.Infra/
│   ├── DataAccess/Posts/                    # projection تازهٔ read-only
│   └── AI/
│       ├── BlogSpecLab.Infra.AI.csproj
│       └── ContentReview/                   # adapter، prompt، Schema و options
└── 03.Endpoints/Api/
    ├── Endpoints/                           # مسیر content-review
    ├── Contracts/                           # envelope HTTP
    ├── Http/                                # خطاها و OpenAPI
    └── Configuration/                       # Composition و تنظیم اختیاری AI
tests/BlogSpecLab.Tests/
├── Application/
├── Integration/
├── AI/                                      # transport fake و آزمون واقعی opt-in
└── Fixtures/                                # مجموعهٔ مصنوعی ثابت پذیرش
```

Domain به SDK یا گزارش AI وابسته نمی‌شود. Application مالک `IPostContentReviewReader` و `IContentReviewGateway` است؛ reader نیاز snapshot و بازخوانی تازه را تأمین می‌کند. پست tracked موجود برای recheck استفاده نمی‌شود. Infra.AI هیچ Repository یا اختیار نوشتن ندارد. Api هویت، deadline ورودی، binding، mapping HTTP و DI را انجام می‌دهد. Schema قرارداد با نام `content_review_v1` هنگام پیاده‌سازی بدون نسخهٔ دستی متفاوت در adapter بسته‌بندی می‌شود.

## جریان طراحی

1. از اولین middleware ورودی deadline مشترک ثبت شود؛ authentication و تمام I/O نیز محدود به آن باشند.
2. Application مجوز فعلی نویسندگی، وجود/مالکیت پست، وضعیت مجاز و متن غیرخالی را کنترل و snapshot immutable از revision و متن دقیق ذخیره‌شده بسازد.
3. فقط title/body به gateway داده شوند؛ adapter دستور ثابت و Schema را به درخواست Provider اضافه کند. اطلاعات کاربر و snapshot داخلی بیرون نروند.
4. Adapter outcome پروتکل را تفکیک و candidate گزارش را deserialize کند؛ Application قواعد گزارش و شواهد را روی snapshot کنترل کند.
5. پیش از تحویل، Permission و projection پست تازه خوانده شوند؛ حذف یا لغو دسترسی گزارش را متوقف کند. تغییر محتوا نتیجه را stale می‌کند؛ تغییر صرف وضعیت، snapshot را تغییر نمی‌دهد.
6. گزارش معتبر در envelope سروری با revision و زمان کنترل freshness بازگردد؛ خطاها ProblemDetails باشند. پس از deadline یا قطع درخواست، پاسخ دیررس دور انداخته شود. هیچ تراکنش نوشتن یا lock در طول تماس AI باز نماند.

## خروجی پژوهش و طراحی

Phase 0: [research.md](research.md)، تصمیم، دلیل و جایگزین هر انتخاب جدید.
Phase 1: [data-model.md](data-model.md)، [HTTP](contracts/http-api.md)، [Application/AI](contracts/application-ai.md)، [Schema](contracts/model-output.schema.json) و [quickstart.md](quickstart.md).

| نیازمندی | محل طراحی / بررسی آینده |
| --- | --- |
| FR-001 تا FR-003 | snapshot و recheck در data-model؛ مجوز، مالکیت و رد ورودی در HTTP و quickstart |
| FR-004 تا FR-010 | Schema و validator؛ وضعیت‌ها و شواهد؛ R2، R3، R7 و ارزیابی کیفیت |
| FR-011 | جدول outcome/ProblemDetails؛ refusal، invalid_output، unavailable و timeout جدا |
| FR-012، FR-013 | gateway فاقد write؛ مقایسهٔ کامل گردش کار در تمام مسیرها و submit مستقل |
| FR-014، FR-015 | ContentRevision و freshness هنگام تحویل و پس از دریافت؛ درخواست تازه مستقل |
| FR-016، FR-017 | راهنمای نویسنده در quickstart و بررسی payload بیرونی |
| FR-018 | deadline کل مسیر و cancellation؛ پاسخ دیررس و کندی auth/storage/provider |
| SC-001 تا SC-004، SC-007، SC-008 | آزمون deterministic و Integration طبق quickstart |
| SC-005، SC-006 | مجموعه و rubric انسانی ثابت، مدل واقعی و پنج نویسنده؛ شکست مانع پذیرش |

## پیگیری پیچیدگی

موردی ندارد؛ پروژه و Interfaceهای جدید نیاز مستقیم همین Feature را پوشش می‌دهند. Extension hook پیش و پس از Plan ثبت نشده است (`.specify/extensions.yml` وجود ندارد). بازبینی حاضر فقط طراحی و چهار اصل سراسری تأییدشدهٔ Baseline را اصلاح می‌کند؛ tasks.md، کد، package یا migration تولید نمی‌شوند.
