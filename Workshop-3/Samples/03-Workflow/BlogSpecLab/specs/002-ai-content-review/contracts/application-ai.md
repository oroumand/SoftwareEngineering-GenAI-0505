# قرارداد Application و Infra.AI

این قرارداد طرح آینده است؛ typeهای SDK، کد handler یا service body تعریف نمی‌کند. [مدل داده](../data-model.md) vocabulary و [research](../research.md) دلایل تصمیم‌ها را نگه می‌دارند.

## قراردادهای متعلق به Application

| قرارداد | ورودی | خروجی / تعهد |
| --- | --- | --- |
| PostContentReviewService.ReviewAsync | userId معتبر سروری، postId، deadline context و CancellationToken | نتیجهٔ نوع‌دار گزارش envelope یا failure موجود/جدید؛ هیچ write ندارد |
| IPostContentReviewReader.ReadAsync | postId و CancellationToken | projection immutable تازه یا null؛ Id/OwnerId/Title/Body/Status/ContentRevision/IsDeleted؛ بدون EF type یا IQueryable |
| IContentReviewGateway.ReviewAsync | ReviewContentInput شامل فقط Title/Body و CancellationToken | ContentReviewGatewayOutcome؛ candidate، refused یا failure |

Reader در DataAccess، gateway در Infra.AI پیاده‌سازی می‌شوند. IUserAccessReader موجود برای Permission جاری استفاده می‌شود. cancellation واقعی با dependencyها عبور داده شود؛ coordinator نتیجهٔ دیررس را حتی با gateway غیرهمکار تحویل ندهد. deadline context مبنای monotonic دارد و اطلاعات کاربر/پست به SDK نمی‌برد.

Composition برای عملیات I/O که ممکن است پس از قطع انتظار هنوز در حال اجرا باشد، scope/resources متعلق به عملیات فراهم کند و آن را فقط پس از پایان task dispose کند؛ scope معمول HTTP مالک DbContext فعال دیررس نباشد. service/reader در همین scope مستقل scoped هستند. task دیررس نباید HttpContext یا Response را نگه دارد و coordinator بعد timeout هیچ خواندن تازه‌ای با همان DbContext آغاز نکند. cancellation و exception آن observe شوند؛ فقط cleanup باقی می‌ماند، نه امکان تحویل یا retry گزارش.

### ContentReviewGatewayOutcome

- Candidate: DTO مستقل ContentReviewReport؛ transport فقط response با status پروتکل completed و پیام کامل دارای یک JSON object، بدون refusal و tool/output form غیرمنتظره را candidate می‌کند. status پروتکل مستقل از completed/needs_input داخل JSON است. candidate هنوز گزارش قابل تحویل نیست.
- Refused: امتناع صریح Provider؛ هیچ candidate همراه آن تحویل نشود. متن خام refusal در API و log قرار نگیرد؛ پیام امن فارسی در HTTP ساخته شود.
- Failure: نوع مشخص Unavailable، Timeout یا InvalidOutput؛ exception و status داخلی Provider فقط به علت امن تبدیل شوند. کلید نامعتبر، سهمیه، 429 و 5xx همگی Unavailable‌اند؛ بدون افشای account/credential.
- cancellation ناشی از Client: token/cancellation تا coordinator حفظ شود، به Candidate یا Refused تبدیل نشود.

اگر envelope پروتکل خراب یا response ناقص باشد InvalidOutput است؛ اگر refusal صریح همراه fragment آمده باشد refusal بر fragment مقدم است. deadline بر تمام outcomeهای دیررس مقدم است. در هر outcome قابل بازگشت، قبل از افشای گزارش یا metadata، Application مجوز و وجود/مالکیت تازه را کنترل می‌کند؛ خطای دسترسی/نبود پست بر outcome مدل مقدم است. recheck در timeout مستلزم I/O پس از deadline نیست؛ timeout هیچ محتوا یا metadata ندارد.

## درخواست Provider

Provider همین Feature OpenAI، مدل اولیهٔ ارزیابی `gpt-6-luna` و SDK رسمی `OpenAI 2.14.0` است. adapter از `OpenAI.Responses.ResponsesClient.CreateResponseAsync` با `CreateResponseOptions` و CancellationToken، non-streaming، بدون هیچ ابزار، conversation یا previous_response_id و با `store=false` استفاده کند. `max_output_tokens=8192` (`MaxOutputTokenCount`) نقطهٔ آغاز ارزیابی است و بودجهٔ خروجی/Reasoning را پوشش می‌دهد؛ کفایت آن باید سنجیده شود. Reasoning تا benchmark/Eval مقدار قطعی ندارد و تنظیم مؤثر Feature-level ثبت شود. temperature صفر الزام قبلی حذف می‌شود؛ تنظیم بدون دلیل و بررسی پشتیبانی مدل تثبیت نشود. retry policy صفر و timeout شبکه در همان بودجهٔ باقی‌مانده باشند.

مدل اولیه تضمین Quality Gate نیست؛ شکست آستانه‌های Spec مستلزم بازبینی مدل براساس Eval است، بدون fallback خودکار. snapshot نسخه‌دار مستقل در منبع رسمی بررسی‌شده یافت نشد؛ نام حدسی ایجاد نشود. مدل درخواستی و مدل واقعی پاسخ Provider، تاریخ Eval، نسخهٔ Prompt/Schema و تنظیمات مؤثر در شواهد آینده ثبت شوند. APIهای Responses این SDK با `OPENAI001` experimental هستند؛ suppression آینده فقط در محل مصرف Infra.AI محدود شود. هیچ Client، Type یا Exception SDK به Application وارد نشود. [منبع مدل](https://developers.openai.com/api/docs/models/gpt-6-luna) و [API رسمی همان نسخهٔ SDK](https://github.com/openai/openai-dotnet/blob/OpenAI_2.14.0/api/released/net10.0/OpenAI.Responses.net10.0.cs).

دستور ثابت system/developer بررسی فارسی را درخواست کند: فقط چهار دستهٔ گزارش، پیشنهاد کمکی، عدم تأیید/رد قطعی ادعای بی‌پشتوانه، عدم ساخت منبع، اعلام needs_input برای ارزیابی متوقف‌شده و evidence دقیق. متن پست در user message به‌شکل object سریال‌شده با دو key title/body قرار گیرد؛ delimiter دست‌ساز یا interpolation متن به system instruction استفاده نشود. دستورهای داخل متن صرفاً داده‌اند؛ هیچ tool، write، web یا retrieval در تماس موجود نیست. این تفکیک prompt ریسک دستور تزریقی را کاهش می‌دهد؛ تضمین امنیت، نبود اختیار اجرای عملیات و کنترل Application است.

`text.format` در Responses API از نوع `json_schema`، با `name=content_review_v1`، `strict=true` و `schema` دقیق [model-output.schema.json](model-output.schema.json) باشد؛ JSON Mode ساده و Prompt-only JSON استفاده نشوند. در SDK نسخهٔ 2.14.0، `CreateResponseOptions.TextOptions` از نوع `ResponseTextOptions` و property آن `TextFormat` با `ResponseTextFormat.CreateJsonSchemaFormat(jsonSchemaFormatName: "content_review_v1", jsonSchema: schema, jsonSchemaIsStrict: true)` تنظیم شود؛ schema از artifact به BinaryData تبدیل شود. `StoredOutputEnabled=false` معادل Wire `store=false` است. artifact همچنان Source of Truth شکل Wire است؛ برابری serialization SDK با آن در transport test بررسی شود. [factory رسمی نسخهٔ منتخب](https://github.com/openai/openai-dotnet/blob/OpenAI_2.14.0/OpenAI.Responses/src/Custom/ResponseTextFormat.cs)، [قرارداد Wire رسمی](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=responses). نام Schema metadata فنی است؛ propertyهای schemaVersion/postId/source/freshness داخل خروجی مدل نیستند. تغییر Schema یا prompt مستلزم Eval مجدد است.

### تفسیر پروتکل Responses

Infra.AI پیش از deserialize، status و output items را بررسی کند. refusal صریح از content item نوع `refusal` به Refused تبدیل و fragment گزارش همراه آن کنار گذاشته شود. `incomplete`/`incomplete_details`، پیام ناقص، فقدان متن یا چند خروجی متنی که یک JSON object یکتا نمی‌سازند InvalidOutput هستند؛ blind concatenation، repair و استخراج code fence انجام نشود. Tool Calling یا output form غیرمنتظره نیز InvalidOutput است. item فنی `reasoning` در صورت تولید مدل، گزارش یا tool نیست و به Application/HTTP/log منتقل نشود؛ تنها پیام نهایی مجاز مبنای candidate است. خطای Provider/transport به Unavailable و پایان deadline به Timeout تبدیل شود. هیچ refusal/failure با completed، needs_input یا issues خالی پنهان نشود.

## دو سطح validation

1. Infra.AI شکل wire را کنترل می‌کند: status پاسخ و کامل‌بودن output، content item نوع refusal، سقف ۲۵۶ KiB و depth ۱۶، JSON object یکتا، propertyهای required و بدون unknown/duplicate، type و enum دقیق؛ خروجی بدون repair deserialize شود.
2. Application هر candidate را مستقل کنترل می‌کند: اجزای غیر whitespace، suggestion nullable یا غیرخالی، سازگاری completed/needs_input با missingInformation و وجود exact ordinal همهٔ evidenceها در title/body snapshot. fake gateway هم از این validation عبور می‌کند.

Schema عمداً شرط completed⇒empty و needs_input⇒nonempty را داخل ساختار پیچیده اعمال نمی‌کند؛ Application آن‌ها را الزامی می‌کند. اعتبار ساختاری/شاهد، صحت معنایی ادعا یا کیفیت نگارش را اثبات نمی‌کند. در هر نقض گزارش دور انداخته و ai_review_invalid_output برگردد؛ fallback به متن آزاد یا گزارش خالی ممنوع است.

## تنظیم و Composition

| تنظیم | طراحی |
| --- | --- |
| AI:ContentReview:Enabled | false پیش‌فرض؛ true برای تماس واقعی |
| AI:ContentReview:Model | gpt-6-luna برای ارزیابی اولیه؛ alias بدون snapshot حدسی؛ تغییر نیازمند Eval تازه |
| AI:ContentReview:ApiKey | secret محیط/secret storage؛ بدون مقدار در appsettings یا اسناد |
| deadline درخواست | ثابت سقف ۶۰ ثانیه، بودجهٔ کار ۵۹ و Provider حداکثر ۵۰؛ از ورودی Client قابل افزایش نیست |

Api به AI فقط برای Composition reference می‌دهد؛ AI به Application و بدون DataAccess reference است. در حالت disabled یا تنظیم ناقص، gateway unavailable بدون تماس شبکه‌ای ثبت شود؛ host و CRUD قبلی اجرا شوند. startup مدل را فراخوانی نکند. singleton SDK/adapter thread-safe و request options مستقل؛ service و reader scoped باشند. log/trace شامل متن، شواهد، key، user token، raw refusal یا raw response نباشد.
