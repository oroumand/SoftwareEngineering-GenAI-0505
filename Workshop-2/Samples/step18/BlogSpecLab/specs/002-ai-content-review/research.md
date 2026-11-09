# پژوهش فنی بررسی کمکی محتوا

**قابلیت**: `002-ai-content-review` | **تاریخ بررسی منابع**: 2026-10-12

مرجع نیازمندی [spec.md](spec.md)، اصول [Constitution](../../.specify/memory/constitution.md)، تصمیم‌های تثبیت‌شده [Technical Baseline](../../docs/architecture/technical-baseline.md) و [قرارداد قابلیت اول](../001-blog-post-management/contracts/http-api.md) است. C#، .NET 10، ASP.NET Core، جهت وابستگی، EF Core، Permission جاری، ProblemDetails، xUnit و WebApplicationFactory ورودی ثابت‌اند و دوباره انتخاب نشده‌اند. SQLite، opaque credential و Minimal API موجود برای اجرای محلی حفظ می‌شوند؛ این حفظ به معنی تثبیت سراسری آن‌ها نیست.

## R1 — Provider، مدل، SDK و API

- **تصمیم**: برای همین قابلیت OpenAI API، snapshot ثابت `gpt-4.1-mini-2025-04-14`، package رسمی `OpenAI` نسخهٔ `2.14.0` و `ChatClient.CompleteChatAsync` غیر streaming انتخاب شوند. فقط Infra.AI به package وابسته باشد. `temperature=0`، یک completion و `max_completion_tokens=8192` نقطهٔ آغاز ارزیابی‌اند؛ ثبات snapshot و temperature تضمین deterministic بودن متن نیستند. بدون fallback خودکار به مدل دیگر.
- **دلیل انتخاب**: نیاز فعلی بررسی متن و گزارش ساختاریافته بدون reasoning چندمرحله‌ای است. مدل منتخب Structured Outputs و snapshot دارد؛ Chat برای یک پاسخ بدون state کافی است. این انتخاب اولیه است، نه ادعای عبور از آستانه‌های کیفیت فارسی. SDK رسمی async و Schema را پوشش می‌دهد؛ abstraction چند-provider لازم نیست. نسخه هنگام Implementation دقیقاً pin و compatibility با net10.0 با restore/build اثبات شود؛ اکنون package نصب نمی‌شود. [مدل رسمی](https://developers.openai.com/api/docs/models/gpt-4.1-mini)، [SDK رسمی](https://developers.openai.com/api/docs/libraries)، [انتشار 2.14.0](https://github.com/openai/openai-dotnet/releases/tag/OpenAI_2.14.0).
- **گزینه‌های جایگزین**: Responses API امکانات state و ابزار دارد که اکنون لازم نیست. مدل کامل یا reasoning پس از شکست کیفیت قابل بازبینی است، ولی بدون شواهد پیچیدگی بیشتر توجیه ندارد. Azure OpenAI provisioning تازه و مدل محلی میزبانی و شواهد کیفیت فارسی می‌خواهند. SDK واسط، Semantic Kernel و Microsoft.Extensions.AI برای یک provider و قرارداد اختصاصی ضروری نیستند. HTTP دستی نگهداری پروتکل را بیشتر می‌کند.
- **حد پذیرش**: دسترسی مدل برای حساب و محیط واقعی با smoke test آینده بررسی شود؛ صفحهٔ عمومی دسترسی حساب را اثبات نمی‌کند. تغییر مدل/snapshot/prompt/Schema نیازمند اجرای دوبارهٔ SC-005 است. Provider یا Model سراسری انتخاب نشده است.

## R2 — Structured Output و اعمال Schema

- **تصمیم**: Schema نسخه‌دار `content_review_v1` در [model-output.schema.json](contracts/model-output.schema.json) با root object، enumها، آرایه‌ها، تمام propertyها در `required` و `additionalProperties=false` در همهٔ objectها. `suggestion` الزامی ولی nullable است. Adapter همین Schema را با `ChatResponseFormat.CreateJsonSchemaFormat(..., jsonSchemaIsStrict: true)` در `response_format` ارسال کند. JSON mode یا صرف دستور «JSON بده» استفاده نشود.
- **دلیل انتخاب**: قرارداد ماشین‌خوان FR-005 تا FR-010 را روشن و شکل خروجی را در Provider محدود می‌کند. قیود رابطه‌ای وضعیت/آرایه و شواهد با validator مستقل اعمال می‌شوند؛ Schema ساده و بدون شرط‌های پیچیدهٔ ناسازگار با subset Provider است. Schema wire با transport test و smoke test واقعی بررسی شود. [قواعد Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs).
- **گزینه‌های جایگزین**: متن آزاد و JSON mode شکل Schema را تضمین نمی‌کنند؛ function calling برای گزارش فاقد عملیات، اختیار غیرضروری می‌سازد. union پیچیدهٔ root و چند Schema نگهداری را سخت می‌کنند. artifact قرارداد منبع واحد است؛ برابری نسخهٔ بسته‌بندی‌شده و DTO با آن آزموده شود.

## R3 — موفقیت، needs_input، refusal و failure

- **تصمیم**: گزارش معتبر فقط `completed` یا `needs_input` است. needs_input با حداقل یک missing-information و امکان issues هم‌زمان، HTTP 200 است. refusal از فیلد اختصاصی Provider تشخیص داده و به `ai_review_refused` تبدیل شود؛ refusal داخل Schema گزارش نیست. خروجی قطع‌شده، JSON ناقص، finish reason غیر stop، tool call غیرمنتظره یا نتیجهٔ ناسازگار معتبر نیست. خطای شبکه، quota/rate limit و Provider failure به `ai_review_unavailable`؛ پایان deadline به `ai_review_timeout`؛ خروجی ناقص یا نامعتبر به `ai_review_invalid_output` نگاشت شوند. HTTP در [قرارداد](contracts/http-api.md) مشخص است.
- **دلیل انتخاب**: فهرست خالی به‌تنهایی موفقیت نیست. Adapter ابتدا cancellation/deadline و transport را کنترل کند؛ refusal صریح پیش از deserialize تشخیص داده شود. truncation یا content filtering بدون refusal صریح، invalid_output است. [refusal و پاسخ ناقص](https://developers.openai.com/api/docs/guides/structured-outputs).
- **گزینه‌های جایگزین**: needs_input به‌عنوان خطای فنی، refusal به لیست خالی یا پیام SDK در گزارش معتبر، نیازمندی را مخدوش می‌کند. repair call خودکار خطای اولیه را پنهان و latency را بیشتر می‌کند؛ انجام نمی‌شود.

## R4 — مرز Application و Infra.AI

- **تصمیم**: Application هماهنگی، Permission/مالکیت/وضعیت، snapshot، validator مستقل، recheck و freshness را انجام دهد. `IContentReviewGateway` فقط عنوان/متن immutable و CancellationToken دریافت و candidate/refusal/failure نوع‌دار برگرداند. Infra.AI مسئول SDK، prompt ثابت، Schema wire، transport، outcome پروتکل و DTO مستقل است؛ مجوز، DB، mutation و HTTP در آن نیست. DTO/outcome در Application؛ type و exception SDK از مرز عبور نکنند.
- **دلیل انتخاب**: با جهت وابستگی موجود سازگار و بدون سرویس بیرونی آزمون‌پذیر است. AI نیاز تأییدشده دارد؛ Qdrant و Agent ندارند. [قرارداد مرز](contracts/application-ai.md).
- **گزینه‌های جایگزین**: SDK در Application یا Endpoint، business validation در adapter و AI وابسته به DataAccess خلاف تفکیک‌اند. gateway عمومی chat/multi-provider دامنهٔ اختیار بیشتری از نیاز دارد؛ قرارداد اختصاصی کوچک کافی است.

## R5 — Data Minimization و دادهٔ بیرونی

- **تصمیم**: از دادهٔ پست/کاربر فقط title/body دقیق snapshot ارسال شوند. prompt ثابت، Schema، model و پارامترهای فنی لازم نیز ارسال می‌شوند. postId، revision، owner/userId، token کاربر، نقش، Permission، دلیل برگشت، reviews، publication، تاریخچه و correlation داخلی ارسال نشوند. هیچ فایل، ابزار، conversation یا metadata کاربری اضافه نشود؛ `store=false` صریح باشد. key فقط در secret storage/محیط میزبان و احراز هویت Provider باشد. body درخواست/پاسخ، متن، شواهد، refusal خام و prompt دارای محتوا در log/trace/exception ثبت نشوند؛ telemetry فقط زمان، outcome و نسخه‌های غیرحساس باشد.
- **دلیل انتخاب**: Clarifications و FR-017 ارسال عنوان/متن با درخواست صریح را مجاز می‌کنند. راهنما این ارسال را توضیح می‌دهد. store=false به معنی zero retention نیست؛ سیاست نگهداری بیرونی مستقل است. [کنترل دادهٔ OpenAI](https://developers.openai.com/api/docs/guides/your-data).
- **گزینه‌های جایگزین**: ارسال aggregate کامل یا credential/identity غیرضروری است. redaction خودکار متن ممکن است شواهد و معنا را تغییر دهد و در Spec تعریف نشده؛ اضافه نمی‌شود. رضایت جداگانه برخلاف clarification لازم نیست؛ cache/تاریخچه نیز خارج دامنه است.

## R6 — Timeout، Cancellation و retry

- **تصمیم**: deadline monotonic از اولین middleware دریافت درخواست آغاز شود؛ auth، DB، Provider، parsing، validation و recheck همگی داخل بودجه‌اند. کار عادی تا ثانیهٔ ۵۹ تمام یا متوقف شود تا آماده‌سازی پاسخ timeout تا ثانیهٔ ۶۰ ممکن باشد. بودجهٔ Provider حداکثر ۵۰ ثانیه و کمتر از زمان باقی‌مانده تا پایان بودجهٔ کار، با دو ثانیه ذخیره برای validation/recheck باشد. کمبود بودجه از آغاز مرحله نیز timeout است؛ سقف ۶۰ اجازهٔ پاسخ زودتر می‌دهد.
- **دلیل انتخاب**: timeout تنها SDK زمان قبل/بعد تماس را پوشش نمی‌دهد. token لینک‌شدهٔ RequestAborted و deadline به همهٔ I/O برسد. retry پیش‌فرض SDK با policy صفر retry غیرفعال و network timeout در حد بودجه تنظیم شود. انتظار عملیات با deadline قطع شود، حتی اگر dependency به cancellation پاسخ ندهد؛ task دیررس observe و منابع آن پس از پایان dispose شوند، DbContext هم‌زمان استفاده نشود. elapsed قبل از تحویل مانع موفقیت دیررس باشد. [retry و async در SDK منتخب](https://github.com/openai/openai-dotnet/blob/OpenAI_2.14.0/README.md).
- **گزینه‌های جایگزین**: timeout شصت‌ثانیه‌ای فقط AI، retry نمایی پیش‌فرض یا شروع ساعت پس از auth سقف کل مسیر را نقض می‌کنند. background job برخلاف پاسخ در همان درخواست است. retry مدل/Provider انجام نشود؛ نویسنده درخواست تازه می‌دهد.
- **تفکیک cancellation**: قطع Client توقف و دور انداختن نتیجه است؛ HTTP برای اتصال قطع‌شده تضمین نمی‌شود و cancellation به refusal یا needs_input تبدیل نشود. deadline میزبان برای Client متصل 504/ai_review_timeout است. clock کنترل‌شده و آزمون elapsed واقعی، کندی auth/storage و adapter غیرهمکار را پوشش دهند؛ زمان شبکهٔ Client طبق SC-008 خارج اندازه‌گیری است.
- **مالکیت منابع دیررس**: برای I/O که قطع انتظار آن ممکن است پیش از پایان واقعی task رخ دهد، Composition باید scope/resources مستقلِ متعلق به همان عملیات فراهم کند؛ پایان scope معمول HTTP نباید DbContext فعال آن را زود dispose کند. task فقط دادهٔ immutable و وابستگی‌های همین scope را نگه دارد، به HttpContext/Response پس از timeout دسترسی نداشته باشد و cleanup و مشاهدهٔ exception آن پس از پایان task انجام شود. coordinator پس از timeout همان DbContext را دوباره استفاده نکند؛ این کار آرشیو یا job قابل polling ایجاد نمی‌کند.

## R7 — اعتبارسنجی مستقل خروجی

- **تصمیم**: Infra.AI parsing سخت‌گیرانهٔ شکل Schema با System.Text.Json انجام دهد: یک object کامل، تمام propertyها، بدون property ناشناخته/کلید تکراری، enum دقیق و type درست. coercion، code-fence extraction و اصلاح حدسی ممنوع؛ package عمومی JSON Schema برای قرارداد کوچک لازم نیست. Application دوباره summary، explanation و evidence غیر whitespace، field فقط title/body، چهار category مشخص و سازگاری وضعیت/آرایه‌ها را کنترل کند. suggestion فقط null یا متن غیرخالی؛ هر evidence substring دقیق ordinal از field متن snapshot باشد. missing-information نیز شاهد موجود، reason و requestedInformation غیرخالی داشته باشد.
- **دلیل انتخاب**: Schema حضور شاهد یا نبود hallucination معنایی را اثبات نمی‌کند. مقایسهٔ دقیق اختلاف Unicode/نیم‌فاصله یا شاهد ساخته‌شده را پنهان نمی‌کند؛ prompt شاهد verbatim می‌خواهد. خروجی حداکثر ۲۵۶ KiB UTF-8 و depth حداکثر ۱۶ داشته باشد؛ عبور از حد invalid_output است و truncate نشود. این حد دفاعی خروجی است، نه سقف تجاری طول پست.
- **گزینه‌های جایگزین**: اعتماد به strict Provider، deserialize permissive، regex استخراج JSON یا fuzzy match شاهد ناموجود را معتبر جلوه می‌دهند. صحت علمی از validator اثبات نمی‌شود؛ SC-002 و SC-005 جدا سنجیده شوند. ورودی بدون truncation ارسال شود؛ رد Provider بابت context limit failure قابل تشخیص است، نه گزارش جزئی معرفی‌شده به‌عنوان کامل.

## R8 — snapshot، recheck و concurrency

- **تصمیم**: `IPostContentReviewReader` projection immutable تازهٔ Id/OwnerId/Title/Body/Status/ContentRevision/IsDeleted بدهد؛ DataAccess با AsNoTracking و بدون Include تاریخچه بخواند. IUserAccessReader موجود no-tracking دوباره استفاده شود. recheck از entity tracked `PostStore.FindAsync` انجام نشود. revision همراه برابری دقیق عنوان/متن برای freshness؛ Version که با وضعیت هم تغییر می‌کند ملاک تازگی محتوا نیست. گزارش ذخیره و write session باز نشود.
- **دلیل انتخاب**: PostStore موجود tracked است؛ query تکراری ممکن است همان state حافظه را بدهد. حذف نرم، لغو مجوز و اصلاح باید از منبع جاری دیده شوند. پیش از تحویل Permission/فعال‌بودن/مالکیت دوباره کنترل شوند؛ حذف 404 و لغو Permission 403، بدون افشای گزارش. تغییر صرف وضعیت در حین اجرا snapshot را invalid نمی‌کند؛ eligibility FR-002 مربوط به شروع است و وضعیت هرگز عقب برده نشود.
- **گزینه‌های جایگزین**: lock/تراکنش طول تماس AI مانع ویرایش و لغو دسترسی می‌شود. ذخیرهٔ گزارش و fingerprint قابلیت اضافی است. If-Match در endpoint read-only لازم نیست؛ مبنا متن ذخیره‌شدهٔ زمان شروع است. قرارداد فرمان‌های تغییردهندهٔ قابلیت اول تغییر نمی‌کند.
- **محدودهٔ تضمین**: isCurrentContent فقط در checkedAt معتبر است؛ پس از دریافت Client با revision جزئیات تازه نتیجه را stale کند. recheck نقطهٔ تحویل منطقی است، نه تضمین نبود تغییر در ارسال شبکه. Integration با scope مستقل داده را قبل از آزاد کردن پاسخ gateway تغییر دهد.

## R9 — رفتار deterministic و کیفیت غیرقطعی

- **تصمیم**: CI معمولی با fake gateway و clock کنترل‌شده در Application، transport ساختگی adapter و WebApplicationFactory/SQLite واقعی اجرا شود. access، recheck، snapshot، timeout و validator deterministic آزموده شوند؛ عبارت‌بندی زندهٔ مدل assertion نباشد. suite واقعی جدا با secret محیطی و اجرای صریح برای smoke Schema، کیفیت و latency فراهم شود؛ skip جای پذیرش موفق را نگیرد.
- **دلیل انتخاب**: snapshot و temperature صفر nondeterminism را حذف نمی‌کنند. SC-005 مجموعهٔ انسانی ثابت پیش از اجرا می‌خواهد: حداقل ۲۰ متن دارای ایراد و ۱۰ متن سالم؛ gold شامل issue-id/category/field/شاهد و توضیح معنایی و موارد کمبود اطلاعات باشد. matching انسانی، هر gold issue حداکثر یک‌بار شمارش شود. recall = ایراد مرجع کشف‌شده / کل ایراد مرجع؛ clean rate = متن سالم بدون مشکل بی‌پشتوانه / کل متن سالم؛ هر دو حداقل ۹۰٪. error/refusal/timeout/invalid output نمونهٔ ارزیابی به‌عنوان پاس حذف نشوند. [راهنمای ارزیابی](https://developers.openai.com/api/docs/guides/evaluation-best-practices).
- **گزینه‌های جایگزین**: assertion دقیق متن مدل، mock فقط در HTTP و JSON معتبر شاهد کیفیت نیستند. LLM-as-judge فقط کمک‌کننده است؛ پذیرش انسانی مرجع نهایی. انتخاب بهترین run یا حذف نمونهٔ شکست‌خورده سوگیری می‌سازد.
- **پروتکل پذیرش**: سه اجرای از پیش تعیین‌شده با dataset/model/prompt/Schema ثابت؛ هر اجرا جدا SC-005 و SC-002 را پاس کند، میانگین شکست را پنهان نکند. SC-002 در همهٔ نمونه‌های فاقد منبع/ارجاع مبهم ۱۰۰٪ needs_input و عدم ساخت منبع؛ SC-006 با حداقل پنج نویسنده، حداقل چهار نفر از پنج نفر تمام وضعیت‌ها و اختیار مستقل اصلاح/ارسال را درست بفهمند. rubric، نسخه‌ها، failureها، latency و رأی انسانی در شواهد آینده ثبت شوند. این روش اثبات Spec است؛ ارزیابی اکنون انجام نشده است.

## R10 — پیکربندی، راهنما و اجرای بدون AI

- **تصمیم**: `AI:ContentReview:Enabled` پیش‌فرض false و مدل منتخب قابل تنظیم باشد؛ API key خارج مخزن. در حالت خاموش درخواست مجاز 503/ai_review_unavailable و قابلیت اول بدون key قابل اجرا باشد. تنظیم ناقص مسیر AI را unavailable کند، log فقط نام تنظیم ناقص بدون مقدار باشد. Startup تماس شبکه‌ای، migration یا provision ضمنی نکند. Adapter singleton thread-safe؛ coordinator/reader scoped مطابق DB موجود. راهنمای نویسنده در quickstart باشد.
- **دلیل انتخاب**: قابلیت اولیه مستقل از مدل می‌ماند. راهنما ارسال بیرونی، وضعیت‌ها، stale و کمکی بودن گزارش را توضیح می‌دهد. helper موجود DocumentPost برای PostView و metadata مربوط به mutation/ETag طراحی شده است؛ این مسیر metadata اختصاصی گزارش، بدون If-Match و ETag نیاز دارد.
- **گزینه‌های جایگزین**: key اجباری برای کل host، خاموش‌شدن CRUD در اختلال AI، صفحهٔ وب یا شرط گزارش برای submit نیاز این گام نیستند.

## کاندیدهای Technical Baseline — تصویب نشده‌اند

| پیشنهاد سراسری | دلیل و اثر تثبیت احتمالی |
| --- | --- |
| قرارداد structured نسخه‌دار با enforcement در Provider و validator مستقل | الگوی Integrationهای AI بعدی؛ اثر بر adapter، قرارداد و آزمون؛ صحت معنایی همچنان ارزیابی مستقل می‌خواهد |
| تفکیک refusal از گزارش معتبر و failure فنی در مرز AI | مانع موفقیت ظاهری شکست می‌شود؛ ممکن است outcomeها و mapping قابلیت‌های آینده را هماهنگ کند |
| حداقل‌سازی payload AI و منع log محتوا/خروجی | اثر بر logging/tracing و Integrationهای آینده؛ مجوز ارسال هر داده همچنان در Spec همان قابلیت تعیین شود |
| CI deterministic جدا از ارزیابی غیرقطعی نسخه‌دار | روش پذیرش AI آینده؛ corpus و آستانه‌ها متعلق به هر قابلیت باقی بمانند |

این‌ها فقط کاندید بازبینی انسانی‌اند؛ Technical Baseline تغییر نکرده است. Provider، مدل، SDK، Chat API، سقف ۶۰ ثانیه، دسته‌ها، عدم آرشیو و آستانه‌های کیفیت به پروژهٔ کامل تعمیم داده نمی‌شوند. R1 تا R10 مجهولات فنی طراحی را حل کرده‌اند؛ هیچ NEEDS CLARIFICATION حل‌نشده‌ای باقی نیست.
