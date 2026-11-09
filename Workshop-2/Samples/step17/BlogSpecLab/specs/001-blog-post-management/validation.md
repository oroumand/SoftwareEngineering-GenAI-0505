# شواهد اعتبارسنجی فاز ۶

تاریخ: 2026-10-12؛ Feature: `001-blog-post-management`. محدودهٔ اجرا T036 تا T039 و آماده‌سازی T040 است. پذیرش انسانی و پذیرش نهایی قابلیت هنوز انجام نشده‌اند. Spec، Constitution 1.0.0 و Technical Baseline تغییر نکردند؛ Plan/Research اسناد تاریخی طراحی‌اند و عبارت‌های مربوط به زمان تولیدشان گزارش وضعیت امروز محسوب نمی‌شوند.

## محیط و اجرای نهایی

Windows، PowerShell 7.6.6 روی .NET Runtime 10.0.12، SDK انتخاب‌شدهٔ 10.0.401؛ `dotnet --list-sdks` نسخه‌های 9.0.312، 10.0.200 و 10.0.401 را نشان داد. همهٔ پنج پروژه net10.0 هستند. `dotnet dev-certs https --check --trust` وجود گواهی توسعهٔ معتبر و trusted برای localhost را تأیید کرد؛ TLS دور زده نشد.

| فرمان واقعی از ریشه | نتیجه |
| --- | --- |
| `dotnet clean BlogSpecLab.slnx` | موفق، خروجی‌های ساخت پاک شدند |
| `dotnet restore BlogSpecLab.slnx` | موفق؛ restore همهٔ پروژه‌ها up-to-date بود |
| `dotnet tool restore` | موفق؛ dotnet-ef 10.0.12 |
| `dotnet build BlogSpecLab.slnx --no-restore` | موفق؛ ۰ Warning، ۰ Error |
| `dotnet test BlogSpecLab.slnx --no-build --no-restore --logger 'trx;LogFileName=phase6.trx'` | 178 Passed، 0 Failed، 0 Skipped؛ 178 Total |
| `dotnet ef database update --project src/02.Infra/DataAccess --startup-project src/03.Endpoints/Api --no-build` | موفق روی SQLite تازه؛ InitialBlogSchema |
| `dotnet run --no-build --no-launch-profile --project src/03.Endpoints/Api -- access provision 2026-12-31T23:59:59Z` | موفق؛ ۱۱ حساب و تحویل رمزگذاری‌شده خارج مخزن |
| `dotnet run --no-build --no-launch-profile --project src/03.Endpoints/Api` | Kestrel واقعی در https://localhost:7180 با تنظیمات محیط راه افتاد |
| `demo.ps1 -ProtectedFile $bundle.FullName` | مسیر اصلی و شکست‌های ثبت‌شدهٔ quickstart موفق؛ دو اجرا روی DBهای تازه، اجرای دوم با Build نهایی |
| `Invoke-RestMethod https://localhost:7180/openapi/v1.json` | سند واقعی با ۱۰ مسیر و ۱۳ عملیات دریافت شد |

خروجی TRX نهایی در `tests/BlogSpecLab.Tests/TestResults/phase6.trx` موجود است و طبق .gitignore خروجی آزمون محسوب می‌شود. Counters فایل: total/executed/passed برابر 178؛ failed/error/timeout/aborted/notExecuted برابر صفر. clean بازسازی بدون استفاده از DLLهای قبلی را بررسی کرد؛ cache سراسری NuGet پاک نشد و نصب از ماشین بدون SDK یا شبکهٔ متفاوت آزمایش نشده است. شکست‌های میانی تنظیم metadata پیش از اصلاح، نتیجهٔ نهایی محسوب نمی‌شوند.

| Suite | Passed |
| --- | ---: |
| Domain.PostAuthorTests | 28 |
| Domain.PostReviewTests | 11 |
| Application.AuthorPostTests | 11 |
| Application.EditorPostTests | 8 |
| Integration.AuthorApiTests | 25 |
| Integration.EditorApiTests | 6 |
| Integration.PublicApiTests | 2 |
| Integration.FoundationContractTests | 25 |
| Integration.StartupConfigurationTests | 11 |
| Integration.PostPersistenceTests | 2 |
| Integration.PostConcurrencyTests | 16 |
| Integration.PublicationSecurityTests | 19 |
| Integration.PublicationAtomicityTests | 13 |
| Integration.OpenApiContractTests | 1 |

تمام Suiteها Failed/Skipped صفر دارند. عدد coverage ابزار اندازه‌گیری نشده و هیچ درصد line/branch coverage ادعا نمی‌شود؛ شاهد پوشش، سناریوها و assertionهای رفتاری زیر است.

## تطبیق OpenAPI و HTTP — T036

مرجع رفتار Spec و قرارداد تأییدشده است؛ metadata باید رفتار آن‌ها را توصیف کند. اختلاف اولیه، فقدان AddOpenApi/MapOpenApi و metadata DTO/response بود؛ در Api اصلاح شد، بدون تغییر Provider، قواعد کسب‌وکار یا افزودن Package. متن آغاز قرارداد که API را پیاده‌سازی‌نشده معرفی می‌کرد پس از گزارش اصلاح شد؛ تعریف مسیر/DTO/code تغییر نکرد. خطای غیرمنتظرهٔ 500 با `unexpected_error` از قبل در mapper وجود داشت و اکنون در OpenAPI مستند است.

`OpenApiContractTests.GeneratedDocumentDescribesAllContractRoutesSecurityBodiesResponsesAndHeaders` سند واقعی تولیدشده را از HTTP دریافت می‌کند و مجموعهٔ دقیق روش/مسیرهای قرارداد، 201/200/204، Bearer فقط برای مدیریت، If-Match الزامی mutation، ETag جزئیات/نتایج، WWW-Authenticate، Cache-Control، schema عمومی، allowedActions و code و رد فیلد اضافی Request را بررسی می‌کند. این آزمون بدون Schema/Provision DB نیز تولید سند را تأیید می‌کند.

- Author: شش عملیات؛ Create/PUT فقط PostContentRequest شامل title/body؛ Create دارای Location/ETag و 201، حذف 204 بدون Body؛ سایر موفقیت‌ها 200.
- Editor: پنج عملیات؛ Request Changes فقط reason؛ Approve/Publish بدون Body؛ پاسخ PostView. خودتأییدی code مستقل و Publish تکراری بدون Commit مستند شدند.
- Public: دو عملیات ناشناس؛ PublicPostView دقیقاً id/title/body/publishedAt، بدون ownerId/version/lastReview/publication/allowedActions.
- PostView و ReviewView/PublicationView از DTOهای واقعی تولید شدند؛ زمان‌های UTC و Version UUID. ترتیب/شرایط allowedActions در توضیح schema و آزمون‌های رفتاری هر وضعیت بررسی شدند، از جمله کاربر دو نقشی و آرایهٔ خالی Published.
- If-Match یک UUID داخل کوتیشن؛ نبود 428، قالب نامعتبر 400، Version قدیمی 412. header ETag و Location در سند و پاسخ واقعی وجود دارند.
- ProblemDetails دارای code الزامی و extensionهای اختیاری errors/currentStatus/currentVersion است؛ status و codeهای 400/401/403/404/409/412/428/503/500 مستند و در FoundationContractTests و آزمون‌های مسیر بررسی شده‌اند.
- مدیریت و خطاها no-store؛ 401 دارای WWW-Authenticate: Bearer؛ قرارداد content-typeها و JSON camelCase حفظ شده است.

`AuthorApiTests`، `EditorApiTests`، `PublicApiTests` و `PublicationSecurityTests` به جای اتکا به سند، responseهای واقعی و DB را بررسی می‌کنند. وضعیت خارج از محدودهٔ مشاهدهٔ Editor، مثلاً Draft، 404 است؛ PendingReview با درخواست Publish و Version جاری 409 invalid_state دارد. Demo خطاهای مهم را روی Kestrel و Credential واقعی دوباره بررسی کرد. UI یا Swagger package اضافه نشد. تولید سند با transformer استاندارد مطابق [مستندات رسمی ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0) انجام شد.

## اجرای quickstart — T037

راهنما اکنون فرمان‌های اجراشدهٔ SDK، HTTPS، restore/tool restore، migration/build/test/run، مسیر مطلق SQLite، Provision و لغو را دارد. `demo.ps1` برای خواندن امن بستهٔ DPAPI و اجرای مثال‌ها افزوده شد؛ dependency/پروژهٔ جدید ایجاد نشد.

با نقش‌های مستقل Author و Editor مسیر Create → Edit → Submit → Request Changes → Edit → Submit again → Approve → Publish اجرا شد. فهرست/جزئیات هر نقش و Public Read، ETag، Location، allowedActions و نمایش دلیل برگشت بررسی شدند. انتشار مجدد با Version قدیمی اطلاعات انتشار و Version نخست را حفظ کرد. Unauthorized=401، خودتأییدی=403، stale=412، mutation Published=409، private/deleted public access=404، wildcard=400 و نبود If-Match=428 واقعی بودند. پس از لغو Permission همان Credential پاسخ 403 داد؛ پس از لغو Credential پاسخ 401 شد. متن خصوصی و token در شواهد یا Log ثبت نشدند.

این Demo خودکار برای قابلیت تکرار راهنماست؛ اثبات فهم انسان محسوب نمی‌شود. زمان انقضای نمونه در روز اجرا آینده بود؛ اجرای بعدی نیازمند تاریخ آینده و batch جدید است.

## معماری، وابستگی و امنیت — T038

فایل‌های csproj و کد واقعی با Constitution و Technical Baseline بازبینی شدند:

| پروژه | Project Reference واقعی | نتیجه |
| --- | --- | --- |
| Domain | ندارد؛ Package نیز ندارد | مستقل |
| Application | Domain؛ Package ندارد | بدون Infra/Endpoints/EF/DbContext/IQueryable |
| DataAccess | Application و Domain | EF/SQLite، نگاشت، query، transaction، credential persistence |
| Api | Application و DataAccess | HTTP Binding/Authentication/metadata/نتیجه/Composition |
| Tests | چهار پروژهٔ اجرایی | Domain/Application بدون سرویس بیرونی؛ HTTP با handler واقعی و SQLite موقت |

چرخه یا Project اضافه وجود ندارد. بررسی XML رفرنس‌ها و جست‌وجوی source Core برای وابستگی EF/ASP.NET، DbContext و IQueryable نتیجهٔ خلاف استقلال Core نداشت. انتقال‌های Create/Edit/Delete/Submit/Review/Publish در Post و Permission/مالکیت/هماهنگی در سرویس‌های Application هستند؛ Endpointها آن‌ها را دوباره پیاده نمی‌کنند. DataAccess query و projection/filer و کنترل persistence دارد؛ guard حفظ ReviewDecision تاریخی در DbContext از قبل موجود است و rule انتقال وضعیت یا مجوز عملیات را جای Application/Domain اجرا نمی‌کند. ابزار AccessProvisioner نگاشت اپراتوری نقش مصوب و ذخیره/لغو را انجام می‌دهد.

Packageها همان EF Core SQLite/Design/OpenAPI 10.0.12 و xUnit/Test SDK/Mvc.Testing موجودند؛ هیچ AI، MCP، RAG، Qdrant، SDK مدل، Provider جدید یا پروژهٔ مربوط افزوده نشده است. جست‌وجوی csprojها وابستگی ناخواسته‌ای نشان نداد. .gitignore دارای bin/obj/packages/TestResults، فایل‌های DB و جانبی، فایل‌های محلی Secret و key است؛ Docker/Node/ESLint/Prettier/Terraform/Helm وجود ندارد و ignore نامربوط ساخته نشد. پوشه .git ندارد؛ git diff/status قابل استفاده نبود و Git ایجاد نشد.

بازبینی appsettings، handler/verifier، AccessCommands، fixtureها و الگوهای credential ثابت در source/docs نتیجه‌ای از secret ثابت نداشت. token واقعی از RNG گرفته می‌شود، SHA-256 در DB و بستهٔ رمزگذاری‌شده خارج مخزن تحویل می‌شود. Demo بسته را در حافظه می‌خواند و فقط status/code/مسیر را نمایش می‌دهد؛ مقدار Credential/Connection String در شواهد ثبت نشده است. logging EF خاموش است؛ mapper خطای داخلی را عمومی می‌کند. این بررسی محدود به فایل‌ها و Logهای همین اجرای محلی است، نه ممیزی جامع امنیتی یا تضمین نبود Secret در هر محیط خارجی.

AGENTS.md با tool restore و تصریح .NET 10 و Migration/Provision مستقل به‌روز شد؛ جزئیات Feature وارد آن نشد.

## معیارهای موفقیت خودکار — T039

نتایج زیر همگی در Suite نهایی Passed هستند؛ ۱۰۰٪ به سناریوهای مشخص و آزموده‌شده اشاره دارد، نه اثبات ریاضی تمام ورودی‌ها یا تضمین Production.

| معیار | تعریف | شاهد آزمون و نتیجه | شاهد HTTP / DB |
| --- | --- | --- | --- |
| SC-001 | انتشار بدون مجوز/تأیید/تأیید محتوای قدیمی هیچ محتوای تازه‌ای عمومی نکند | PublicationSecurityTests: CredentialAndCurrentPermissionMatrixRejectsWithoutLeakOrMutation، DirectPublishCannotBypassStateOrApproval، InvalidApprovalCannotPublish؛ EditorApiTests.ActualEditInvalidatesApprovalAndPublishCannotUseOldDecision؛ PostConcurrencyTests و PublicationAtomicityTests همگی Passed | 401/403/404/409/412 برحسب دسترسی/وضعیت؛ عدم تغییر Post/Review/Publication؛ تأیید قبلی پس از Edit نامعتبر، Publication null و Draft؛ خطای ذخیره و lock 503 بدون تغییر جزئی |
| SC-002 | Author نتواند پست خصوصی دیگران را مشاهده یا تغییر دهد | AuthorApiTests.IdentityPermissionOwnershipAndVersionErrorsDoNotLeakOrMutate و ListsAreSortedByIdAndContainOnlyOwnNonDeletedIncludingPublished؛ PublicationSecurityTests.ForgedHeadersAndBodiesDoNotCreatePermissionsOwnershipOrState؛ Passed | 404 قبل از افشای تعارض/محتوا، یا 403 برای نقش نامجاز؛ DB ثابت، فهرست فقط مالک. Demo Author دوم روی Draft=404 |
| SC-003 | تمام مسیرهای گردش با نتیجهٔ درست و Public فقط Published | AuthorApiTests.SixRoutesProvideIndependentAuthorWorkflow؛ EditorApiTests.IndependentReturnResubmitApprovePublishWorkflowAndHistoricalRevocation؛ PublicApiTests.AnonymousListAndDetailExposeOnlyPublishedProjectionSortedById و PrivateDeletedAndMissingHaveIdenticalProblemsApartFromRequestedInstance؛ Passed | 201/200/204 و انتقال‌های صحیح؛ دو تصمیم تاریخی پس از برگشت/تأیید، ApprovedReviewId مرتبط و زمان UTC؛ Public DTO چهار فیلد، private/deleted/missing=404. Demo از ایجاد تا انتشار و خواندن عمومی موفق |
| SC-006 | خودتأییدی و ویرایش/حذف Published رد شوند و داده ثابت بماند | PublicationSecurityTests.SelfApprovalIsForbiddenForDualRoleOwnerWithoutMutation و PublishedIsImmutableForEveryRole؛ Domain.PostAuthorTests.PublishedRejectsAllAuthorChangesIncludingIdenticalEdit؛ Passed | 403 self_approval_forbidden؛ 409 published_post_immutable با Version جاری؛ assertion ثبات وضعیت، محتوا، Version، تصمیم و انتشار. Demo خودتأییدی و PUT/DELETE Published همین نتیجه‌ها را داد |

PublicationAtomicityTests شامل یک انتشار نخست در دو Publish هم‌زمان، بازخوانی همراه Permission تازه، revoke و عملیات حساس در هر دو ترتیب، lock ماندگار و rollback پس از خطای Commit واقعی است. PostConcurrencyTests هر دو ترتیب edit/approve، edit/publish، delete/publish و دو تصمیم را با connectionهای مستقل بررسی می‌کند؛ هیچ retry تجاری و تصمیم/انتشار یتیم پذیرفته نمی‌شود.

**سبز بودن Testها جایگزین SC-004 و SC-005 نیست.** هیچ شرکت‌کنندهٔ واقعی در این اجرا ارزیابی نشد؛ درصد یا نتیجهٔ این معیارها محاسبه نشده است. [acceptance.md](acceptance.md) آمادهٔ اجرا است و T040 باز است.

## محدودیت‌ها و وضعیت نهایی

SQLite فایل‌محور تک‌نمونه‌ای و یک نویسندهٔ هم‌زمان، Windows DPAPI همان حساب، Runtime مشخص Demo و گواهی توسعهٔ localhost حدود شواهد هستند. Schema باید صریح آماده شود؛ خطاهای runtime DB با 503 مدیریت می‌شوند. نصب مستقل از NuGet cache، Production، ظرفیت/SLA و ابزار HTTP انسانی متفاوت آزمایش نشده‌اند. Providerهای نهایی DB/Auth تعیین نشده‌اند. شواهد پذیرش انسانی باقی مانده‌اند؛ هیچ نتیجه‌ای ساخته نشده است.

T036، T037، T038 و T039 تکمیل و قابل علامت‌گذاری‌اند. T040 فقط آماده‌سازی شده و `[ ]` باقی می‌ماند. `.specify/extensions.yml` در بررسی پیش و پس از اجرا وجود نداشت؛ هیچ hook ثبت‌شده‌ای برای dispatch وجود ندارد. هیچ Feature یا مرحلهٔ بعدی، AI/MCP/RAG/Qdrant/Agent شروع نشد.