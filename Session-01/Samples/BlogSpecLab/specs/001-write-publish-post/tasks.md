# فهرست وظایف: نگارش و انتشار پست وبلاگ

**ورودی**: مصنوعات طراحی در `specs/001-write-publish-post/`

**پیش‌نیازها**: `plan.md`، `spec.md`، `research.md`، `data-model.md`،
`contracts/openapi.yaml` و `quickstart.md`

**آزمون‌ها**: طبق Constitution، آزمون‌های Domain، Application، Architecture و API
Integration بخشی از تحویل قابلیت هستند. هر آزمون باید پیش از پیاده‌سازی رفتار متناظر
نوشته و ابتدا ناموفق مشاهده شود.

**سازمان‌دهی**: وظایف بر پایهٔ داستان کاربر گروه‌بندی شده‌اند. هر داستان پس از تکمیل
Foundation با دادهٔ آزمون مناسب مستقل قابل پیاده‌سازی و آزمون است، حتی اگر در جریان واقعی
محصول از Postهای ایجادشده یا منتشرشده استفاده کند.

## قالب وظایف

- `[P]` یعنی وظیفه پس از تکمیل وابستگی‌های اعلام‌شده و در فایل‌های جداگانه قابل انجام
  هم‌زمان است.
- `[USn]` وظیفه را به داستان کاربر متناظر وصل می‌کند.
- معیار پایان هر وظیفه در انتهای همان خط و پس از «معیار پایان:» آمده است.

## Phase 1: آماده‌سازی Solution و ساختار مشترک

**هدف**: ایجاد ساختار چهارلایهٔ .NET 10 و پروژه‌های آزمون، بدون نقض جهت وابستگی Onion.

- [X] T001 ایجاد `global.json`، `Directory.Build.props` و `BlogSpecLab.slnx` در ریشهٔ Repository با SDK سازگار با .NET 10 و Target Framework `net10.0`؛ معیار پایان: همهٔ پروژه‌های بعدی از تنظیم مشترک استفاده می‌کنند.
- [X] T002 ایجاد پروژه‌های Production در `src/BlogSpecLab.Domain/BlogSpecLab.Domain.csproj`، `src/BlogSpecLab.Application/BlogSpecLab.Application.csproj`، `src/BlogSpecLab.Infrastructure/BlogSpecLab.Infrastructure.csproj` و `src/BlogSpecLab.Api/BlogSpecLab.Api.csproj` با Referenceهای مجاز Onion؛ معیار پایان: Domain بدون Reference، Application فقط به Domain، Infrastructure به Application و Domain، و API به Application و Infrastructure Reference دارد.
- [X] T003 ایجاد پروژه‌های آزمون در `tests/BlogSpecLab.Domain.Tests/BlogSpecLab.Domain.Tests.csproj`، `tests/BlogSpecLab.Application.Tests/BlogSpecLab.Application.Tests.csproj`، `tests/BlogSpecLab.Architecture.Tests/BlogSpecLab.Architecture.Tests.csproj` و `tests/BlogSpecLab.Api.IntegrationTests/BlogSpecLab.Api.IntegrationTests.csproj`؛ معیار پایان: Domain.Tests فقط به Domain وابسته است؛ Application.Tests به Application و وابستگی انتقالی Domain؛ Api.IntegrationTests به API و ابزارهای Integration؛ Architecture.Tests به اسمبلی‌های Domain، Application، Infrastructure و API برای کنترل جهت وابستگی وابسته است؛ و هیچ پروژهٔ Production به پروژهٔ آزمون Reference ندارد.
- [X] T004 افزودن Package Referenceهای انتخاب‌شده در `src/BlogSpecLab.Infrastructure/BlogSpecLab.Infrastructure.csproj`، `src/BlogSpecLab.Api/BlogSpecLab.Api.csproj` و فایل‌های `.csproj` آزمون با نسخه‌های سازگار با `net10.0`؛ معیار پایان: EF Core/Npgsql، xUnit v3، ArchUnitNET، WebApplicationFactory و Testcontainers فقط در لایه‌های مجاز قرار دارند و Domain Package خارجی ندارد.
- [X] T005 افزودن همهٔ پروژه‌های `src/**` و `tests/**` به `BlogSpecLab.slnx`؛ معیار پایان: `dotnet build BlogSpecLab.slnx` همهٔ پروژه‌ها را کشف می‌کند.

---

## Phase 2: Foundation مشترک و مسدودکننده

**هدف**: ایجاد Aggregate غنی `Post`، Portهای داخلی، Persistence، مرز HTTP و حفاظت معماری؛
هیچ داستانی پیش از اتمام این Phase شروع نمی‌شود.

- [X] T006 ایجاد اسکلت حداقلی و قابل‌کامپایل `PostId`، `AuthorId`، `PostTitle`، `PostContent`، `PostStatus`، `Post` و خطاهای Domain در `src/BlogSpecLab.Domain/Posts/ValueObjects/`، `src/BlogSpecLab.Domain/Posts/Entities/Post.cs` و `src/BlogSpecLab.Domain/Posts/Errors/`؛ معیار پایان: فقط نوع‌ها و امضای عمومی `CreateDraft`، `EditDraft` و `Publish` وجود دارند تا آزمون کامپایل شود و هیچ Validation، مالکیت یا منطق گذار وضعیت پیاده‌سازی نشده است.
- [X] T007 نوشتن آزمون Invariantهای Aggregate در `tests/BlogSpecLab.Domain.Tests/Posts/PostTests.cs` و اجرای آن پیش از رفتار Domain؛ معیار پایان: مسیر معتبر و رد حالت نامعتبر برای ایجاد، مالکیت، ویرایش، انتشار و Post منتشرشده پوشش دارد و آزمون‌ها ابتدا در برابر اسکلت T006 قرمز مشاهده می‌شوند.
- [X] T008 پیاده‌سازی رفتار Aggregate و Value Objectهای Domain در `src/BlogSpecLab.Domain/Posts/Entities/Post.cs`، `src/BlogSpecLab.Domain/Posts/ValueObjects/` و `src/BlogSpecLab.Domain/Posts/Errors/`؛ معیار پایان: آزمون‌های قرمز T007 سبز می‌شوند و مالکیت ثابت، گذار تنها `Draft → Published`، Validation عنوان/متن و منع ویرایش/انتشار دوباره داخل Domain محافظت می‌شوند.
- [X] T009 تعریف قراردادهای Application شامل `IPostRepository`، `IUnitOfWork`، `IClock`، `PostVersion` و نتیجه‌های کنترل‌شده در `src/BlogSpecLab.Application/Posts/Ports/`؛ معیار پایان: Application بدون Reference به EF Core، HTTP یا نوع Concrete، خواندن/نوشتن Post و Version opaque را بیان می‌کند.
- [X] T010 پیاده‌سازی DbContext، Mapping و Migrationهای پایه در `src/BlogSpecLab.Infrastructure/Posts/Persistence/` و `src/BlogSpecLab.Infrastructure/Migrations/`؛ معیار پایان: جدول `posts` شامل وضعیت، زمان انتشار، `version_token` Shadow و `xmin` Shadow Concurrency Token است و Domain از آن‌ها آگاه نیست.
- [X] T011 پیاده‌سازی Repository، Unit of Work و Clock در `src/BlogSpecLab.Infrastructure/Posts/Repositories/` و `src/BlogSpecLab.Infrastructure/Posts/Time/`; معیار پایان: Handler/Application پس از کنترل قابل‌مشاهده‌بودن، مالکیت و وضعیت Domain، Aggregate مجازِ بارگذاری‌شده و `PostVersion` opaque را به Repository می‌دهد؛ Repository فقط تطابق Version Token همان Aggregate را به‌صورت اتمیک بررسی می‌کند و تعارض Commit ناشی از `xmin` را به `Precondition Failed` تبدیل می‌کند، بدون تصمیم‌گیری دربارهٔ قابل‌مشاهده‌بودن، مالکیت یا وضعیت Domain.
- [X] T012 ایجاد Composition Root، `ICurrentAuthorAccessor` قابل‌جایگزینی و Handler سراسری `ProblemDetails` در `src/BlogSpecLab.Api/Program.cs`، `src/BlogSpecLab.Api/Posts/Identity/` و `src/BlogSpecLab.Api/Posts/Errors/`; معیار پایان: API فقط Contractهای Application را مصرف می‌کند، هویت جاری را جدا از سازوکار حساب استخراج می‌کند و هیچ جزئیات حساس در خطا برنمی‌گرداند.
- [X] T013 ایجاد پوستهٔ `PostsController : ControllerBase` در `src/BlogSpecLab.Api/Posts/Controllers/PostsController.cs` و مدل‌ها/Mapping مشترک در `src/BlogSpecLab.Api/Posts/Models/` و `src/BlogSpecLab.Api/Posts/Mapping/`; معیار پایان: تمام مسیرهای قرارداد در Controller قرار می‌گیرند و Controller به DbContext یا Repository Concrete دسترسی ندارد.
- [X] T014 [P] نوشتن آزمون جهت وابستگی و ساختار Aggregate در `tests/BlogSpecLab.Architecture.Tests/ArchitectureRulesTests.cs`; معیار پایان: Domain Package/Reference خارجی، وابستگی معکوس لایه‌ها، Concrete Infrastructure در Controller و مصنوعات Posts بیرون از پوشهٔ Aggregate باعث شکست آزمون می‌شوند.
- [X] T015 [P] ایجاد Fixture PostgreSQL و Host آزمون در `tests/BlogSpecLab.Api.IntegrationTests/Posts/PostApiFixture.cs`; معیار پایان: Testcontainers با PostgreSQL واقعی اجرا می‌شود، Migrationها اعمال می‌شوند و Adapter هویت آزمایشی قابل کنترل است.

**Checkpoint**: Foundation آماده است؛ `Post` غنی، Persistence مبتنی بر `version_token` و `xmin`، Controller نازک، ProblemDetails و آزمون معماری وجود دارند.

---

## Phase 3: داستان کاربر ۱ — ایجاد پیش‌نویس (P1) 🎯 MVP

**هدف داستان**: نویسندهٔ احرازشده Post معتبر را به‌عنوان Draft مالک خود ایجاد می‌کند و
Draft از خواننده پنهان می‌ماند.

**آزمون مستقل**: با هویت نویسندهٔ آزمایشی، `POST /api/posts` معتبر را اجرا کنید؛ پاسخ
`201`، مالکیت، وضعیت Draft، نبود زمان انتشار و ETag opaque را بررسی کنید؛ سپس همان Draft
از مسیر خواننده قابل مشاهده نباشد.

- [X] T016 [P] [US1] نوشتن آزمون Application برای CreateDraft در `tests/BlogSpecLab.Application.Tests/Posts/CreateDraftTests.cs`; معیار پایان: Command معتبر Post Draft می‌سازد و Validation/هویت نامعتبر Commit ایجاد نمی‌کند.
- [X] T017 [P] [US1] نوشتن آزمون Integration قرارداد ایجاد Draft و پنهان‌بودن آن در `tests/BlogSpecLab.Api.IntegrationTests/Posts/CreateDraftTests.cs`; معیار پایان: `201` دارای Body قرارداد و Header `ETag` opaque است، ورودی خالی `400` می‌گیرد و Reader عنوان/متن Draft را نمی‌بیند.
- [X] T018 [US1] ایجاد Command، DTO، Handler و نتیجهٔ CreateDraft در `src/BlogSpecLab.Application/Posts/CreateDraft/`; معیار پایان: Handler از `AuthorId` مرزی استفاده و فقط Aggregate را ایجاد و Commit می‌کند.
- [X] T019 [US1] افزودن Mapping `POST /api/posts` و پاسخ `201` با Header `ETag` در `src/BlogSpecLab.Api/Posts/Controllers/PostsController.cs` و `src/BlogSpecLab.Api/Posts/Mapping/CreateDraftMapping.cs`; معیار پایان: Controller فقط Binding، Validation مرزی، استخراج هویت، فراخوانی Use Case و تبدیل نتیجه به HTTP انجام می‌دهد.
- [X] T020 [US1] تکمیل تولید اولیهٔ `version_token` و تبدیل آن به ETag opaque در `src/BlogSpecLab.Infrastructure/Posts/Persistence/` و `src/BlogSpecLab.Infrastructure/Posts/Repositories/`; معیار پایان: پاسخ ایجاد ETag دارد اما `xmin`، PostgreSQL و Token ماندگاری را افشا نمی‌کند.
- [ ] T021 [US1] اجرای آزمون‌های `CreateDraftTests.cs` و ثبت نتیجه در `tests/BlogSpecLab.Application.Tests/Posts/CreateDraftTests.cs` و `tests/BlogSpecLab.Api.IntegrationTests/Posts/CreateDraftTests.cs`; معیار پایان: SC-001 و بخش ایجاد SC-002 با آزمون موفق اثبات می‌شوند.

**Checkpoint**: ایجاد Draft برای نویسندهٔ احرازشده مستقل قابل استفاده و آزمون است؛ هیچ Draftی از مسیر Reader افشا نمی‌شود.

---

## Phase 4: داستان کاربر ۲ — انتشار پیش‌نویس مالک (P2)

**هدف داستان**: مالک Draft معتبر آن را با ETag مشاهده‌شده منتشر می‌کند و Post برای Reader
قابل مشاهده می‌شود؛ مالک غیرمجاز، نسخهٔ قدیمی و وضعیت نامعتبر کنترل‌شده رد می‌شوند.

**آزمون مستقل**: با Fixture یک Draft معتبر و ETag آن، `POST /api/posts/{id}/publish` را
اجرا کنید؛ Status و PublishedAt را بررسی کنید. همان آزمایش با نویسندهٔ دیگر، Header مفقود،
ETag قدیمی و Post منتشرشده باید به‌ترتیب بدون افشا رد شود.

- [ ] T022 [P] [US2] نوشتن آزمون‌های Application انتشار، مالکیت و وضعیت در `tests/BlogSpecLab.Application.Tests/Posts/PublishDraftTests.cs`; معیار پایان: فقط مالک Draft معتبر با Version Token منطبق Publish می‌کند و خطاهای مالکیت/وضعیت Commit ندارند.
- [ ] T023 [P] [US2] نوشتن آزمون‌های Integration `If-Match` و Concurrency انتشار در `tests/BlogSpecLab.Api.IntegrationTests/Posts/PublishDraftTests.cs` و `tests/BlogSpecLab.Api.IntegrationTests/Posts/PublishDraftConcurrencyTests.cs` پیش از رفتار انتشار؛ معیار پایان: Header مفقود برای Draft قابل‌دسترس `428`، ETag نامنطبق `412` و Draft غیرقابل مشاهده بدون افشای محتوا را می‌سنجد؛ Publish موفق Version Token را می‌چرخاند، درخواست Publish بعدی با ETag اولیه و قدیمی `412 precondition-failed` می‌گیرد، و `409` فقط با ETag منطبق با نسخهٔ جاری Post منتشرشده انتظار می‌رود؛ دو درخواست Publish هم‌زمان با ETag اولیهٔ یکسان دقیقاً یک `204` و یک `412 precondition-failed` را انتظار دارند و همهٔ آزمون‌ها ابتدا قرمز مشاهده می‌شوند.
- [ ] T024 [US2] ایجاد Command، Handler و Result انتشار در `src/BlogSpecLab.Application/Posts/PublishDraft/`; معیار پایان: Handler ابتدا قابل‌مشاهده‌بودن و مالکیت را کنترل می‌کند، سپس Precondition مربوط به `If-Match`/`PostVersion` را اعمال می‌کند؛ ETag نامنطبق بدون فراخوانی `Post.Publish` به `412 precondition-failed` تبدیل می‌شود. فقط پس از تطابق Token، Handler `Post.Publish` را برای کنترل وضعیت Domain فرا می‌خواند تا Post منتشرشده با ETag جاری `409 post-state-conflict` بدهد؛ سپس Aggregate مجاز و `PostVersion` را برای بررسی اتمیک نهایی `version_token` و `xmin` در Commit به Repository می‌دهد تا رقابت پس از Precondition نیز `412` شود. `IClock` فقط برای Domain فراهم می‌شود.
- [ ] T025 [US2] افزودن Mapping `POST /api/posts/{postId}/publish` و Header اجباری `If-Match` در `src/BlogSpecLab.Api/Posts/Controllers/PostsController.cs` و `src/BlogSpecLab.Api/Posts/Mapping/PublishDraftMapping.cs`; معیار پایان: پاسخ‌های 401، 404، 409، 412 و 428 مطابق `contracts/openapi.yaml` هستند و Publish موفق `204` است.
- [ ] T026 [US2] تکمیل نگاشت `version_token`، `xmin` و ذخیره‌سازی Publish در `src/BlogSpecLab.Infrastructure/Posts/Repositories/` و `src/BlogSpecLab.Infrastructure/Posts/Persistence/`; معیار پایان: Status، PublishedAt و `version_token` تازه در یک ذخیره‌سازی اتمیک Publish می‌شوند، توکن تازه به Domain یا HTTP افشا و در پاسخ 204 بازگردانده نمی‌شود، و Commit پس از تغییر رقیب به `Precondition Failed` بدون Retry یا Last-Write-Wins تبدیل می‌شود.
- [ ] T027 [US2] اجرای آزمون‌های PostgreSQL انتشار از `tests/BlogSpecLab.Api.IntegrationTests/Posts/PublishDraftTests.cs` و `tests/BlogSpecLab.Api.IntegrationTests/Posts/PublishDraftConcurrencyTests.cs` پس از T024 تا T026؛ معیار پایان: پس از Publish موفق، Publish با ETag اولیه و قدیمی `412 precondition-failed` می‌گیرد؛ Publish با ETag منطبق با نسخهٔ جاری Post منتشرشده `409 post-state-conflict` می‌گیرد؛ و دو درخواست هم‌زمان با ETag اولیهٔ یکسان دقیقاً یک `204` و یک `412` دارند و PublishedAt/محتوای نهایی فقط نتیجهٔ درخواست موفق است.
- [ ] T028 [US2] اجرای آزمون‌های انتشار و ثبت نتیجه در `tests/BlogSpecLab.Application.Tests/Posts/PublishDraftTests.cs` و `tests/BlogSpecLab.Api.IntegrationTests/Posts/PublishDraftTests.cs`; معیار پایان: FR-009 تا FR-013، بخش انتشار SC-003 و SC-004 و بخش Publish از FR-018، از جمله چرخش token و تفکیک 412 نسخهٔ قدیمی از 409 وضعیت Domain، پوشش دارند.

**Checkpoint**: انتشار مالک با If-Match، زمان انتشار و حفاظت از نسخهٔ قدیمی مستقل قابل آزمون است.

---

## Phase 5: داستان کاربر ۳ — مشاهدهٔ پست‌های منتشرشده (P3)

**هدف داستان**: Reader فقط همهٔ Postهای Published را از جدیدترین به قدیمی‌ترین فهرست و
هر Post منتشرشده را جداگانه می‌خواند.

**آزمون مستقل**: Fixture شامل Postهای Published با زمان‌های متفاوت و یک Draft بسازید؛
فهرست و Detail Reader را اجرا و ترتیب، همهٔ Publishedها و عدم افشای Draft را اثبات کنید.

- [ ] T029 [P] [US3] نوشتن آزمون‌های Application Query خوانندگان در `tests/BlogSpecLab.Application.Tests/Posts/PublishedPostQueryTests.cs`; معیار پایان: فقط Publishedها بر اساس PublishedAt نزولی برگردانده می‌شوند و Draft به DTO خواننده تبدیل نمی‌شود.
- [ ] T030 [P] [US3] نوشتن آزمون‌های Integration فهرست، Detail و عدم افشای Draft در `tests/BlogSpecLab.Api.IntegrationTests/Posts/ReadPublishedPostsTests.cs`; معیار پایان: فهرست شامل همهٔ Publishedها، صفر Draft و ترتیب صحیح است؛ Detail Draft همان پاسخ منبع غیرقابل مشاهده را می‌دهد.
- [ ] T031 [US3] ایجاد Queryها، Handlerها و DTOهای ListPublished و GetPublished در `src/BlogSpecLab.Application/Posts/ListPublished/` و `src/BlogSpecLab.Application/Posts/GetPublished/`; معیار پایان: Application هیچ نوع HTTP یا Persistence Concrete مصرف نمی‌کند و فقط Contractهای خواندن Application را استفاده می‌کند.
- [ ] T032 [US3] پیاده‌سازی Queryهای Published در `src/BlogSpecLab.Infrastructure/Posts/Repositories/PublishedPostReadRepository.cs`; معیار پایان: List همهٔ Publishedها را با PublishedAt نزولی و Detail فقط Published را برمی‌گرداند؛ Draft وجود یا محتوا را افشا نمی‌کند.
- [ ] T033 [US3] افزودن Mapping `GET /api/posts` و `GET /api/posts/{postId}` در `src/BlogSpecLab.Api/Posts/Controllers/PostsController.cs` و `src/BlogSpecLab.Api/Posts/Mapping/PublishedPostMapping.cs`; معیار پایان: پاسخ‌های 200 و 404 و مدل‌های HTTP دقیقاً با `contracts/openapi.yaml` منطبق‌اند.
- [ ] T034 [US3] اجرای آزمون‌های Reader در `tests/BlogSpecLab.Application.Tests/Posts/PublishedPostQueryTests.cs` و `tests/BlogSpecLab.Api.IntegrationTests/Posts/ReadPublishedPostsTests.cs`; معیار پایان: FR-014 تا FR-017، SC-005 و SC-006 اثبات شده‌اند.

**Checkpoint**: تجربهٔ Reader مستقل قابل استفاده و آزمون است و Draft از همهٔ مسیرهای آن پنهان می‌ماند.

---

## Phase 6: داستان کاربر ۴ — ویرایش پیش‌نویس مالک (P4)

**هدف داستان**: مالک Draft با ETag فعلی عنوان و متن را اتمیک ویرایش می‌کند، ETag تازه
می‌گیرد و هیچ درخواست قدیمی یا نامعتبر داده را تغییر نمی‌دهد.

**آزمون مستقل**: Fixture یک Draft مالک‌دار با ETag می‌سازد؛ ویرایش معتبر، ورودی نامعتبر،
غیرمالک، Header مفقود، ETag نامنطبق و دو ویرایش با ETag یکسان را اجرا کنید.

- [ ] T035 [P] [US4] نوشتن آزمون‌های Application ویرایش Draft در `tests/BlogSpecLab.Application.Tests/Posts/EditDraftTests.cs`; معیار پایان: مالک Draft با Title/Content معتبر و Version Token منطبق ویرایش می‌کند و هر خطا حالت قبلی را حفظ می‌کند.
- [ ] T036 [P] [US4] نوشتن آزمون‌های Integration ETag، وضعیت خطا و Concurrency ویرایش در `tests/BlogSpecLab.Api.IntegrationTests/Posts/EditDraftTests.cs` و `tests/BlogSpecLab.Api.IntegrationTests/Posts/EditDraftConcurrencyTests.cs` پیش از رفتار ویرایش؛ معیار پایان: 400 Validation، 404 عدم‌مشاهده، 409 وضعیت Published، 412 ETag قدیمی و 428 Header مفقود با ProblemDetails کنترل‌شده را می‌سنجد؛ دو درخواست Edit با ETag یکسان دقیقاً یک `204` همراه ETag تازه و یک `412 precondition-failed` را انتظار دارند و همهٔ آزمون‌ها ابتدا قرمز مشاهده می‌شوند.
- [ ] T037 [US4] ایجاد Command، Handler و Result ویرایش در `src/BlogSpecLab.Application/Posts/EditDraft/`; معیار پایان: ترتیب بررسی قابل‌مشاهده‌بودن/مالکیت، Precondition و سپس `Post.EditDraft` رعایت می‌شود و تغییر Title/Content اتمیک است.
- [ ] T038 [US4] افزودن Mapping `PUT /api/posts/{postId}`، Header اجباری `If-Match` و ETag جدید پاسخ 204 در `src/BlogSpecLab.Api/Posts/Controllers/PostsController.cs` و `src/BlogSpecLab.Api/Posts/Mapping/EditDraftMapping.cs`; معیار پایان: Controller نسخه را فقط به `PostVersion` opaque تبدیل و هرگز `xmin` یا `version_token` را افشا نمی‌کند.
- [ ] T039 [US4] تکمیل چرخش `version_token` پس از Edit موفق در `src/BlogSpecLab.Infrastructure/Posts/Repositories/` و `src/BlogSpecLab.Infrastructure/Posts/Persistence/`; معیار پایان: Edit موفق ETag جدید تولید می‌کند و عملیات متکی بر ETag قبلی در Commit یا پیش‌شرط با 412 رد می‌شود.
- [ ] T040 [US4] اجرای آزمون Concurrency PostgreSQL ویرایش از `tests/BlogSpecLab.Api.IntegrationTests/Posts/EditDraftConcurrencyTests.cs` پس از T037 تا T039؛ معیار پایان: دقیقاً یک `204` همراه ETag تازه و یک `412 precondition-failed` وجود دارد و دادهٔ نهایی فقط نتیجهٔ Edit موفق است.
- [ ] T041 [US4] اجرای آزمون‌های ویرایش در `tests/BlogSpecLab.Application.Tests/Posts/EditDraftTests.cs` و `tests/BlogSpecLab.Api.IntegrationTests/Posts/EditDraftTests.cs`; معیار پایان: FR-005 تا FR-008، بخش ویرایش SC-002 و بخش Edit از FR-018 پوشش دارند.

**Checkpoint**: ویرایش مالک مستقل قابل استفاده و آزمون است؛ ETag قدیمی هرگز تغییر بی‌صدای Draft ایجاد نمی‌کند.

---

## Phase 7: یکپارچه‌سازی، کنترل Constitution و پذیرش نهایی

**هدف**: اجرای کامل سناریوهای Quickstart و اثبات پذیرش قابلیت، بدون افزودن رفتار خارج از
Specification.

- [ ] T042 [P] بازبینی و تکمیل پاسخ‌های `ProblemDetails` در `src/BlogSpecLab.Api/Posts/Errors/ProblemDetailsMapping.cs` و آزمون‌های `tests/BlogSpecLab.Api.IntegrationTests/Posts/ProblemDetailsTests.cs`; معیار پایان: 400، 401، 404، 409، 412 و 428 هیچ Stack Trace، Secret، SQL، `xmin`، `version_token` یا محتوای Draft افشا نمی‌کنند.
- [ ] T043 [P] تکمیل آزمون‌های معماری در `tests/BlogSpecLab.Architecture.Tests/ArchitectureRulesTests.cs`; معیار پایان: همهٔ قواعد Onion، Domain بدون Package خارجی، Aggregate-first، Controller نازک و منع Minimal API در CI قابل تشخیص‌اند.
- [ ] T044 اجرای همهٔ Domain، Application، Architecture و API Integration Tests با `BlogSpecLab.slnx`؛ معیار پایان: `dotnet build BlogSpecLab.slnx` و `dotnet test BlogSpecLab.slnx` بدون شکست پایان می‌یابند.
- [ ] T045 اجرای سناریوهای ۱ تا ۷ در `specs/001-write-publish-post/quickstart.md` و ثبت هر اختلاف در آزمون‌های مربوط؛ معیار پایان: تمام نتایج مورد انتظار Quickstart، شامل دو درخواست ETag یکسان و پاسخ 412، اثبات شده‌اند.
- [ ] T046 انجام Constitution Check نهایی در `specs/001-write-publish-post/plan.md` در برابر `src/**` و `tests/**`; معیار پایان: هر هشت اصل، Referenceهای پروژه، Packageهای Domain، پوشه‌های Posts، آزمون‌های Invariant و عدم افشای دادهٔ حساس تأیید شده‌اند.

---

## وابستگی‌ها و ترتیب اجرا

### ترتیب Phaseها

```text
Phase 1 (Setup)
  → Phase 2 (Foundation)
    → US1 ایجاد Draft (P1)
    → US2 انتشار Draft (P2)
    → US3 خواندن Published (P3)
    → US4 ویرایش Draft (P4)
      → Phase 7 یکپارچه‌سازی و پذیرش
```

همهٔ داستان‌ها پس از Phase 2 با Fixture مناسب مستقل قابل آزمون‌اند؛ برای تحویل تدریجی در
محصول ترتیب اولویت `US1 → US2 → US3 → US4` است. US2، US3 و US4 برای آزمون مستقل لازم
نیست منتظر Endpoint داستان قبلی بمانند، اما در اجرای کامل، Postهای لازم را از Fixture
می‌سازند.

### وابستگی‌های حیاتی

- T001 تا T005 پیش‌نیاز T006 تا T015 هستند.
- T006 فقط پیش‌نیاز کامپایل T007 است؛ T007 باید قرمز شود و T008 آن را سبز کند. T008 و
  T009 پیش‌نیاز Handlerهای داستانی هستند؛ T009 تا T013 پیش‌نیاز Endpointها.
- T010، T011 و T015 فقط زیرساخت اجرای آزمون Integration و ETag/If-Match را فراهم می‌کنند؛
  رفتار Publish و Edit در آن‌ها پیاده‌سازی نمی‌شود.
- T016/T017 پیش از T018 تا T020؛ T022/T023 پیش از T024 تا T026 و سپس T027؛ در T024 ترتیب
  الزامی قابل‌مشاهده‌بودن/مالکیت → Precondition → `Post.Publish` → Commit اتمیک Repository
  است؛ T029/T030 پیش از T031 تا T033؛ و T035/T036 پیش از T037 تا T039 و سپس T040 اجرا
  می‌شوند.
- T042 تا T046 پس از هر چهار داستان اجرا می‌شوند.

## فرصت‌های اجرای موازی

- پس از T005، T006 و T014 می‌توانند هم‌زمان شروع شوند؛ T007 پس از T006 و پیش از T008
  انجام می‌شود تا رفتار Domain فقط پس از آزمون قرمز پیاده‌سازی شود.
- پس از T010، T011 و T015، آزمون‌های Integration داستان‌ها در فایل‌های جداگانه قابل
  آماده‌سازی‌اند؛ T023 و T036 باید پیش از رفتارهای Publish و Edit به‌صورت قرمز آماده
  شوند.
- در هر داستان، آزمون Application و Integration با نشان `[P]` قابل انجام هم‌زمان‌اند.
- پس از Foundation، توسعهٔ Queryهای US3 و Contractهای Mutation برای US2/US4 با تیم‌های
  جداگانه قابل پیشبرد است، مشروط به استفاده از Fixture و Contractهای مشترک.

## راهبرد تحویل

### MVP

MVP شامل Phase 1، Phase 2 و US1 است: نویسندهٔ احرازشده Draft معتبر با ETag opaque ایجاد
می‌کند و Reader به Draft دسترسی ندارد. پیش از ادامه باید Checkpoint US1 و آزمون‌های آن
موفق باشند.

### تحویل افزایشی

1. US1: ایجاد Draft ایمن و پنهان از Reader.
2. US2: انتشار مالک با If-Match و حفاظت نسخهٔ قدیمی.
3. US3: فهرست و Detail فقط برای Publishedها.
4. US4: ویرایش اتمیک Draft با ETag تازه.
5. Phase 7: اجرای کامل Quickstart و همهٔ Gateهای Constitution.

## ردیابی نیازمندی‌ها

| نیازمندی | وظایف پوشش‌دهنده |
|---|---|
| FR-001 تا FR-004 | T006 تا T008، T016 تا T021 |
| FR-005 تا FR-008 | T006 تا T008، T035 تا T041 |
| FR-009 تا FR-013 | T006 تا T008، T022 تا T028 |
| FR-014 تا FR-017 | T029 تا T034، T042 و T045 |
| FR-018 | T009 تا T011، T020، T023 تا T027، T036 و T038 تا T040، T045 |
| SC-001 تا SC-006 | T021، T028، T034، T041، T044 و T045 |

## یادداشت اجرایی

- هر وظیفه باید پیش از علامت‌خوردن، معیار پایان خود را برآورده کند.
- هیچ رفتار خارج از `spec.md` یا قرارداد `contracts/openapi.yaml` افزوده نشود.
- تغییر در هر تصمیم معماری یا Product Rule فقط پس از اصلاح رسمی Constitution یا
  Specification مجاز است.
