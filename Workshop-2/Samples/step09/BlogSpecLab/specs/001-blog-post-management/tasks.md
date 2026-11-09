# وظایف: مدیریت و انتشار کنترل‌شدهٔ پست‌های وبلاگ

**ورودی**: اسناد `specs/001-blog-post-management/`: `spec.md`، `plan.md`، `research.md`، `data-model.md`، `contracts/http-api.md` و `quickstart.md`؛ همچنین Constitution و `docs/architecture/technical-baseline.md`.

**وضعیت**: فهرست اجرای آینده؛ هیچ وظیفه‌ای اجرا نشده است. این فرمان فقط Tasks است و شروع Implement یا پذیرش قابلیت محسوب نمی‌شود.

**آزمون‌ها**: سناریوهای پذیرش Spec و الزام آزمون خودکار Constitution با طراحی آزمون Plan مبنا هستند. آزمون‌های هر داستان پیش از پیاده‌سازی رفتار نوشته شوند و شکست مرتبط با رفتار دیده شود؛ خطای کامپایل به‌تنهایی شاهد شکست رفتاری نیست.

**قالب**: `- [ ] Tnnn [P?] [USn?] شرح همراه مسیر`. `[P]` فقط برای فایل‌های مستقل در گروه‌های مشخص‌شده است؛ پیش‌نیاز گروه باید کامل باشد. مسیرها نسبت به ریشهٔ مخزن‌اند. شناسه‌های کد انگلیسی و مستندات و پیام‌های انسانی فارسی باشند.

## فاز ۱: آماده‌سازی مشترک

**هدف**: ایجاد فقط چهار پروژهٔ اجرایی و یک پروژهٔ آزمون مطابق Plan؛ بدون پروژهٔ AI یا Qdrant.

- [ ] T001 بررسی SDK نصب‌شدهٔ .NET 10 و ایجاد `BlogSpecLab.slnx` و پروژه‌های `src/01.Core/Domain/BlogSpecLab.Core.Domain.csproj`، `src/01.Core/Application/BlogSpecLab.Core.Application.csproj`، `src/02.Infra/DataAccess/BlogSpecLab.Infra.DataAccess.csproj`، `src/03.Endpoints/Api/BlogSpecLab.Endpoints.Api.csproj` و `tests/BlogSpecLab.Tests/BlogSpecLab.Tests.csproj` با target برابر net10.0؛ Domain مستقل، Application فقط به Domain، DataAccess به Core، Api به Application/DataAccess و Tests به پروژه‌های لازم رفرنس داشته باشند.
- [ ] T002 افزودن EF Core SQLite/Design 10 به `src/02.Infra/DataAccess/BlogSpecLab.Infra.DataAccess.csproj`، OpenAPI 10 به `src/03.Endpoints/Api/BlogSpecLab.Endpoints.Api.csproj` و xUnit، runner، Test SDK سازگار و Mvc.Testing 10 به `tests/BlogSpecLab.Tests/BlogSpecLab.Tests.csproj`؛ نسخهٔ دقیق پایدار را هنگام restore بررسی و تثبیت و ابزار dotnet-ef 10 را در `.config/dotnet-tools.json` ثبت کنید.
- [ ] T003 [P] تنظیم مسیر SQLite بدون secret در `src/03.Endpoints/Api/appsettings.json` و HTTPS محلی در `src/03.Endpoints/Api/Properties/launchSettings.json`؛ فایل DB، فایل‌های جانبی SQLite، خروجی ساخت و secretهای محلی را در `.gitignore` مستثنا کنید.
- [ ] T004 [P] ایجاد میزبان آزمون با handler واقعی، فایل SQLite موقت، Credential تصادفی و پاک‌سازی منابع در `tests/BlogSpecLab.Tests/Integration/BlogApiFactory.cs` و سازندهٔ fixture بدون secret ثابت در `tests/BlogSpecLab.Tests/Fixtures/TestUsers.cs`؛ خروجی آزمون token یا متن خصوصی را چاپ نکند. آزمون Host-level تنظیمات را در `tests/BlogSpecLab.Tests/Integration/StartupConfigurationTests.cs` آماده کنید: تنظیمات الزامی معتبر میزبان قابل راه‌اندازی بدهند؛ Connection String غایب، خالی یا با قالب نامعتبر باعث Fail-fast شود و پیام خطا Secret، Credential، مقدار Connection String یا اطلاعات حساس را افشا نکند؛ Startup عادی Migration/Provision ضمنی نکند و نبود کاربران provision‌شده مانع آن نباشد. در این فاز فقط fixture و طراحی آزمون آماده می‌شوند؛ اجرای آزمون منتظر T010 و T017 است. آزمون خطای Runtime دیتابیس و 503 در T031 مستقل باقی بماند.

## فاز ۲: زیرساخت پایهٔ مسدودکننده

**هدف**: هویت، مجوز جاری، نتیجهٔ مورد کاربرد و مرز تراکنش؛ پیش از داستان‌ها کامل شود. Aggregate و migration پست در US1 ساخته می‌شوند.

- [ ] T005 [P] تعریف `UserAccess` و قرارداد خواندن دسترسی در `src/01.Core/Application/Security/UserAccess.cs` و `src/01.Core/Application/Security/IUserAccessReader.cs` با قواعد عین مدل: UserId «هویت معتبر؛ مستقل از نام نمایشی»، CanWrite «مجوز فعلی نویسندگی»، CanReview «مجوز فعلی بررسی و تأیید»، CanPublish «مجوز فعلی انتشار»، IsActive «حساب غیرفعال مجوز عملیاتی ندارد»؛ نگاشت Author/Editor/دو نقش فقط سروری باشد.
- [ ] T006 [P] تعریف قرارداد verifier در `src/01.Core/Application/Security/ICredentialVerifier.cs`، نتیجه‌های مستقل از HTTP در `src/01.Core/Application/Common/OperationResult.cs` و قرارداد حداقلی تراکنش نوشتن در `src/01.Core/Application/Persistence/IWriteSession.cs`؛ قرارداد اجازهٔ خواندن مجوز و Post در همان تراکنش و یک Commit یا rollback دهد و DbContext/IQueryable را به Core نشت ندهد.
- [ ] T007 پیاده‌سازی `src/02.Infra/DataAccess/BlogDbContext.cs`، دادهٔ Credential در `src/02.Infra/DataAccess/Security/Credential.cs` و verifier در `src/02.Infra/DataAccess/Security/CredentialVerifier.cs`؛ Credential شامل Id/UserId و «TokenHash یکتا، ExpiresAt و RevokedAt nullable» باشد؛ hash برابر SHA-256 روی token حداقل ۳۲ بایتی تصادفی، هویت منقضی/لغوشده نامعتبر و مقدار خام خارج از DB/log/repo بماند؛ روابط تاریخی با لغو دسترسی حذف نشوند.
- [ ] T008 پیاده‌سازی تراکنش کوتاه SQLite با گرفتن write transaction پیش از خواندن مجوز و داده در `src/02.Infra/DataAccess/Persistence/SqliteWriteSession.cs` و خواندن بدون cache مجوز در `src/02.Infra/DataAccess/Security/UserAccessReader.cs`؛ retry محدود فقط گرفتن lock پیش از تصمیم، Commit واحد، rollback کامل و خطای storage_unavailable پس از اتمام مهلت؛ بدون retry فرمان تجاری.
- [ ] T009 پیاده‌سازی حالت اپراتوری محلی provision و لغو credential/مجوز در `src/03.Endpoints/Api/Operations/AccessCommands.cs` و `src/02.Infra/DataAccess/Security/AccessProvisioner.cs`؛ از همان DB و write transaction استفاده کند، حداقل پنج Author و پنج Editor و یک کاربر دو نقشی قابل ایجاد باشند، token فقط یک‌بار به secret storage خارج مخزن تحویل شود و endpoint مدیریت حساب ایجاد نشود.
- [ ] T010 پیاده‌سازی AuthenticationHandler در `src/03.Endpoints/Api/Security/OpaqueBearerHandler.cs` و wiring پایه در `src/03.Endpoints/Api/Program.cs`؛ handler فقط هویت معتبر را استخراج کند، 401 همراه WWW-Authenticate باشد و ادعاهای نقش/مالکیت header مجوز نسازند. پیش از اجرای HTTP Host، وجود و قالب تنظیمات الزامی Authentication/Persistence مطابق R4 در research بررسی شود؛ حداقل Connection String تعریف‌شده، غیرخالی و با قالب قابل استفاده لازم است و تنظیمات ناقص/نامعتبر Fail-fast بدون افشای Secret یا مقدار تنظیمات حساس بدهند. Availability لحظه‌ای دیتابیس، Schema Migration یا Certificate Validation سفارشی جزو Configuration Validation نباشند؛ Startup عادی Migration/Provision نکند، ابزار آماده‌سازی پیش از Host قابل اجرا باشد و HTTPS به Host و سازوکار استاندارد محیط واگذار شود. آزمون Startup تعریف‌شده در T004 پس از T017 اجرا شود.
- [ ] T011 پیاده‌سازی نگاشت OperationResult به ProblemDetails در `src/03.Endpoints/Api/Http/ProblemResults.cs` و parsing در `src/03.Endpoints/Api/Http/VersionHeaders.cs`؛ code/statusهای قرارداد، پیام فارسی، 500 عمومی بدون جزئیات داخلی، no-store برای مدیریت/خطا و ETag قوی UUID داخل کوتیشن؛ نبود If-Match برابر 428 و wildcard/چند مقدار/قالب نامعتبر برابر 400 باشد.

**نقطهٔ کنترل**: کد پایهٔ احراز هویت، خواندن مجوز جاری، ابزار اپراتوری و مرز تراکنش آماده است. این نقطه به معنای قابل اجرا بودن End-to-End مسیر واقعی هویت، Provision و Persistence نیست؛ اجرای آن‌ها به ایجاد Schema و اعمال Migration اولیه در T017 وابسته است. اعتبارسنجی اجرایی این بخش پس از T017 و پیش از پذیرش User Story 1 انجام می‌شود.

## فاز ۳: داستان ۱ — مدیریت پست‌های خود نویسنده (P1؛ MVP نویسنده)

**هدف**: ایجاد، فهرست، جزئیات، اصلاح، حذف منطقی و ارسال با حفظ مالکیت؛ FR-001 تا FR-005، FR-010، FR-013 و بخش نویسندهٔ FR-016.

**آزمون مستقل**: دو Author؛ ایجاد Draft غیرعمومی، CRUD پست خود، ارسال PendingReview، رد متن خالی و رد دسترسی Author دوم بدون تغییر داده. fixture می‌تواند وضعیت‌های دیگر را فراهم کند؛ برای آزمون US1 به endpoint بررسی نیاز نیست.

### آزمون‌ها

- [ ] T012 [P] [US1] نوشتن آزمون‌های قواعد Create/Edit/Delete/Submit در `tests/BlogSpecLab.Tests/Domain/PostAuthorTests.cs`؛ null/empty/whitespace، Unicode بدون نرمال‌سازی، متن یکسان بدون ابطال تأیید، revision فقط با تغییر واقعی، حفظ ChangesRequested و دلیل تاریخی، بازگشت PendingReview/Approved به Draft با تغییر واقعی، حذف همهٔ وضعیت‌های منتشرنشده و منع تغییر Published را پوشش دهید.
- [ ] T013 [P] [US1] نوشتن آزمون‌های Application در `tests/BlogSpecLab.Tests/Application/AuthorPostTests.cs` برای هویت/مجوز جاری، مالکیت ثابت، عدم دسترسی نویسندهٔ دیگر، ناموجود/حذف‌شده، ترتیب بررسی دسترسی پیش از تعارض، Version قدیمی و شکست بدون تغییر؛ وابستگی‌های ذخیره‌سازی را بدون سرویس بیرونی جایگزین کنید.
- [ ] T014 [P] [US1] نوشتن آزمون قرارداد شش مسیر Author در `tests/BlogSpecLab.Tests/Integration/AuthorApiTests.cs` مطابق `contracts/http-api.md`؛ 201/Location/ETag، 200/204، فهرست مرتب Id شامل Published خود، 400/401/403/404/409/412/428، فیلدهای اضافی، no-store و عدم افشای دادهٔ دیگران را با handler واقعی و SQLite بسنجید؛ مقادیر دقیق، ترتیب ثابت و نبود تکرار allowedActions را برای مالک در وضعیت‌های اصلی و کاربر دو نقشی بررسی کنید؛ غیرمالک PostView یا allowedActions پست دیگری دریافت نکند و Published آرایهٔ خالی داشته باشد. بررسی اقدامات ویراستاری کاربر دو نقشی به رفتار US2 وابسته است و در T023 و رگرسیون US1 تکمیل می‌شود.

### پیاده‌سازی

- [ ] T015 [US1] ایجاد Aggregate در `src/01.Core/Domain/Posts/Post.cs` و enum در `src/01.Core/Domain/Posts/PostStatus.cs` با قواعد عین مدل: Id «تولید سمت سرور»، OwnerId «از هویت ایجادکننده؛ ثابت»، Title/Body «غیر null، غیرخالی و غیر whitespace؛ بدون سقف طول کسب‌وکاری ساختگی»، Status «Draft، PendingReview، ChangesRequested، Approved، Published»، ContentRevision «از ۱؛ فقط با تغییر واقعی Title یا Body افزایش می‌یابد»، Version «توکن رقابت؛ در هر تغییر ذخیره‌شده، از جمله انتقال وضعیت، عوض می‌شود»، IsDeleted «خروج منطقی از تمام مسیرهای مشاهده و گردش کار»، CurrentReviewId «آخرین تصمیم مرتبط؛ تصمیم‌های قبلی تاریخچه‌اند» از نوع UUID nullable، Publication «فقط در Published موجود است» از نوع PublicationInfo nullable؛ Create/Edit/Delete/Submit مطابق جدول انتقال‌ها باشند.
- [ ] T016 [US1] تعریف ساختار immutable تصمیم و انتشار موردنیاز Aggregate در `src/01.Core/Domain/Posts/ReviewDecision.cs` و `src/01.Core/Domain/Posts/PublicationInfo.cs`؛ قواعد عین مدل: Id/PostId «هر تصمیم متعلق به یک Post»، ContentRevision «نسخهٔ دقیق بررسی‌شده»، ReviewedTitle/ReviewedBody «Snapshot عنوان و متن در زمان تصمیم»، Decision «Approve یا RequestChanges»، ReviewerId «هویت معتبر تصمیم‌گیرنده»، DecidedAt «تولید سمت سرور» به UTC، Reason از نوع string nullable «برای RequestChanges غیرخالی و غیر whitespace»؛ Publication شامل PublisherId، «PublishedAt به UTC و ApprovedReviewId» مرتبط با تأیید معتبر همان Post باشد؛ رفتار تأیید/انتشار در US2 افزوده شود.
- [ ] T017 [US1] تعریف قرارداد حداقلی پست در `src/01.Core/Application/Posts/IPostStore.cs` و پیاده‌سازی نگاشت و store در `src/02.Infra/DataAccess/Posts/PostConfiguration.cs` و `src/02.Infra/DataAccess/Posts/PostStore.cs`؛ Version برابر IsConcurrencyToken، UPDATE مشروط، تصمیم‌ها immutable، Publication متعلق به Post، UTC قابل مقایسه، فیلتر حذف و ایندکس OwnerId/IsDeleted و Status/IsDeleted؛ اتصال Aggregate به `src/02.Infra/DataAccess/BlogDbContext.cs` و ایجاد migration اولیهٔ تمام جداول در `src/02.Infra/DataAccess/Migrations/` با آزمون اعمال روی SQLite خالی. پس از ایجاد و اعمال Migration، آزمون Startup Configuration تعریف‌شده در T004 و Smoke Test مسیر واقعی Provision و Authentication را اجرا کنید؛ خواندن Permission جاری از منبع معتبر سمت سرور تأیید شود، لغو Credential هویت را نامعتبر کند و لغو Permission با Credential همچنان معتبر هیچ Permission عملیاتی باقی نگذارد. این Validation اجرایی T007 تا T010 باید پیش از پذیرش End-to-End User Story 1 انجام شود.
- [ ] T018 [US1] پیاده‌سازی Create/List/Get/Edit/Delete/Submit در `src/01.Core/Application/Posts/AuthorPostService.cs`؛ UserId معتبر و CanWrite فعلی، مالکیت و محدودهٔ خواندن قبل از اعلام وضعیت/Version، اعتبارسنجی و قواعد Domain، خواندن مجوز/Post داخل write session برای mutation و Commit واحد؛ شکست هیچ اثر ذخیره‌شده نداشته باشد.
- [ ] T019 [US1] تعریف DTOهای صریح PostView و ورودی title/body در `src/03.Endpoints/Api/Contracts/PostContracts.cs` و نگاشت lastReview/publication/allowedActions در `src/01.Core/Application/Posts/PostView.cs`؛ lastReview بدون Snapshot داخلی، allowedActions از مجوز جاری و وضعیت و مالکیت محاسبه و صرفاً راهنما باشد؛ JSON camelCase، enum مطابق مدل، زمان UTC و فیلدهای اضافی رد شوند.
- [ ] T020 [US1] اتصال شش مسیر Author در `src/03.Endpoints/Api/Endpoints/AuthorPostEndpoints.cs` و ثبت سرویس‌ها در `src/03.Endpoints/Api/Program.cs`؛ Binding/Authentication/HTTP فقط در Api، If-Match برای هر فرمان پس از ایجاد و Location/ETag مطابق قرارداد؛ آزمون‌های T012 تا T014 را اجرا و اشکال‌های همین داستان را رفع کنید.

**نقطهٔ کنترل**: MVP نویسنده قابل استفاده است؛ هنوز انتشار کامل ارائه نشده است.

## فاز ۴: داستان ۲ — بررسی، تأیید و انتشار ویراستار (P1)

**هدف**: صف، برگشت با دلیل، تأیید محتوای دقیق و انتشار با مجوز فعلی؛ FR-006 تا FR-009 و FR-014 و مسیر ویراستار FR-016.

**آزمون مستقل**: با fixture پست PendingReview، بدون ایجاد پست از API، برگشت/اصلاح/ارسال و تأیید/انتشار را بررسی کنید؛ غیرمالک باید تأیید کند، مالک دو نقشی با تأیید دیگری بتواند منتشر کند.

### آزمون‌ها

- [ ] T021 [P] [US2] نوشتن آزمون‌های Domain در `tests/BlogSpecLab.Tests/Domain/PostReviewTests.cs` برای انتقال RequestChanges/Approve/Publish، دلیل whitespace، Snapshot و revision دقیق، منع خودتأییدی، تأیید معتبر جاری، ابطال با تغییر محتوا، ثبات تصمیم تاریخی و اطلاعات انتشار نخست.
- [ ] T022 [P] [US2] نوشتن آزمون‌های Application در `tests/BlogSpecLab.Tests/Application/EditorPostTests.cs` برای CanReview/CanPublish جاری و IsActive، محدودیت صف PendingReview/Approved، عدم تغییر متن/مالکیت/حذف توسط نقش Editor، برگشت پست خود، رد خودتأییدی، انتشار مالک دو نقشی با تأیید دیگری و اعتبار تأیید تاریخی پس از لغو مجوز تأییدکننده.
- [ ] T023 [P] [US2] نوشتن آزمون قرارداد پنج مسیر Editor در `tests/BlogSpecLab.Tests/Integration/EditorApiTests.cs` برای صف مرتب، جزئیات محدود به PendingReview/Approved، reason و Body ممنوع در approve/publish، ETag/If-Match، وضعیت و code، lastReview و publication، مسیر کامل برگشت تا انتشار و عدم عمومی شدن صرفاً با تأیید؛ مقادیر دقیق، ترتیب ثابت و نبود تکرار allowedActions را برای Editor مالک/غیرمالک و کاربر دو نقشی در وضعیت‌های مربوط و پاسخ انتقال‌ها بررسی کنید؛ خودتأییدی هیچ‌گاه approve مجاز نمایش ندهد، publish فقط با تأیید معتبر محتوای فعلی درج شود و Published حتی در پاسخ Publish تکراری آرایهٔ خالی داشته باشد. بررسی اقدامات ویراستاری در پاسخ Author کاربر دو نقشی و رگرسیون T014 نیز تکمیل شود.

### پیاده‌سازی

- [ ] T024 [US2] افزودن انتقال‌های بررسی و انتشار در `src/01.Core/Domain/Posts/Post.cs` با استفاده از `src/01.Core/Domain/Posts/ReviewDecision.cs` و `src/01.Core/Domain/Posts/PublicationInfo.cs`؛ تأیید فقط آخرین تصمیم جاری Approve با revision و Snapshot یکسان و ReviewerId متفاوت OwnerId؛ زمان سروری و اطلاعات تصمیم ثابت، انتشار فقط Approved معتبر و تکرار انتشار بدون تغییر اطلاعات نخست.
- [ ] T025 [US2] پیاده‌سازی List/Get/RequestChanges/Approve/Publish در `src/01.Core/Application/Posts/EditorPostService.cs`؛ CanReview یا CanPublish فعلی مستقل، مجوز در write transaction، بررسی دسترسی پیش از تعارض، ثبت تصمیم و انتقال در یک Commit، بدون بررسی دوبارهٔ مجوز تاریخی تأییدکننده؛ مالک دارای CanPublish با تأیید معتبر دیگری مجاز باشد.
- [ ] T026 [US2] افزودن ذخیرهٔ تصمیم/Publication اتمی و query صف در `src/02.Infra/DataAccess/Posts/PostStore.cs` و `src/02.Infra/DataAccess/Posts/PostConfiguration.cs`؛ هر شکست rollback کامل دهد، روابط تاریخی با لغو Credential/مجوز باقی بمانند و schema تغییرکرده فقط در صورت نیاز migration جدید در `src/02.Infra/DataAccess/Migrations/` بگیرد.
- [ ] T027 [US2] اتصال پنج مسیر Editor در `src/03.Endpoints/Api/Endpoints/EditorPostEndpoints.cs`، ورودی reason در `src/03.Endpoints/Api/Contracts/ReviewContracts.cs` و تکمیل allowedActions در `src/01.Core/Application/Posts/PostView.cs`؛ صف پست خود Editor را نشان دهد ولی approve ممنوع باشد؛ آزمون‌های T021 تا T023 و رگرسیون US1 اجرا شوند.

**نقطهٔ کنترل**: مسیر مدیریت تا انتشار ثبت‌شده کامل است؛ اثبات جامع رقابت و خواندن عمومی در US3 انجام می‌شود.

## فاز ۵: داستان ۳ — جلوگیری از انتشار غیرمجاز و مشاهدهٔ عمومی (P1)

**هدف**: خواندن عمومی امن و اثبات عدم دور زدن تأیید/مجوز، ثبات Published و رقابت؛ FR-010 تا FR-015 و SC-001/002/003/006.

**آزمون مستقل**: fixture همهٔ وضعیت‌ها و مجوزها؛ فقط Published عمومی، انتشار غیرمجاز بدون تغییر، تکرار بدون انتشار تازه و رقابت کنترل‌شده با دو اتصال مستقل.

### آزمون‌ها

- [ ] T028 [P] [US3] نوشتن آزمون قرارداد دو مسیر عمومی در `tests/BlogSpecLab.Tests/Integration/PublicApiTests.cs`؛ ناشناس فقط Published حذف‌نشده به‌ترتیب Id، DTO فقط id/title/body/publishedAt، 404 یکسان برای ناموجود/خصوصی/حذف‌شده و عدم افشای مالک، دلیل، تصمیم و Version.
- [ ] T029 [P] [US3] نوشتن ماتریس امنیت HTTP در `tests/BlogSpecLab.Tests/Integration/PublicationSecurityTests.cs`؛ Credential غایب/جعلی/منقضی/لغوشده، حساب غیرفعال، مجوز لغوشده با token معتبر، header/Body جعلی، انتشار مستقیم وضعیت‌های غیر Approved، تأیید قدیمی، خودتأییدی و ویرایش/حذف Published توسط مالک/Editor/دو نقش؛ پاسخ و DB بدون اثر را بررسی کنید.
- [ ] T030 [P] [US3] نوشتن آزمون‌های رقابت provider در `tests/BlogSpecLab.Tests/Integration/PostConcurrencyTests.cs` با دو DbContext/connection روی SQLite فایل‌محور؛ Version اولیه مشترک و ترتیب کنترل‌شدهٔ هر دو جهت edit/approve، edit/publish، delete/publish و دو تصمیم بررسی، بدون sleep یا retry تجاری؛ تعارض 412/رد حالت مجاز، نبود تصمیم/Publication یتیم و حفظ تأیید معتبر را بسنجید؛ رقابت مشروط UPDATE را در store و ترتیب‌های قفل نوشتن را در مسیر واقعی جدا بررسی کنید.
- [ ] T031 [P] [US3] نوشتن آزمون‌های تکرار و لغو دسترسی در `tests/BlogSpecLab.Tests/Integration/PublicationAtomicityTests.cs`؛ دو publish هم‌زمان و publish با Version قدیمی، همان زمان/Publisher/Version نخست؛ revoke اپراتوری در رقابت با تراکنش حساس در هر دو ترتیب؛ قفل پایدار 503 بدون تغییر جزئی و شکست Commit بدون دادهٔ یتیم؛ آزمون قطعی با کنترل ترتیب و بدون sleep.

### پیاده‌سازی و سخت‌کردن رفتارهای مصوب

- [ ] T032 [US3] پیاده‌سازی query عمومی فقط Published حذف‌نشده در `src/01.Core/Application/Posts/PublicPostService.cs` و `src/02.Infra/DataAccess/Posts/PublicPostReader.cs` با DTO حداقلی در `src/01.Core/Application/Posts/PublicPostView.cs`؛ هیچ دادهٔ مدیریتی یا Snapshot تاریخی برنگردانید.
- [ ] T033 [US3] اتصال GET `/api/posts` و GET `/api/posts/{id}` در `src/03.Endpoints/Api/Endpoints/PublicPostEndpoints.cs` و `src/03.Endpoints/Api/Program.cs` مطابق PublicPostView و 404 یکسان؛ آزمون T028 را اجرا کنید.
- [ ] T034 [US3] تکمیل مسیر تعارض و استثنای محدود publish تکراری در `src/01.Core/Application/Posts/EditorPostService.cs`، `src/02.Infra/DataAccess/Persistence/SqliteWriteSession.cs` و `src/03.Endpoints/Api/Http/ProblemResults.cs`؛ پس از هویت/مجوز جاری و If-Match با قالب معتبر، Published معتبر حتی با Version قدیمی 200 بدون Commit؛ بازندهٔ دو publish فقط پس از بازخوانی و کنترل دوبارهٔ مجوز انتشار نخست را برگرداند، سایر تعارض‌ها 412؛ currentStatus/currentVersion فقط برای کاربر مجاز و بدون محتوا و پیام بازخوانی فارسی.
- [ ] T035 [US3] اجرای آزمون‌های T029 تا T031 و رفع هر نقض مصوب در `src/01.Core/Domain/Posts/Post.cs`، `src/01.Core/Application/Posts/AuthorPostService.cs`، `src/01.Core/Application/Posts/EditorPostService.cs` و `src/02.Infra/DataAccess/Posts/PostStore.cs`؛ همهٔ آزمون‌های US1/US2/US3 نیز عبور کنند و مجوز تاریخی تأییدکننده با مجوز فعلی منتشرکننده اشتباه نشود.

**نقطهٔ کنترل**: سه داستان با پاسخ HTTP و وضعیت واقعی DB قابل اثبات‌اند؛ پذیرش انسانی هنوز لازم است.

## فاز ۶: تکمیل و بررسی سراسری

- [ ] T036 تولید OpenAPI با Microsoft.AspNetCore.OpenApi در `src/03.Endpoints/Api/Program.cs` و تکمیل metadata مسیرها در `src/03.Endpoints/Api/Endpoints/AuthorPostEndpoints.cs`، `src/03.Endpoints/Api/Endpoints/EditorPostEndpoints.cs` و `src/03.Endpoints/Api/Endpoints/PublicPostEndpoints.cs`؛ DTO، Bearer، If-Match/ETag، ProblemDetails و status/codeها با `specs/001-blog-post-management/contracts/http-api.md` تطبیق داده شوند؛ package رابط Swagger اضافه نشود.
- [ ] T037 به‌روزرسانی فارسی `specs/001-blog-post-management/quickstart.md` با دستورهای واقعاً اجرا و بررسی‌شدهٔ restore/tool restore/migration/build/test/run، مسیر SQLite/HTTPS و provision/revoke، مدیریت secret، مثال تمام مسیرهای اصلی، If-Match، status/code و allowedActions؛ راهنما را با SQLite تازه و نقش‌های مستقل از ابتدا تا انتشار و شکست‌های مصوب اجرا کنید.
- [ ] T038 پس از ایجاد پروژه‌ها و اجرای موفق، بخش ساخت و آزمون `AGENTS.md` را با دستورهای معتبر و کوتاه به‌روز کنید؛ بررسی نهایی Project Referenceها، نبود وابستگی اضافی/منطق کسب‌وکار در Api یا DataAccess، نبود secret در فایل‌ها/log و اجرای build/test واقعی را در `specs/001-blog-post-management/validation.md` ثبت کنید.
- [ ] T039 ثبت شواهد SC-001، SC-002، SC-003 و SC-006 در `specs/001-blog-post-management/validation.md` با نگاشت سناریو به آزمون و نتیجهٔ HTTP/DB بدون token/متن خصوصی؛ خروجی OpenAPI را با قرارداد بررسی و محدودیت‌های اجرای محلی و هر مشکل باقی‌مانده را صریح ثبت کنید.
- [ ] T040 آماده‌سازی و برگزاری پذیرش انسانی مطابق `specs/001-blog-post-management/quickstart.md` و ثبت نتیجه در `specs/001-blog-post-management/acceptance.md`؛ حداقل پنج Author و پنج Editor، وظیفهٔ نقش بدون کمک برای SC-004 و تشخیص وضعیت/اقدام بعدی برای SC-005، آستانهٔ حداقل ۹۰٪ برای هر معیار، تعداد شرکت‌کننده/موفق‌ها/خطای فهم/شرایط اجرا؛ نبود شرکت‌کنندگان به‌عنوان کار باقی‌مانده ثبت شود و موفقیت جعل نشود.

## وابستگی‌ها و ترتیب اجرا

```text
فاز ۱ (T001–T004) → فاز ۲ (T005–T011)
  → US1 (T012–T020) → US2 (T021–T027) → US3 (T028–T035)
  → تکمیل (T036–T040)
```

- T001 پیش از T002؛ T003 و T004 پس از T002 مستقل‌اند. T004 فقط زیرساخت fixture می‌سازد؛ ایجاد schema و تست رفتاری منتظر T017 است.
- T005 و T006 مستقل؛ T007 پس از هر دو، T008 پس از T007، T009 و T010 پس از T008، T011 پس از قرارداد نتیجهٔ T006 و wiring T010.
- T017 پیش‌نیاز Validation اجرایی T007 تا T010 در برابر Database واقعی است؛ کامل بودن کد فاز ۲ با قابل اجرا بودن End-to-End آن یکسان نیست. بخش Host-level آزمون T004 در فاز ۱ فقط آماده می‌شود و اجرای آن به T010 و T017 وابسته است؛ این تأخیر مانع تکمیل کد فاز ۲ نیست. پس از T017، Smoke Test مربوط به Startup Configuration، Provision، Authentication و Permission Reading اجرا شود و سپس پذیرش End-to-End US1 ادامه یابد. آزمون Runtime Storage و 503 مستقل در T031 باقی است.
- آزمون‌های هر داستان مستقل‌اند و پیش از رفتار نوشته می‌شوند؛ برای اجرای شکست رفتاری، فقط اسکلت حداقلی نوع‌ها/امضاهای موردنیاز بسازید. سپس وظایف پیاده‌سازی همان داستان به ترتیب شناسه اجرا شوند. T015 با نوع‌های T016 تکمیل و قابل ساخت می‌شود.
- US2 از Aggregate، store، schema و مسیرهای اصلاح/ارسال US1 استفاده می‌کند؛ مستقل بودن آزمون به معنای مستقل بودن کد از US1 نیست. fixture PendingReview نیاز به ایجاد پست از API را حذف می‌کند.
- US3 از انتشار و دادهٔ US2 استفاده می‌کند؛ آزمون‌های عمومی با fixture Published قابل اجرای مستقل‌اند. US2 و US3 به‌دلیل فایل‌های مشترک Post/store/service به‌صورت کامل موازی نیستند.
- T036 سپس T037؛ T038 و T039 پس از عبور آزمون‌ها و راهنمای T037؛ T040 پس از آماده بودن API و راهنما. تکمیل پذیرش وابسته به مشارکت انسانی واقعی است.

## فرصت‌ها و نمونه‌های اجرای موازی

این بخش امکان تقسیم کار در اجرای آینده را بیان می‌کند؛ مجوز اجرای خودکار عامل‌ها یا شروع Implement نیست.

| گروه | نمونهٔ وظایف قابل اجرای هم‌زمان | شرط آغاز |
| --- | --- | --- |
| آماده‌سازی | T003 و T004 | T002 کامل |
| پایه | T005 و T006 | فاز ۱ کامل |
| US1 | T012 (Domain)، T013 (Application)، T014 (HTTP) | فاز ۲ کامل؛ فایل‌های آزمون مستقل |
| US2 | T021 (Domain)، T022 (Application)، T023 (HTTP) | US1 کامل؛ fixture و قراردادهای آزمون مشترک ثابت |
| US3 | T028 (عمومی)، T029 (امنیت)، T030 (رقابت)، T031 (اتمیسیته) | US2 کامل؛ هر آزمون DB موقت مستقل |

تغییر هم‌زمان fixture، Post، Program، PostView یا PostStore در این گروه‌ها مجاز نیست؛ نیاز به تغییر فایل مشترک ابتدا به‌صورت ترتیبی حل شود. هیچ وظیفهٔ پیاده‌سازی دارای `[P]` نیست، زیرا این نسخه فایل‌های مشترک و وابستگی‌های واقعی دارد.

## راهبرد پیاده‌سازی و تحویل تدریجی

۱. آماده‌سازی و پایه، سپس US1؛ با دو نویسنده معیار مستقل را تأیید کنید و توقف برای بازبینی داشته باشید. این **MVP نویسنده** است و انتشار کنترل‌شدهٔ کامل محسوب نمی‌شود.

۲. US2 را اضافه کنید و مسیر برگشت، تأیید غیرمالک و انتشار با مجوز فعلی را با fixture و مسیر کامل بسنجید.

۳. US3 را اضافه کنید؛ چون هر سه داستان P1 هستند و عدم انتشار غیرمجاز نیاز اصلی است، **حداقل نسخهٔ کامل قابل پذیرش محصول شامل هر سه داستان** است.

۴. OpenAPI، راهنمای واقعی و شواهد خودکار را تکمیل کنید؛ SC-004 و SC-005 فقط با پذیرش انسانی اثبات می‌شوند. عبور build/test به‌تنهایی پذیرش نهایی نیست. بدون دستور کاربر به قابلیت یا مرحلهٔ SDD بعدی نروید.

## کنترل پوشش

| نیازمندی | وظایف اصلی |
| --- | --- |
| FR-001، FR-013 | T005–T011، T013–T014، T018، T022، T025، T029 |
| FR-002 تا FR-005 | T012–T020 |
| FR-006، FR-007 | T021–T027 |
| FR-008، FR-009 | T016، T021–T027، T029–T031، T034–T035 |
| FR-010، FR-012 | T008، T011، T013–T014، T030–T031، T034–T035 |
| FR-011 | T028، T032–T033 |
| FR-014، FR-015 | T012، T021–T022، T024–T025، T029، T035 |
| FR-016 | T020، T027، T033، T036–T037، T040 |
| SC-001، SC-002، SC-003، SC-006 | آزمون‌های داستان‌ها، T035، T038–T039 |
| SC-004، SC-005 | T037، T040 |

**جمع**: ۴۰ وظیفه؛ آماده‌سازی ۴، پایه ۷، US1 تعداد ۹، US2 تعداد ۷، US3 تعداد ۸، تکمیل ۵؛ ۱۴ وظیفه دارای `[P]` در پنج گروه مشخص.
