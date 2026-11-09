# راهنمای اجرای واقعی و Demo

این راهنما در 2026-10-12 از ریشهٔ مخزن با Windows، PowerShell 7.6.6، SDK 10.0.401 و ASP.NET Core Runtime 10.0.12 اجرا شد. SQLite تازه، Migration موجود، Provision واقعی و میزبان Kestrel HTTPS استفاده شدند؛ [شواهد](validation.md). این اجرا پذیرش انسانی نیست.

## آماده‌سازی

فرمان‌ها را از ریشهٔ `BlogSpecLab` اجرا کنید. Runtime 10.0.12 برای خواندن بستهٔ رمزگذاری‌شده توسط `demo.ps1` لازم است. ابزار تعامل نمونه PowerShell 7.6 است. گواهی توسعه باید از قبل معتبر و trusted باشد؛ دستور زیر وضعیت آن را بررسی می‌کند.

```powershell
dotnet --list-sdks
dotnet dev-certs https --check --trust
dotnet restore BlogSpecLab.slnx
dotnet tool restore
dotnet build BlogSpecLab.slnx --no-restore
dotnet test BlogSpecLab.slnx --no-build --no-restore
```

SDKهای نصب‌شدهٔ بررسی‌شده: 9.0.312، 10.0.200 و 10.0.401؛ هدف همهٔ پروژه‌ها net10.0 است. ابزار محلی dotnet-ef برابر 10.0.12 است. برای ساخت از صفر نیز `dotnet clean BlogSpecLab.slnx` و سپس restore/build/test اجرا و تأیید شدند.

یک پوشهٔ تازه بسازید؛ فایل DB داخل مخزن لازم نیست. تنظیم زیر را در **هر دو نشست** آماده‌سازی و میزبان یکسان نگه دارید. متغیر محیط بر `appsettings.json` مقدم است. مسیر نسبی پیش‌فرض `blogspeclab.db` به پوشهٔ اجرای فرایند وابسته است؛ مسیر مطلق این ابهام را برطرف می‌کند.

```powershell
$demoRoot = Join-Path $env:TEMP ('BlogSpecLab.Phase6.' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $demoRoot | Out-Null
$env:ConnectionStrings__BlogDatabase = 'Data Source=' + (Join-Path $demoRoot 'blog.db') + ';Pooling=False'
dotnet ef database update --project src/02.Infra/DataAccess --startup-project src/03.Endpoints/Api --no-build
dotnet run --no-build --no-launch-profile --project src/03.Endpoints/Api -- access provision 2026-12-31T23:59:59Z
```

Migration `20261012072052_InitialBlogSchema` روی فایل خالی اعمال شد. Migration تازه ایجاد نمی‌کنیم؛ Schema فعلی موجود است. Startup معمولی Migration یا Provision انجام نمی‌دهد. انقضای مثال UTC و در زمان بررسی در آینده بود؛ هنگام اجرای بعدی زمان آیندهٔ صریح انتخاب کنید. Provision هر بار پنج Author، پنج Editor و یک AuthorEditor مستقل می‌سازد؛ این حساب‌ها شرکت‌کنندگان پذیرش انسانی نیستند.

## Credential و اجرای API

Provision بستهٔ `.protected` را زیر `%LOCALAPPDATA%/BlogSpecLab/Secrets` می‌نویسد. محتوا با ASP.NET Core Data Protection و کلیدهای Windows DPAPI همان حساب سیستم محافظت می‌شود؛ token خام در Console و DB نوشته نمی‌شود. DB فقط SHA-256 token تصادفی ۳۲ بایتی را نگه می‌دارد. شناسهٔ Credential و UserId را از بسته در حافظه بگیرید؛ token قابل بازیابی از DB نیست. [جزئیات ابزار](../../docs/operations/local-access.md).

در نشست میزبان با همان Connection String:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'https://localhost:7180'
dotnet run --no-build --no-launch-profile --project src/03.Endpoints/Api
```

نشست میزبان را باز نگه دارید؛ پایان با Ctrl+C. در نشست دوم، Connection String همان DB را تنظیم کنید. اگر چند بسته دارید، بستهٔ همان Provision را انتخاب کنید؛ انتخاب آخرین فایل فقط وقتی درست است که Provision دیگری هم‌زمان انجام نشده باشد.

```powershell
$bundle = Get-ChildItem (Join-Path $env:LOCALAPPDATA 'BlogSpecLab/Secrets') -Filter '*.protected' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
& ./specs/001-blog-post-management/demo.ps1 -ProtectedFile $bundle.FullName
$doc = Invoke-RestMethod https://localhost:7180/openapi/v1.json
```

`demo.ps1` بسته را با همان حساب Windows، ApplicationName برابر `BlogSpecLab.LocalAccess` و purpose برابر `BlogSpecLab.LocalAccess.v1` فقط در حافظه باز می‌کند؛ حساب‌های ۰ تا ۴ Author، ۵ تا ۹ Editor و ۱۰ دو نقشی هستند. Credential مستقیماً در header `Authorization: Bearer …` قرار می‌گیرد. بسته یا token را چاپ، در history درج یا به فایل متنی export نکنید. ابزار مدیریت HTTP حساب وجود ندارد. Demo در پایان مجوز و Credential Editor اول بسته را لغو می‌کند؛ برای تکرار کامل Demo، batch تازه provision کنید.

## درخواست‌های اجراشده

تابع `Invoke-Blog` در [demo.ps1](demo.ps1) یک پاسخ شامل `Value`، `ETag` و `Headers` می‌دهد و فقط روش، مسیر، status و code را چاپ می‌کند. `$author` و `$editor` از بسته در حافظه‌اند؛ `$id` و `$p.ETag` از پاسخ واقعی گرفته می‌شوند. نمونه‌های زیر همان فراخوانی‌های اجراشدهٔ فایل هستند؛ برای اجرای کامل و مدیریت متغیرها فایل را اجرا کنید.

```powershell
$p = Invoke-Blog POST '/api/author/posts' $author @{title='عنوان نمونه';body='متن نمونه'} -Expected 201
$id = $p.Value.id
$initialTag = $p.ETag
$null = Invoke-Blog GET '/api/author/posts' $author
$p = Invoke-Blog GET "/api/author/posts/$id" $author
$p = Invoke-Blog PUT "/api/author/posts/$id" $author @{title='عنوان اصلاح‌شده';body='متن اصلاح‌شده'} $p.ETag
$p = Invoke-Blog POST "/api/author/posts/$id/submit" $author -ETag $p.ETag
$null = Invoke-Blog GET '/api/editor/posts' $editor
$p = Invoke-Blog GET "/api/editor/posts/$id" $editor
$p = Invoke-Blog POST "/api/editor/posts/$id/request-changes" $editor @{reason='اصلاح عنوان لازم است'} $p.ETag
$p = Invoke-Blog GET "/api/author/posts/$id" $author
$p = Invoke-Blog PUT "/api/author/posts/$id" $author @{title='عنوان نهایی';body='متن نهایی'} $p.ETag
$p = Invoke-Blog POST "/api/author/posts/$id/submit" $author -ETag $p.ETag
$p = Invoke-Blog POST "/api/editor/posts/$id/approve" $editor -ETag $p.ETag
$p = Invoke-Blog POST "/api/editor/posts/$id/publish" $editor -ETag $p.ETag
$public = Invoke-Blog GET "/api/posts/$id"
$null = Invoke-Blog GET '/api/posts'
```

Create پاسخ 201 با Location و ETag دارد؛ دیگر عملیات بالا 200 دارند. حذف پست منتشرنشده با DELETE و If-Match جاری 204 و بدون Body می‌دهد. درخواست Submit/Approve/Publish/DELETE، Body ندارد. ETag یک UUID داخل کوتیشن است؛ آن را بدون حذف کوتیشن در If-Match بفرستید. در هر mutation آخرین ETag را جایگزین کنید. انتشار تکراری با مجوز جاری و If-Match دارای قالب معتبر، حتی Version قدیمی، همان انتشار نخست را برمی‌گرداند و Commit تازه ندارد.

| وضعیت | اقدام نویسندهٔ مالک | اقدام Editor غیرمالک |
| --- | --- | --- |
| Draft | edit، delete، submit | در جزئیات صف قابل مشاهده نیست |
| ChangesRequested | edit، delete، submit؛ دلیل در lastReview.reason | در جزئیات صف قابل مشاهده نیست |
| PendingReview | edit، delete | requestChanges، approve |
| Approved | edit، delete | publish |
| Published | آرایهٔ خالی | آرایهٔ خالی؛ انتشار تکراری استثنای قرارداد است |

`allowedActions` با ترتیب ثابت و بدون تکرار است و مجوز درخواست بعدی را تضمین نمی‌کند. کاربر دو نقشی اتحاد اقدامات مجاز را می‌بیند، ولی approve پست خود حذف و اجرای آن 403 است. صف و فهرست‌ها به ترتیب Id، بدون صفحه‌بندی‌اند. PostView مدیریتی شامل ownerId، version، lastReview و publication است؛ PublicPostView فقط id، title، body و publishedAt دارد. مدیریت و خطاها Cache-Control: no-store دارند.

## خطاها و لغو دسترسی

خطاها `application/problem+json` با type=about:blank، title، status، detail، instance و code پایدار هستند. پیام انسانی فارسی است. PowerShell ممکن است Content نوع problem+json را byte[] بگیرد؛ تابع نمونه آن را با UTF-8 به JSON تبدیل می‌کند.

| آزمون اجراشده در Demo | نتیجه |
| --- | --- |
| بدون Credential | 401 unauthenticated؛ WWW-Authenticate: Bearer |
| Author دیگر / Public روی Draft | 404 post_not_found |
| نبود If-Match | 428 version_required |
| If-Match برابر wildcard | 400 invalid_input |
| Version پیش از ویرایش | 412 stale_version؛ currentStatus/currentVersion، بدون متن پست |
| خودتأییدی حساب دو نقشی | 403 self_approval_forbidden |
| PUT و DELETE Published با ETag جاری | 409 published_post_immutable |
| Public روی حذف منطقی | 404 post_not_found |
| Permission لغوشده، Credential معتبر | 403 forbidden |
| Credential لغوشده | 401 unauthenticated |

پس از 412 جزئیات را بخوانید و اقدام مناسب را انتخاب کنید؛ درخواست شکست‌خورده خودکار تکرار نمی‌شود. 409 یعنی عملیات با وضعیت فعلی سازگار نیست. خطاهای storage_unavailable برابر 503، approval_required برابر 409 و unexpected_error برابر 500 در آزمون‌های HTTP/نگاشت بررسی می‌شوند؛ اختلال DB را برای Demo ایجاد نکردیم. وضعیت‌های خارج از محدودهٔ صف Editor از مسیر مدیریتی مربوط 404 هستند.

فرمان‌های زیر در Demo با شناسه‌های واقعی همان بسته اجرا شدند؛ مقدار token در آرگومان نیست:

```powershell
dotnet run --no-build --no-launch-profile --project src/03.Endpoints/Api -- access revoke-permissions $editor.UserId
dotnet run --no-build --no-launch-profile --project src/03.Endpoints/Api -- access revoke-credential $editor.CredentialId
```

لغو Permission تمام مجوزهای عملیاتی را خاموش می‌کند و Credential را معتبر نگه می‌دارد؛ لغو Credential مستقل است. تاریخچهٔ مالکیت، تصمیم و انتشار حذف نمی‌شود. بستهٔ رمزگذاری‌شده را خارج مخزن و زیر دسترسی همان حساب نگه دارید؛ پس از پایان استفاده، Credentialهای آزمایشی را لغو کنید.

## محدودیت و پذیرش

این اجرای محلی Windows/SQLite است؛ Provider نهایی DB و Authentication سراسری تعیین نشده‌اند. این راهنما SLA، ظرفیت یا کارایی Production را اثبات نمی‌کند. HTTPS استاندارد میزبان استفاده شد و certificate validation دور زده نشد. موفقیت خودکار SC-001/002/003/006 با آزمون‌ها و شواهد محدود به سناریوهای ثبت‌شده است. SC-004 و SC-005 هنوز اجرا نشده‌اند؛ [پروتکل پذیرش انسانی](acceptance.md) آماده است و T040 باز می‌ماند.