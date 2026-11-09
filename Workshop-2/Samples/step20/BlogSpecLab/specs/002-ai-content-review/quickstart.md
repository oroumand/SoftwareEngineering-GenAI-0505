# راهنمای اعتبارسنجی بررسی کمکی محتوا

این راهنما سناریوهای قابل اجرای **پس از پیاده‌سازی** را تعریف می‌کند؛ endpoint، تنظیم‌های AI و آزمون‌های پیشنهادی این قابلیت هنوز وجود ندارند. تولید Plan یا موفقیت آزمون‌های قابلیت اول شاهد پذیرش AI نیست. [قرارداد HTTP](contracts/http-api.md)، [مرز AI](contracts/application-ai.md) و [مدل داده](data-model.md) مرجع جزئیات‌اند.

## پیش‌نیاز و آماده‌سازی

- .NET 10 SDK و ابزارهای موجود مخزن؛ اجرای فرمان‌ها از ریشهٔ پروژه.
- SQLite موقت/محلی، Migration و Provision صریح مطابق [local-access](../../docs/operations/local-access.md)؛ startup عادی این کارها را انجام نمی‌دهد.
- Credential نویسنده و ویراستار با روش محرمانهٔ موجود؛ token و API key در مخزن، خروجی آزمون یا تاریخچهٔ فرمان ثبت نشوند.
- برای مسیر واقعی AI، دسترسی مجاز حساب/شبکه به OpenAI Responses API و مدل اولیهٔ gpt-6-luna؛ corpus مصنوعی فاقد اطلاعات شخصی. برای CI deterministic، هیچ key یا شبکهٔ بیرونی لازم نیست.

```powershell
dotnet restore BlogSpecLab.slnx
dotnet tool restore
dotnet build BlogSpecLab.slnx --no-restore
dotnet test BlogSpecLab.slnx --no-build --no-restore --filter 'Category!=AIQuality&Category!=AIProvider'
```

در اجرای آینده هیچ آزمون اصلی AI به‌علت نبود key skip نشود؛ suite deterministic باید واقعاً آزمون‌های این Feature را اجرا کرده و تعدادشان غیرصفر باشد. `AIProvider` و `AIQuality` نام Traitهای پیشنهادی اجرای واقعی‌اند؛ هنگام این Plan فرمان‌های filter وجود آزمون آینده را اثبات نمی‌کنند. آزمون‌های قبلی قابلیت اول نیز باید پاس بمانند.

برای اجرای محلی واقعی، API key را از secret storage در `AI__ContentReview__ApiKey` وارد محیط فرایند کنید، بدون چاپ مقدار. تنظیم‌های غیرمحرمانه:

```powershell
$env:AI__ContentReview__Enabled = 'true'
$env:AI__ContentReview__Model = 'gpt-6-luna'
dotnet run --no-launch-profile --project src/03.Endpoints/Api
```

پایگاه داده و URL/HTTPS طبق محیط موجود تنظیم شوند. در نبود key یا Enabled=false، CRUD قبلی قابل اجرا و درخواست مجاز بررسی 503/ai_review_unavailable باشد؛ درخواست غیرمجاز همچنان خطای دسترسی بگیرد. endpoint اجازهٔ تغییر مدل یا prompt از Client ندارد.

## راهنمای نویسنده

با درخواست بررسی، عنوان و متن ذخیره‌شدهٔ پست شما برای بررسی به سرویس بیرونی OpenAI ارسال می‌شوند؛ هویت نویسنده، دلیل برگشت و تاریخچهٔ گردش کار ارسال نمی‌شوند. گزارش کمکی است و ممکن است از نظر معنایی اشتباه باشد؛ نتیجهٔ بدون مشکل تضمین صحت یا تأیید انتشار نیست.

- completed: بررسی تمام شده؛ issues ممکن است خالی یا شامل مشکلات احتمالی باشد.
- needs_input: حداقل یک قسمت برای ارزیابی به اطلاعات بیشتر نیاز دارد؛ missingInformation را بخوانید. issues شناخته‌شدهٔ دیگر نیز ممکن است وجود داشته باشند.
- refusal یا failure: گزارش معتبر تولید نشده است؛ ProblemDetails.code را بخوانید. فهرست خالی به‌تنهایی موفقیت نیست.
- requiresNewReview=true: گزارش برای محتوای snapshot قدیمی است؛ محتوای فعلی را دوباره بررسی کنید. پس از دریافت نیز revision گزارش را با revision پست تازه مقایسه کنید.

برای اصلاح، عنوان/متن را از مسیر موجود ویرایش و ذخیره کنید؛ سپس بررسی تازه درخواست دهید. پیشنهادها خودکار اعمال نمی‌شوند. ارسال به ویراستار اقدام مستقل شماست و حتی بدون بررسی یا پس از failure طبق قواعد موجود مجاز است. بررسی تخصصی، جست‌وجوی منبع بیرونی و راستی‌آزمایی قطعی در این قابلیت انجام نمی‌شود.

### درخواست HTTP پس از پیاده‌سازی

در session خصوصی، `BLOG_API_BASE_URL` و `BLOG_AUTHOR_TOKEN` را با روش امن تنظیم کنید. شناسهٔ پست متعلق به همین نویسنده و در Draft/ChangesRequested باشد. قبل از اجرا از مسیر موجود یک پست بسازید یا ویرایش و ذخیره کنید.

```powershell
$blogApiBaseUri = $env:BLOG_API_BASE_URL.TrimEnd('/')
$blogAuthorHeaders = @{ Authorization = "Bearer $env:BLOG_AUTHOR_TOKEN" }
$blogPostId = '10000000-0000-0000-0000-000000000001' # با شناسهٔ واقعی پست خود جایگزین شود
$blogReview = Invoke-RestMethod -Method Post -Uri "$blogApiBaseUri/api/author/posts/$blogPostId/content-review" -Headers $blogAuthorHeaders
$blogReview.report.status
$blogReview.freshness.requiresNewReview
```

Body و If-Match ارسال نکنید؛ گزارش از متن ذخیره‌شده گرفته می‌شود، نه متن محلی ذخیره‌نشده. برای خواندن مشکلات از report.issues و اطلاعات موردنیاز از report.missingInformation استفاده کنید. در پاسخ غیر 200، code بدنهٔ ProblemDetails را بخوانید؛ متن پیام مبنای تصمیم برنامه نباشد. گزارش و محتوای خصوصی را به log عمومی منتقل نکنید.

## سناریوهای deterministic — بدون Provider واقعی

| سناریو / ورودی | نتیجهٔ لازم |
| --- | --- |
| گزارش completed با issue نگارشی معتبر و شاهد دقیق | 200، issue قابل استخراج؛ تمام داده‌های Post/Review/Publication ثابت |
| completed با issues خالی | 200 با status صریح و missingInformation خالی؛ بدون ادعای تأیید |
| needs_input برای ارجاع مبهم، و نمونهٔ مختلط نگارش + کمبود منبع | 200، حداقل یک missingInformation؛ issueهای قابل تشخیص حفظ شوند |
| enum ناشناخته، فیلد ناقص/extra/duplicate، null نابجا، whitespace، JSON خراب، خروجی بیش از حد | 502/ai_review_invalid_output؛ گزارش معتبر تحویل نشود |
| completed با missingInformation پر یا needs_input با آن خالی | 502/ai_review_invalid_output |
| evidence ناموجود یا تفاوت نیم‌فاصله/Unicode با snapshot | 502/ai_review_invalid_output؛ fuzzy matching انجام نشود |
| refusal صریح، response truncated، شبکه/429/5xx | به‌ترتیب 422/refused، 502/invalid_output، 503/unavailable؛ هرگز completed خالی |
| بدون credential، نویسنده فاقد Permission، Editor تنها، غیرمالک | 401، 403، 403، 404 مطابق قرارداد؛ Provider فراخوانی نشود |
| ناموجود/حذف‌شده، PendingReview/Approved/Published، title/body whitespace | 404، 409، 400؛ Provider فراخوانی نشود |
| body غیرخالی و نبود If-Match | اولی 400؛ دومی برای درخواست مجاز موفق، بدون الزام header |
| متن شامل دستور تأیید/انتشار | صرفاً متن بررسی؛ هیچ write/tool یا تغییر status رخ ندهد |
| submit مستقل، بدون گزارش یا پس از needs_input/refusal/failure | قواعد قابلیت اول و بررسی انسانی پابرجا |
| درخواست تکراری یا ویرایش ذخیره‌شده و درخواست تازه | هر درخواست snapshot خودش؛ نتیجهٔ قبلی cache نشود |

Adapter با transport کنترل‌شده واقعاً serialization SDK را بررسی کند: request به /v1/responses، non-streaming، فقط title/body از دادهٔ کاربر، text.format با type=json_schema، name=content_review_v1، schema دقیق artifact و strict=true، store=false و بدون tools/conversation/previous_response_id/metadata کاربری. key Provider فقط برای Authentication آن است و token کاربر در outbound نیست. response با status=completed، content item نوع refusal، status=incomplete/incomplete_details، پیام ناقص، tool/output form غیرمنتظره و خطای transport مستقل آزموده شوند؛ status پروتکل completed با completed/needs_input داخل JSON گزارش اشتباه نشود. item فنی reasoning گزارش نیست و به Application منتقل نشود؛ fake Application جای آزمون adapter را نمی‌گیرد. log/trace/ProblemDetails نباید محتوا، output خام یا secrets داشته باشند.

## رقابت، لغو و سقف زمان

از gateway fake قابل توقف و clock/TimeProvider کنترل‌شده استفاده شود. بعد از گرفتن snapshot، پاسخ gateway متوقف و با scope/DbContext مستقل یکی از عملیات زیر انجام شود؛ سپس پاسخ آزاد گردد:

1. ویرایش و ذخیره: report بر snapshot اول، source revision اول، isCurrentContent=false و requiresNewReview=true؛ متن جدید تغییر نکند.
2. submit بدون تغییر محتوا: گردش کار PendingReview بماند، revision ثابت و freshness محتوا true در زمان recheck؛ AI وضعیت را برنگرداند.
3. حذف نرم: 404/post_not_found و عدم تحویل گزارش؛ هیچ بازسازی پست.
4. لغو Permission با Credential معتبر: 403/forbidden و عدم تحویل گزارش.
5. اختلال خواندن تازهٔ DB: 503/storage_unavailable؛ پست tracked قبلی مبنای پاسخ نشود.

پس از دریافت نیز پست را ویرایش کنید: Client با revision تازه باید نتیجهٔ قبلی را stale نشان دهد؛ checkedAt تضمین آینده نیست. تمام مقایسه‌ها تغییر مستقل کاربر را از هر اثر AI تفکیک کنند.

کندی یا عدم پاسخ Provider، adapter بی‌اعتنا به token و کندی authentication/storage را آزمون کنید. ساعت از دریافت HTTP باشد؛ آماده‌شدن نتیجه یا timeout حداکثر تا ۶۰ ثانیه، نه شصت ثانیه بعد از شروع Provider. در fake clock زمان را تا deadline ببرید و سپس پاسخ دیررس آزاد کنید؛ هیچ موفقیتی برای همان درخواست تحویل نشود. یک آزمون Integration elapsed واقعی با Client متصل و cancellation میزبان، سقف آماده‌سازی پاسخ را اندازه بگیرد؛ زمان انتقال شبکهٔ Client جداست. قطع Client باید token را به وابستگی برساند، retry/write ایجاد نکند و گزارش دیررس دور انداخته شود؛ دریافت HTTP برای Client قطع‌شده شرط آزمون نیست.

برای dependency غیرهمکار، پایان پاسخ timeout را قبل از آزادکردن task بررسی کنید؛ منابع اختصاصی آن تا پایان task زود dispose نشوند، پس از پایان cleanup شوند و task به Response یا DbContext جدید/مشترک دسترسی نداشته باشد. exception دیررس نیز باید observe شود و درخواست بعدی بتواند مستقل اجرا شود.

## smoke test و ارزیابی مدل واقعی

پس از اجرای suite معمولی، با secret محیطی و فعال‌سازی صریح Provider:

```powershell
dotnet test BlogSpecLab.slnx --no-build --no-restore --filter 'Category=AIProvider'
dotnet test BlogSpecLab.slnx --no-build --no-restore --filter 'Category=AIQuality'
```

این Traitها طراحی آینده‌اند؛ در نبود آزمون یا key، پذیرش نتیجه‌گیری نشود. AIProvider schema acceptance، parsing، model access و cancellation واقعی را بررسی کند. AIQuality اجرای corpus نسخه‌دار با مدل درخواستی، prompt/Schema و تنظیمات مؤثر ثابت و سپس rubric انسانی است؛ موفقیت فرایند آزمون بدون نمرهٔ انسانی SC-005 پذیرش نیست.

پیش از اولین run، حداقل ۲۰ متن فارسی دارای ایراد و ۱۰ متن سالم را با پاسخ مرجع انسانی ثابت کنید. چهار دستهٔ FR-004، title/body، چند ایراد یک بخش، نمونهٔ مختلط و کمبود منبع/ارجاع مبهم پوشش داده شوند. gold issueها شناسه، دسته، بخش، شاهد و معنای ایراد داشته باشند؛ موارد needs_input و منبع مفقود نیز علامت‌گذاری شوند. corpus مصنوعی باشد؛ محتوای واقعی نویسنده خودکار به مجموعهٔ ارزیابی وارد نشود.

سه run از پیش تعیین‌شده انجام شود؛ بهترین خروجی انتخاب نشود. در هر run جدا:

- SC-005: ایرادهای مرجع کشف‌شده / همهٔ ایرادهای مرجع ≥۹۰٪؛ تطبیق معنایی انسانی، نه عبارت دقیق؛ هر gold فقط یک‌بار شمرده شود.
- SC-005: متن‌های سالم بدون مشکل بی‌پشتوانه / همهٔ متن‌های سالم ≥۹۰٪؛ با ۱۰ متن حداقل ۹ متن. failure/refusal/timeout نمونهٔ سالم، پاس محسوب نشود.
- SC-002: همهٔ نمونه‌های کمبود منبع/ارجاع مبهم needs_input و بدون منبع/پاسخ ساخته‌شده؛ صحت معنایی با انسان بررسی شود.
- نمونهٔ شکست‌خورده از denominator حذف نشود؛ SC-001/SC-003/SC-004/SC-007/SC-008 نیز مستقل با شواهد آزمون اثبات شوند.

مدل درخواستی و مدل واقعی پاسخ Provider، snapshot فقط در صورت وجود رسمی، تاریخ اجرای Eval، SDK، prompt/Schema/corpus version، تنظیم‌های مؤثر از جمله Reasoning، تعداد نمونه/ایراد، outcome هر نمونه، latency و رأی انسانی در شواهد پذیرش آینده ثبت شوند؛ secrets ثبت نشوند. gpt-6-luna انتخاب اولیه است و تضمین Quality Gate ندارد؛ اگر آستانه‌های Specification پاس نشوند انتخاب مدل براساس Eval بازبینی شود، بدون fallback خودکار. Reasoning و کفایت max_output_tokens=8192 باید benchmark/Eval شوند؛ snapshot حدسی ساخته نشود. هیچ threshold از نتیجهٔ اجرای اولیه به نفع مدل تغییر نکند. تغییر مدل یا prompt/Schema مستلزم ارزیابی دوباره است؛ شکست هر آستانه مانع پذیرش قابلیت است.

## ارزیابی فهم راهنما

حداقل پنج نویسنده همین راهنما و نمونه‌های completed، needs_input و failure را ببینند. هر نفر باید هر سه وضعیت را درست تشخیص دهد، بداند اصلاح/ارسال مستقل بر عهدهٔ اوست و گزارش، تأیید ویراستار نیست. SC-006 حداقل ۸۰٪ افراد با پاسخ کامل صحیح است؛ برای پنج نفر حداقل چهار نفر. شمارش پاسخ‌ها و اشکال‌های فهم ثبت شود؛ نبود ارزیابی انسانی یا شکست آن مانع پذیرش است.

این مرحله فقط روش اعتبارسنجی را طراحی کرده است؛ آزمون Feature، تماس واقعی، ارزیابی کیفیت یا پذیرش انسانی انجام نشده‌اند.
