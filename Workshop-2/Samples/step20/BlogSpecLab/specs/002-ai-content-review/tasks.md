# وظایف: بررسی هوشمند و کمکی محتوای پست

**ورودی**: اسناد `specs/002-ai-content-review/`: [plan.md](plan.md)، [spec.md](spec.md)، [research.md](research.md)، [data-model.md](data-model.md) و [contracts/](contracts/).

**پیش‌نیازها**: Constitution، Technical Baseline و پیاده‌سازی موجود قابلیت اول. شناسهٔ فعال Spec Kit برابر `002-ai-content-review` است؛ مخزن Git ندارد. این سند فقط برنامهٔ اجراست و به‌معنای شروع Implement یا پذیرش قابلیت نیست. وضعیت متنی Spec و Plan در این مرحله تغییر نمی‌کند.

**آزمون‌ها**: طبق سناریوهای پذیرش Specification و اصل چهارم Constitution، آزمون‌های خودکار لازم‌اند؛ آزمون‌های قطعی بدون شبکه از ارزیابی واقعی مدل جدا هستند. آزمون هر بخش پیش از پیاده‌سازی همان بخش نوشته و شکست مورد انتظار آن بررسی شود؛ شکست build ناشی از نبود type به‌تنهایی شاهد رفتار نیست.

**قالب**: `- [ ] Tnnn [P?] [USn?] شرح همراه مسیر`. `[P]` فقط برای فایل‌های جدا و بدون وابستگی باز در گروه اجرای ذکرشده است. مسیرهای پیشنهادی فایل‌های جدید دقیق‌اند؛ از فایل‌های موجود و الگوهای همان پروژه استفاده شود.

## فاز ۱: آماده‌سازی

**هدف**: افزودن فقط پروژهٔ Infra.AI موردنیاز، بدون Qdrant، صفحهٔ وب، migration یا framework جدید.

- [ ] T001 پروژهٔ .NET 10 در `src/02.Infra/AI/BlogSpecLab.Infra.AI.csproj` با package رسمی `OpenAI` نسخهٔ `2.14.0` و reference فقط به Application ایجاد و در `BlogSpecLab.slnx` ثبت کن؛ reference Api به AI و دسترسی پروژهٔ آزمون به adapter را در `src/03.Endpoints/Api/BlogSpecLab.Endpoints.Api.csproj` و `tests/BlogSpecLab.Tests/BlogSpecLab.Tests.csproj` اضافه کن؛ SDK وارد Core نشود و AI به DataAccess وابسته نباشد.
- [ ] T002 نحوهٔ بسته‌بندی مستقیم artifact `specs/002-ai-content-review/contracts/model-output.schema.json` را در `src/02.Infra/AI/BlogSpecLab.Infra.AI.csproj` تنظیم کن؛ فایل کپی‌شدهٔ دستی با نسخهٔ مستقل نساز و suppression مربوط به `OPENAI001` را فقط در محل مصرف Responses محدود کن.

## فاز ۲: زیرساخت مشترک و پیش‌شرط‌های مسدودکننده

**هدف**: مرزهای مستقل از SDK، خواندن تازه، مهلت و مالکیت منابع. تمام داستان‌ها به پایان این فاز وابسته‌اند؛ کنترل دسترسی بخشی از مسیر اولیهٔ US1 است و تا US3 به تعویق نمی‌افتد.

- [ ] T003 DTOهای immutable و قراردادهای `IPostContentReviewReader`، `IContentReviewGateway`، ورودی فقط Title/Body و outcomeهای Candidate/Refused/Unavailable/Timeout/InvalidOutput را در `src/01.Core/Application/ContentReview/ContentReviewContracts.cs` تعریف کن؛ snapshot با PostId Guid «شناسهٔ موجود و متعلق به درخواست‌کننده»، OwnerId string «فقط کنترل سروری؛ بیرون ارسال نشود»، Title/Body string «متن دقیق ذخیره‌شده؛ غیر whitespace؛ بدون normalize یا truncation»، ContentRevision int «revision موجود Post؛ همراه متن، مبنای این بررسی»، StatusAtStart «فقط Draft یا ChangesRequested» و CapturedAt «زمان گرفتن snapshot؛ توسط سرور» از projection دارای IsDeleted ساخته شود؛ SDK/EF/IQueryable در قرارداد نباشند.
- [ ] T004 مدل report و envelope را در `src/01.Core/Application/ContentReview/ContentReviewReport.cs` تعریف کن؛ status: «completed یا needs_input؛ حساس به حروف»، summary: «غیر whitespace؛ توضیح فارسی کمکی، بدون ادعای تأیید/انتشار»، issues: «الزامی؛ خالی مجاز؛ در needs_input نیز قابل پرشدن»، missingInformation: «completed: خالی؛ needs_input: حداقل یک مورد»؛ issue.category: «writing، ambiguity، internal_inconsistency یا unsupported_claim»، field: «title یا body»، evidence: «نقل‌قول غیرخالی دقیق و ordinal موجود در field snapshot»، explanation: «غیر whitespace؛ توضیح احتمال مشکل»، suggestion: «property همیشه موجود؛ null یعنی پیشنهاد ندارد؛ رشتهٔ غیرخالی صرفاً توصیه»؛ missing.field: «title یا body»، evidence: «بخش مبهم/ادعا، شاهد دقیق موجود در همان field»، reason: «دلیل غیرخالی اینکه چرا ارزیابی ممکن نیست»، requestedInformation: «اطلاعات غیرخالی لازم؛ مثلاً منبع و معیار مقایسه».
- [ ] T005 envelope سروری را در `src/01.Core/Application/ContentReview/ContentReviewEnvelope.cs` تعریف کن؛ schemaVersion: «content_review_v1»، postId: «از snapshot سروری، نه مدل»، source.contentRevision: «revision محتوای بررسی‌شده، هرگز revision تازه جایگزین نشود»، source.capturedAt: «زمان snapshot»، freshness.checkedAt: «زمان بازخوانی سروری پیش از تحویل»، isCurrentContent: «فقط در checkedAt؛ revision و متن دقیق برابر با snapshot»، requiresNewReview: «نقیض isCurrentContent»، report: «فقط گزارش معتبر، بدون refusal/failure»؛ timestampها UTC باشند.
- [ ] T006 [P] آزمون projection تازه و بدون tracking، حذف نرم و تغییر متن با DbContext مستقل را در `tests/BlogSpecLab.Tests/Integration/ContentReviewReaderTests.cs` بنویس؛ Include تاریخچه و write مجاز نیست.
- [ ] T007 [P] آزمون بودجهٔ monotonic با TimeProvider کنترل‌شده، سقف کار ۵۹ ثانیه، سقف کل ۶۰ ثانیه، Provider حداکثر ۵۰ ثانیه و ذخیرهٔ دو ثانیه برای validation/recheck را در `tests/BlogSpecLab.Tests/Application/ContentReviewDeadlineTests.cs` بنویس؛ کمبود بودجه و Client cancellation را متمایز کن.
- [ ] T008 reader اختصاصی `src/02.Infra/DataAccess/Posts/PostContentReviewReader.cs` را با projection immutable و AsNoTracking پیاده‌سازی کن؛ `PostStore.FindAsync` tracked را برای recheck استفاده نکن، جدول یا migration اضافه نکن (پس از T006).
- [ ] T009 deadline context با TimeProvider و بودجهٔ باقی‌مانده را در `src/01.Core/Application/ContentReview/ContentReviewDeadline.cs` پیاده‌سازی کن؛ token لینک‌شده به قطع Client و deadline و کنترل elapsed قبل از تحویل داشته باشد؛ سقف از Client قابل افزایش نباشد (پس از T007).
- [ ] T010 هماهنگ‌کنندهٔ HTTP و scope مستقل متعلق به عملیات را در `src/03.Endpoints/Api/Http/ContentReviewRequestCoordinator.cs` و `src/03.Endpoints/Api/Http/ContentReviewDeadlineMiddleware.cs` ایجاد کن؛ ساعت پیش از authentication برای همین مسیر شروع شود، انتظار dependency غیرهمکار قطع شود، task دیررس فقط دادهٔ immutable و scope خودش را نگه دارد، exception آن observe و scope فقط پس از پایان task dispose شود؛ پس از timeout به HttpContext/Response یا DbContext مشترک دسترسی و recheck تازه نداشته باشد.

**نقطهٔ کنترل**: قراردادها و reader/deadline آزمون‌پذیرند؛ هیچ تماس واقعی یا اختیار نوشتن ایجاد نشده است.

## فاز ۳: داستان ۱ — دریافت گزارش مشکلات احتمالی متن (P1)

**هدف**: مسیر کامل و مجاز درخواست صریح تا گزارش ماشین‌خوان؛ completed با مشکل یا بدون مشکل، با snapshot و کنترل تازهٔ دسترسی.

**آزمون مستقل**: در Draft و ChangesRequested متعلق به نویسندهٔ مجاز، با fake گزارش معتبر، status/issueها استخراج شوند، envelope revision درست داشته باشد و تمام داده‌های پست ثابت بمانند؛ آزمون adapter مستقل serialization واقعی SDK را با transport کنترل‌شده بررسی کند.

### آزمون‌ها

- [ ] T011 [P] [US1] آزمون Application برای Permission جاری/IsActive، مالکیت، Draft/ChangesRequested، عنوان/متن whitespace، snapshot دقیق و completed با چند مشکل یا بدون مشکل را در `tests/BlogSpecLab.Tests/Application/ContentReviewServiceTests.cs` بنویس؛ در precheck ناموفق gateway فراخوانی نشود (FR-001 تا FR-003، FR-005، FR-009).
- [ ] T012 [P] [US1] آزمون validator مستقل حتی برای fake gateway را در `tests/BlogSpecLab.Tests/Application/ContentReviewValidatorTests.cs` بنویس؛ enum ناشناخته، null/whitespace، suggestion خالی، ناسازگاری هر دو status با missingInformation، شاهد ناموجود و تفاوت Unicode/نیم‌فاصله رد و چند مشکل با شاهد مشترک پذیرفته شوند (FR-006، FR-007، FR-010).
- [ ] T013 [P] [US1] transport آزمون‌پذیر SDK و تست درخواست را در `tests/BlogSpecLab.Tests/AI/ContentReviewTransportTests.cs` بنویس؛ `/v1/responses`، non-streaming، strict Schema دقیق artifact با نام content_review_v1، store=false، input سریال‌شدهٔ فقط title/body، عدم tools/conversation/previous_response_id/metadata کاربری، retry صفر، cancellation و عدم عبور token کاربر را کنترل کن (FR-017).
- [ ] T014 [P] [US1] تست parsing و پروتکل را در `tests/BlogSpecLab.Tests/AI/ContentReviewProtocolTests.cs` بنویس؛ completed پروتکل از status گزارش مستقل باشد؛ refusal بر fragment مقدم، incomplete/پیام ناقص/متن مفقود/چند object/tool یا output غیرمنتظره InvalidOutput باشند؛ reasoning منتقل نشود؛ required/unknown/duplicate/type/enum، JSON خراب/code fence، سقف ۲۵۶ KiB UTF-8 و depth ۱۶ بررسی شوند؛ خطای شبکه/key/quota/429/5xx Unavailable باشد و cancellation به گزارش تبدیل نشود.
- [ ] T015 [US1] پشتیبانی fake gateway قابل توقف و clock برای WebApplicationFactory/SQLite واقعی را در `tests/BlogSpecLab.Tests/Integration/ContentReviewApiFactory.cs` بر الگوی `tests/BlogSpecLab.Tests/Integration/BlogApiFactory.cs` ایجاد کن؛ تست 200ها، 400/401/403/404/409، refusal/failure، no-store، body غیرخالی و id نامعتبر را در `tests/BlogSpecLab.Tests/Integration/ContentReviewApiTests.cs` بنویس؛ هیچ key یا شبکه لازم نباشد.

### پیاده‌سازی

- [ ] T016 [US1] validator مستقل در `src/01.Core/Application/ContentReview/ContentReviewReportValidator.cs` پیاده‌سازی کن؛ همهٔ قیود T004 و exact ordinal evidence در field snapshot، suggestion null یا غیر whitespace و رابطهٔ completed/needs_input با missingInformation اعمال شوند؛ ساختار معتبر تضمین صحت معنایی معرفی نشود (پس از T012).
- [ ] T017 [US1] `PostContentReviewService.ReviewAsync` را در `src/01.Core/Application/ContentReview/PostContentReviewService.cs` پیاده‌سازی کن؛ IUserAccessReader موجود، Permission پیش از lookup حساس، غیرمالک/حذف‌شده 404 یکسان، precheck و snapshot، gateway فقط Title/Body، validation مستقل، بازخوانی تازهٔ Permission سپس وجود/مالکیت، و envelope سروری؛ در همهٔ outcomeهای داخل مهلت خطای دسترسی/نبود پست بر outcome مدل مقدم باشد؛ DB failure به storage_unavailable و پاسخ دیررس کنار گذاشته شود؛ هیچ write session/lock/تراکنش طول تماس و cache ایجاد نشود (پس از T011، T016).
- [ ] T018 [P] [US1] دستور ثابت فارسی نسخه‌دار را در `src/02.Infra/AI/ContentReview/ContentReviewPrompt.cs` بنویس؛ چهار دسته، احتمال مشکل، پیشنهاد کمکی، evidence دقیق، needs_input برای اطلاعات ناکافی، عدم ساخت منبع و عدم حکم قطعی/تأیید/انتشار؛ متن عنوان/بدنه فقط object سریال‌شدهٔ user باشد، دستور داخل محتوا داده محسوب شود؛ به system/developer interpolate نشود.
- [ ] T019 [P] [US1] parser سخت‌گیرانهٔ System.Text.Json را در `src/02.Infra/AI/ContentReview/ContentReviewOutputParser.cs` پیاده‌سازی کن؛ قیود wire و حدود T014، یک object کامل و suggestion required nullable، بدون coercion/repair/استخراج code fence/truncation؛ package عمومی Schema اضافه نکن.
- [ ] T020 [US1] adapter در `src/02.Infra/AI/ContentReview/OpenAiContentReviewGateway.cs` با ResponsesClient/CreateResponseAsync و API منتخب قرارداد بساز؛ TextOptions.TextFormat با CreateJsonSchemaFormat و StoredOutputEnabled=false، MaxOutputTokenCount اولیه 8192، cancellation/بودجهٔ Provider، retry صفر و request options مستقل؛ outcome پروتکل قبل از parsing و خطاهای امن؛ singleton thread-safe، بدون tool/write/DB، fallback یا تماس اصلاحی؛ Reasoning تنظیم Feature-level قابل ارزیابی باشد و مقدار حدسی تثبیت نشود (پس از T013، T014، T018، T019).
- [ ] T021 [US1] outcomeهای جدید را در `src/01.Core/Application/Common/OperationResult.cs` و نگاشت پیام امن فارسی را در `src/03.Endpoints/Api/Http/ProblemResults.cs` اضافه کن؛ 422/refused، 502/invalid_output، 503/unavailable یا storage_unavailable، 504/timeout و 500/unexpected مطابق HTTP contract، بدون raw output/refusal/SDK/secret؛ خطا هرگز completed خالی نشود.
- [ ] T022 [US1] DTO و نگاشت HTTP envelope را در `src/03.Endpoints/Api/Contracts/ContentReviewContracts.cs` و مسیر `POST /api/author/posts/{id}/content-review` را در `src/03.Endpoints/Api/Endpoints/ContentReviewEndpoints.cs` اضافه کن؛ هویت معتبر سروری، body خالی، رد body غیرخالی/id نامعتبر، بدون If-Match/ETag و همیشه no-store؛ قواعد کسب‌وکار به service واگذار شوند (پس از T015، T017، T021).
- [ ] T023 [US1] تنظیم‌های Enabled=false پیش‌فرض، Model=gpt-6-luna و key فقط از secret محیطی را در `src/02.Infra/AI/ContentReview/ContentReviewOptions.cs` و gateway بدون شبکهٔ unavailable را در `src/02.Infra/AI/ContentReview/UnavailableContentReviewGateway.cs` بساز؛ DI/ثبت reader/service/adapter/coordinator را در `src/03.Endpoints/Api/Configuration/ContentReviewConfiguration.cs` و ترتیب middleware پیش از auth و نگاشت endpoint را در `src/03.Endpoints/Api/Program.cs` وصل کن؛ disabled/تنظیم ناقص CRUD را متوقف نکند، startup تماس Provider/migration/provision نداشته باشد.
- [ ] T024 [US1] metadata اختصاصی گزارش را در `src/03.Endpoints/Api/Http/OpenApiMetadata.cs` اضافه کن؛ DocumentPost مربوط به mutation را استفاده نکن؛ schema envelope/report، enumها، nullable suggestion، همهٔ status/codeها و نبود الزام If-Match/ETag را در `tests/BlogSpecLab.Tests/Integration/ContentReviewOpenApiTests.cs` کنترل کن.
- [ ] T025 [US1] مجموعهٔ آزمون‌های T011 تا T015 و T024 را اجرا و نتیجهٔ غیرصفر آزمون‌های این داستان و برابری Schema بسته‌بندی‌شده با artifact را در `specs/002-ai-content-review/validation.md` ثبت کن؛ شکست‌ها رفع شوند، هیچ تماس زنده شاهد CI معرفی نشود.

**نقطهٔ کنترل**: برش اولیهٔ قابل نمایش US1 کار می‌کند؛ پذیرش نهایی کیفیت و کل قابلیت هنوز به فازهای بعد وابسته است.

## فاز ۴: داستان ۲ — تشخیص نیاز به اطلاعات بیشتر (P1)

**هدف**: گزارش نیاز اطلاعات، گزارش مختلط و درخواست تازه پس از ذخیرهٔ اطلاعات؛ قرارداد و validation پایهٔ این وضعیت از ابتدا موجود است.

**آزمون مستقل**: متن «طبق گزارش، این روش بهترین نتیجه را دارد» needs_input با شاهد/دلیل/اطلاعات درخواستی بدهد؛ گزارش مختلط مشکل نگارشی را حفظ کند؛ پس از ویرایش مستقل و ذخیره، درخواست دوم متن تازه را ارسال کند، بدون cache یا endpoint مکالمه.

- [ ] T026 [P] [US2] آزمون needs_input مستقل و مختلط، تمایز با completed بدون مشکل/refusal/failure و snapshot جدید پس از ذخیره را در `tests/BlogSpecLab.Tests/Application/ContentReviewNeedsInputTests.cs` بنویس (FR-007، FR-008، FR-015).
- [ ] T027 [P] [US2] آزمون HTTP دو گزارش needs_input با issues خالی/پر و ویرایش با ETag موجود سپس درخواست تازه را در `tests/BlogSpecLab.Tests/Integration/ContentReviewNeedsInputApiTests.cs` بنویس؛ missingInformation بدون تفسیر summary قابل استخراج و شاهد مربوط به هر snapshot باشد.
- [ ] T028 [US2] نمونه‌های ارزیابی ابهام/کمبود منبع و متن مختلط را در `tests/BlogSpecLab.Tests/Fixtures/ContentReviewNeedsInputCases.json` تعریف کن و دستور `src/02.Infra/AI/ContentReview/ContentReviewPrompt.cs` را با آن‌ها بازبینی و در صورت نیاز تکمیل کن؛ ارزیابی ادعای بی‌پشتوانه نامشخص، نیاز به منبع/معیار صریح و هیچ اطلاعات ساخته‌شده‌ای مجاز نیست؛ نسخهٔ prompt و نیاز Eval مجدد ثبت شود.
- [ ] T029 [US2] اجرای سناریوهای T026/T027 و تمایزهای ماشین‌خوان آن‌ها را در `specs/002-ai-content-review/validation.md` ثبت کن؛ اگر خللی در service/validator کشف شد همان قواعد مصوب را در `src/01.Core/Application/ContentReview/PostContentReviewService.cs` یا `src/01.Core/Application/ContentReview/ContentReviewReportValidator.cs` اصلاح کن؛ آزمون قطعی را شاهد عدم hallucination مدل زنده معرفی نکن.

**نقطهٔ کنترل**: مسیر نرم‌افزاری needs_input و بررسی تازه مستقل قابل آزمون‌اند؛ SC-002 معنایی در Eval واقعی بررسی می‌شود.

## فاز ۵: داستان ۳ — حفظ اختیار نویسنده و بررسی انسانی (P1)

**هدف**: اثبات عدم تغییر، عدم افشا، تازگی و سقف زمان در تمام مسیرها؛ کنترل‌های امنیتی مسیر اولیه حفظ شوند.

**آزمون مستقل**: قبل/بعد دادهٔ کامل پست مقایسه شود؛ با gateway متوقف، ویرایش/submit/حذف/لغو Permission مستقل انجام و سپس پاسخ آزاد شود؛ timeout پیش از پایان dependency غیرهمکار پاسخ دهد و موفقیت دیررس تحویل نشود.

- [ ] T030 [P] [US3] invariant کامل Title/Body/OwnerId/Status/ContentRevision/Version/IsDeleted/CurrentReviewId/Reviews/دلیل برگشت/Publication را برای completed، needs_input، refusal، failure، تکرار و متن دارای دستور انتشار در `tests/BlogSpecLab.Tests/Integration/ContentReviewNonMutationTests.cs` مقایسه کن؛ submit بدون گزارش و پس از گزارش مشکل‌دار یا failure همچنان طبق قواعد موجود و تأیید انسانی اجرا شود (FR-012، FR-013؛ SC-003).
- [ ] T031 [P] [US3] رقابت ویرایش، بازگرداندن متن به مقدار قبلی با revision بالاتر، submit بدون تغییر محتوا، حذف نرم، لغو Permission و اختلال DB در recheck را با scope مستقل و gateway متوقف در `tests/BlogSpecLab.Tests/Integration/ContentReviewConcurrencyTests.cs` بنویس؛ revision همراه متن معیار freshness، submit به‌تنهایی stale نیست، حذف 404، لغو 403 و DB failure 503 بدون گزارش باشند (FR-001، FR-014؛ SC-007).
- [ ] T032 [P] [US3] آزمون clock کنترل‌شده برای auth/storage/Provider کند، gateway غیرهمکار، پاسخ درست یا refusal دیررس، کمبود بودجه و Client cancellation را در `tests/BlogSpecLab.Tests/Integration/ContentReviewTimeoutTests.cs` بنویس؛ deadline از دریافت HTTP و موفقیت دیررس ممنوع؛ بعد timeout scope تا پایان task زنده، سپس dispose، exception observe و درخواست بعدی مستقل باشد.
- [ ] T033 [P] [US3] آزمون elapsed واقعی با Client متصل و dependency متوقف در `tests/BlogSpecLab.Tests/Integration/ContentReviewElapsedTests.cs` اضافه کن؛ آماده‌سازی 504/ai_review_timeout حداکثر تا ۶۰ ثانیه از دریافت HTTP، زمان شبکهٔ Client خارج اندازه‌گیری؛ تست در suite قطعی واقعاً اجرا شود (FR-018؛ SC-008).
- [ ] T034 [P] [US3] ثبت log/trace و ProblemDetails در موفقیت/refusal/invalid_output/transport failure و denied access را در `tests/BlogSpecLab.Tests/Integration/ContentReviewPrivacyTests.cs` بررسی کن؛ متن، شاهد، raw response/refusal، key، credential/token و اطلاعات حساب افشا نشوند؛ غیرمالک 404 یکسان و کاربر بدون مجوز پیش از lookup/Provider رد شود (SC-004، FR-011، FR-017).
- [ ] T035 [P] [US3] در `tests/BlogSpecLab.Tests/Integration/ContentReviewConfigurationTests.cs` disabled و تنظیم ناقص را کنترل کن؛ درخواست مجاز unavailable و غیرمجاز خطای دسترسی، CRUD اول بدون key فعال، startup بدون شبکه و secrets، مدل/prompt قابل تغییر از Client نباشند.
- [ ] T036 [US3] منطق freshness و ترتیب recheck/outcome را با نتایج T031 در `src/01.Core/Application/ContentReview/PostContentReviewService.cs` تکمیل کن؛ source revision هرگز جایگزین نشود، تغییر مستقل کاربر حفظ شود، تغییر صرف وضعیت snapshot را نامعتبر نکند و بعد deadline هیچ recheck جدید آغاز نشود.
- [ ] T037 [US3] مالکیت scope/task و انتشار cancellation را با نتایج T032/T033 در `src/03.Endpoints/Api/Http/ContentReviewRequestCoordinator.cs`، `src/03.Endpoints/Api/Http/ContentReviewDeadlineMiddleware.cs` و `src/03.Endpoints/Api/Security/OpaqueBearerHandler.cs` تکمیل کن؛ authentication نیز داخل بودجه، بدون dispose زودرس DbContext فعال یا ادامهٔ دیررس روی Response؛ رفتار مسیرهای قبلی حفظ شود.
- [ ] T038 [US3] حفاظت log/trace تنظیم‌های SDK و exceptionهای امن را با نتایج T034 در `src/02.Infra/AI/ContentReview/OpenAiContentReviewGateway.cs` و `src/03.Endpoints/Api/Configuration/ContentReviewConfiguration.cs` تکمیل کن؛ telemetry فقط outcome/زمان/نسخهٔ غیرحساس، بدون body logging یا raw failure.
- [ ] T039 [US3] راهنمای نویسندهٔ `specs/002-ai-content-review/quickstart.md` را با رفتار واقعی تطبیق بده؛ درخواست body خالی، ارسال بیرونی فقط عنوان/متن با درخواست صریح، statusها، کمکی‌بودن، اصلاح/submit مستقل، stale حین بررسی و مقایسهٔ revision بعد دریافت، failure/timeout و درخواست تازه را توضیح بده؛ صفحهٔ وب/رضایت جداگانه اضافه نکن (FR-016).
- [ ] T040 [US3] آزمون‌های T030 تا T035 و regression قابلیت اول را اجرا و شواهد SC-003/SC-004/SC-007/SC-008 و محدودیت checkedAt را در `specs/002-ai-content-review/validation.md` ثبت کن؛ تغییرات مستقل fixture از اثر AI تفکیک شوند.

**نقطهٔ کنترل**: ناورداها، امنیت و deadline شواهد خودکار دارند؛ کیفیت مدل و فهم انسانی همچنان شرط پذیرش‌اند.

## فاز ۶: ارزیابی و تکمیل مشترک

**هدف**: تفکیک CI از Provider/Eval و اثبات همهٔ معیارهای پذیرش؛ این وظایف هنگام Implement/Test اجرا می‌شوند، نه هنگام تولید این سند.

- [ ] T041 [P] مجموعهٔ مصنوعی نسخه‌دار حداقل ۲۰ متن فارسی دارای ایراد و ۱۰ متن سالم را در `tests/BlogSpecLab.Tests/Fixtures/ContentReviewAcceptanceCorpus.json` بساز؛ gold انسانی شامل issue-id/category/field/evidence/معنای ایراد و missing-information، هر چهار دسته، title/body، چند مشکل یک بخش، متن مختلط و ابهام/منبع مفقود؛ پیش از اولین run با بازبینی انسانی ثابت شود، متن واقعی نویسنده وارد نشود.
- [ ] T042 [P] rubric و پروتکل سه run از پیش تعیین‌شده و آزمون فهم حداقل پنج نویسنده را در `specs/002-ai-content-review/evaluation-protocol.md` ثبت کن؛ هر run جدا recall≥۹۰٪ و clean rate≥۹۰٪، هر gold حداکثر یک بار، SC-002 در ۱۰۰٪ نمونه‌های اطلاعات ناکافی؛ failureها از denominator حذف نشوند و بهترین run انتخاب نشود؛ حداقل ۸۰٪ نویسندگان تمام وضعیت‌ها و استقلال اصلاح/ارسال را درست بفهمند.
- [ ] T043 آزمون واقعی opt-in با Trait `Category=AIProvider` را در `tests/BlogSpecLab.Tests/AI/ContentReviewProviderTests.cs` ایجاد کن؛ فقط اجرای صریح با secret محیطی، schema acceptance/access/parsing/cancellation و تنظیم منتخب SDK/مدل را بررسی کند؛ نبود key یا skip شاهد پذیرش نیست، CI معمولی به شبکه وابسته نباشد.
- [ ] T044 runner واقعی opt-in با Trait `Category=AIQuality` را در `tests/BlogSpecLab.Tests/AI/ContentReviewQualityTests.cs` بساز؛ corpus ثابت و rubric T041/T042، خروجی مصنوعی برای رأی انسانی، outcome/latency هر نمونه، مدل درخواستی و مدل واقعی Provider، تاریخ، SDK، prompt/Schema/corpus version و تنظیم مؤثر Reasoning/بودجه ثبت شوند؛ موفقیت process جای نمرهٔ انسانی نیست و secrets ثبت نشوند.
- [ ] T045 با مدل اولیه gpt-6-luna و MaxOutputTokenCount=8192، smoke/benchmark واقعی و ارزیابی Reasoning و کفایت بودجه را طبق `specs/002-ai-content-review/evaluation-protocol.md` اجرا و شواهد را در `specs/002-ai-content-review/evaluation.md` ثبت کن؛ تنظیم مؤثر پیش از سه run پذیرش ثابت شود؛ snapshot حدسی، temperature تحمیلی یا fallback خودکار نساز؛ دسترسی ناموجود/کیفیت یا latency نامناسب مانع پذیرش و نیازمند بازبینی طراحی براساس شواهد است.
- [ ] T046 سه اجرای ثابت و رأی انسانی SC-002/SC-005 را در `specs/002-ai-content-review/evaluation.md` ثبت کن؛ هر run جدا همهٔ آستانه‌ها را پاس کند، خطا/refusal/timeout/invalid output نمونهٔ سالم پاس محسوب نشود و threshold برای نتیجهٔ مدل تغییر نکند؛ تغییر مدل/prompt/Schema/تنظیم مؤثر Eval تازه می‌خواهد (پس از T045).
- [ ] T047 ارزیابی راهنما با حداقل پنج نویسنده را طبق `specs/002-ai-content-review/evaluation-protocol.md` انجام و پاسخ‌ها/ابهام‌ها/نرخ فهم SC-006 را در `specs/002-ai-content-review/evaluation.md` ثبت کن؛ برای پنج نفر حداقل چهار نفر پاسخ کامل صحیح بدهند؛ اگر مشارکت‌کنندگان در دسترس نیستند این شرط باز بماند و نتیجهٔ ساختگی ثبت نشود (پس از T039، T046).
- [ ] T048 راهنمای عملیاتی `docs/operations/local-access.md` و فرمان‌های واقعی `specs/002-ai-content-review/quickstart.md` را برای Enabled/Model/secret محیطی، اجرای بدون AI، filterهای AIProvider/AIQuality و تفاوت CI/Eval تکمیل کن؛ مقدار secret، migration خودکار یا قابلیت پیاده‌نشده درج نشود.
- [ ] T049 restore/tool restore/build/test Solution را مطابق `BlogSpecLab.slnx` و فرمان‌های `specs/002-ai-content-review/quickstart.md` اجرا کن؛ suite قطعی بدون key/شبکه و با تعداد غیرصفر Feature tests و regression قبلی پاس شود؛ Project Referenceها و نبود migration/Domain dependency جدید بررسی و شواهد در `specs/002-ai-content-review/validation.md` ثبت شوند.
- [ ] T050 نگاشت نهایی FR-001 تا FR-018 و SC-001 تا SC-008 به تست/شواهد را در `specs/002-ai-content-review/validation.md` ثبت کن؛ کیفیت یا فهم راهنمای ناموفق/اجرانشده مانع پذیرش است، مورد باز و ریسک SDK experimental/alias مدل شفاف گزارش شود و تکمیل کد به‌تنهایی پذیرش معرفی نشود.

## وابستگی‌ها و ترتیب اجرا

```text
T001 → T002 → فاز ۲ (T003–T010)
                    ↓
              US1 (T011–T025)
                    ↓
              US2 (T026–T029)
                    ↓
              US3 (T030–T040)
                    ↓
      تکمیل/Eval (T041–T050) → بازبینی پذیرش
```

- فاز ۱ زیرساخت پروژه است؛ فاز ۲ برای همه مسدودکننده است. T004 به T003 و T005 به T004 وابسته‌اند. T006/T007 پس از قراردادها موازی‌اند؛ T008/T009 پس از آزمون متناظر، T010 پس از T009.
- US1 مسیر مشترک را می‌سازد. T011 تا T014 بعد از فاز ۲ موازی‌اند؛ T015 fixture مشترک HTTP را می‌سازد. T016/T017 به ترتیب، T018/T019 هم‌زمان در فایل‌های جدا، سپس T020 اجرا شوند. T021/T022/T023/T024 به ترتیب ادغام و T025 پس از همه‌اند.
- US2 برای تحویل HTTP به US1 وابسته است؛ با fixtureهای خودش مستقل آزموده می‌شود. T026/T027 موازی، سپس T028/T029. قرارداد needs_input از فاز ۲ و validator US1 کامل است؛ این فاز بررسی معنای موارد اطلاعات ناکافی را تکمیل می‌کند.
- US3 برای آزمون end-to-end به US1 و برای مجموعهٔ کامل outcomeها به US2 وابسته است؛ T030 تا T035 در فایل‌های مستقل موازی‌اند، T036 تا T039 به ترتیب برای جلوگیری از تعارض فایل‌های مشترک و سپس T040.
- فاز ۶ پس از داستان‌ها انجام شود. T041/T042 موازی، سپس T043/T044 و T045 تا T050 به ترتیب. corpus و gold پیش از تماس پذیرش ثابت شوند؛ هیچ اجرای خودکار این سند به‌معنای فراهم‌بودن مشارکت انسانی نیست.
- اجرای کامل داستان‌ها هم‌زمان توصیه نمی‌شود: service، prompt، Program و DI مشترک‌اند. استقلال آزمون یعنی هر داستان با fixture مستقل قابل اثبات است، نه حذف وابستگی واقعی مسیر مشترک.

## نمونه‌های اجرای موازی

- **زیرساخت**: T006 خواندن تازه و T007 ساعت/بودجه، پس از T003–T005.
- **US1**: T011 آزمون service، T012 validator، T013 serialization و T014 protocol؛ بعد از آماده‌شدن ورودی‌هایشان، T018 prompt و T019 parser.
- **US2**: T026 آزمون Application و T027 آزمون HTTP، پس از T025.
- **US3**: T030 ناوردا، T031 concurrency، T032 ساعت کنترل‌شده، T033 elapsed واقعی، T034 privacy و T035 configuration، پس از fixture مشترک و T029. آزمون زمان واقعی در اجرای جدا از فشار موازی برای اندازه‌گیری قابل اتکا اجرا شود.
- **تکمیل**: T041 corpus و T042 rubric، پیش از تثبیت gold و اجرا.

این مثال‌ها فرصت‌های تقسیم کارند؛ دستور به ایجاد عامل فرعی یا شروع Implement نیستند.

## راهبرد پیاده‌سازی و MVP

1. فازهای ۱ و ۲ را تکمیل و بررسی کن؛ پروژه و قرارداد اضافی خارج از Plan ایجاد نشود.
2. US1 را به‌عنوان برش اولیهٔ MVP با fake/transport ثابت و گزارش completed نشان بده؛ کنترل دسترسی، عدم write و deadline از همین برش الزامی‌اند.
3. توقف و اعتبارسنجی US1، سپس US2 و US3 را به‌ترتیب اضافه و regression بگیر. گزارش هر داستان مستقل از عبارت‌بندی مدل آزموده شود.
4. corpus/rubric را تثبیت، CI را از Provider/Eval جدا و آزمون واقعی و فهم راهنما را انجام بده. **MVP قابل پذیرش این قابلیت شامل هر سه داستان P1 و عبور از تمام Quality Gateهاست**؛ نمایش US1 به‌تنهایی پذیرش کامل نیست.
5. در هر نقطهٔ کنترل نتیجه را قابل بازبینی کن؛ بدون دستور کاربر وارد قابلیت بعدی SDD نشو. این مرحله فقط همین `tasks.md` را تولید می‌کند.

## پوشش و یادداشت‌ها

| نیازمندی | وظایف اصلی |
| --- | --- |
| FR-001 تا FR-003؛ SC-004 | T003، T008، T011، T015، T017، T022، T031، T034 |
| FR-004 تا FR-010؛ SC-001 | T004، T012 تا T020، T024، T026 تا T029، T041 تا T046 |
| FR-011 | T014، T015، T021، T031 تا T035 |
| FR-012، FR-013؛ SC-003 | T017، T020، T030، T040 |
| FR-014، FR-015؛ SC-007 | T005، T017، T026، T027، T031، T036، T039 |
| FR-016؛ SC-006 | T039، T042، T047، T048 |
| FR-017 | T013، T018، T020، T034، T038، T039 |
| FR-018؛ SC-008 | T007، T009، T010، T020، T032، T033، T037 |
| SC-002، SC-005 | T028، T041 تا T046 |

هیچ وظیفه‌ای در این سند انجام‌شده علامت نمی‌خورد. برای خطاهای حساب/کیفیت یا مشارکت انسانیِ ناموجود، شواهد و مانع واقعی ثبت شود؛ آزمون skipشده، JSON معتبر یا موفقیت build جای پذیرش نیست. Schema، Prompt و انتخاب Provider/Model/SDK تصمیم‌های همین Feature باقی می‌مانند؛ Constitution و Baseline تغییر نمی‌کنند.
