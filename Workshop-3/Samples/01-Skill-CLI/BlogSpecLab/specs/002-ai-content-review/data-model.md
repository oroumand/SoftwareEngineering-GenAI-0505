# مدل دادهٔ بررسی کمکی محتوا

این مدل طراحی است؛ جدول یا کد ایجاد نمی‌کند. [Post موجود](../001-blog-post-management/data-model.md) منبع محتوا و گردش کار باقی می‌ماند. هیچ ارتباطی با ReviewDecision تأیید انسانی ایجاد نمی‌شود.

## PostContentReviewSnapshot — داخلی و immutable

| فیلد | نوع | قاعده |
| --- | --- | --- |
| PostId | Guid | شناسهٔ موجود و متعلق به درخواست‌کننده |
| OwnerId | string | فقط کنترل سروری؛ بیرون ارسال نشود |
| Title / Body | string | متن دقیق ذخیره‌شده؛ غیر whitespace؛ بدون normalize یا truncation |
| ContentRevision | int | revision موجود Post؛ همراه متن، مبنای این بررسی |
| StatusAtStart | PostStatus | فقط Draft یا ChangesRequested |
| CapturedAt | UTC timestamp | زمان گرفتن snapshot؛ توسط سرور |

projection خواندن علاوه بر این فیلدها IsDeleted دارد تا حذف نرم کنترل شود؛ پست ناموجود/حذف‌شده snapshot قابل بررسی نمی‌سازد. ReviewId داخلی جدید، در صورت نیاز correlation، فقط شناسهٔ درخواست حافظه‌ای است و آرشیو یا هویت بیرونی ایجاد نمی‌کند. snapshot، userId، postId و revision از مرز gateway عبور نمی‌کنند؛ gateway فقط Title/Body را می‌بیند.

## ContentReviewReport — candidate مدل، سپس گزارش اعتبارسنجی‌شده

| فیلد wire | نوع | قاعده |
| --- | --- | --- |
| status | enum | completed یا needs_input؛ حساس به حروف |
| summary | string | غیر whitespace؛ توضیح فارسی کمکی، بدون ادعای تأیید/انتشار |
| issues | array of ContentReviewIssue | الزامی؛ خالی مجاز؛ در needs_input نیز قابل پرشدن |
| missingInformation | array of MissingReviewInformation | completed: خالی؛ needs_input: حداقل یک مورد |

مدل فقط این چهار فیلد را تولید می‌کند. metadata اتصال به پست و freshness توسط Application ساخته می‌شود؛ مدل اجازه ندارد وضعیت پست یا صحت metadata را تعیین کند.

### ContentReviewIssue

| فیلد | نوع | قاعده |
| --- | --- | --- |
| category | enum | writing، ambiguity، internal_inconsistency یا unsupported_claim |
| field | enum | title یا body |
| evidence | string | نقل‌قول غیرخالی دقیق و ordinal موجود در field snapshot |
| explanation | string | غیر whitespace؛ توضیح احتمال مشکل |
| suggestion | string یا null | property همیشه موجود؛ null یعنی پیشنهاد ندارد؛ رشتهٔ غیرخالی صرفاً توصیه |

writing نگارش؛ ambiguity ابهام بیان؛ internal_inconsistency ناسازگاری درونی؛ unsupported_claim نیاز به پشتوانه برای ادعا است. category آخر حکم رد قطعی ادعا نیست. اگر کمبود پشتوانه مانع ارزیابی است، missingInformation و needs_input هم لازم‌اند؛ این قاعدهٔ معنایی در prompt و ارزیابی انسانی کنترل می‌شود.

چند issue با یک evidence مجاز است؛ هرکدام explanation مستقل دارد. evidence برای تعیین شاهد است، نه offset یکتا؛ در صورت تکرار نقل‌قول، مصرف‌کننده می‌تواند همهٔ رخدادها را نشان دهد. offset یا بازنویسی خودکار به قرارداد اضافه نمی‌شود.

### MissingReviewInformation

| فیلد | نوع | قاعده |
| --- | --- | --- |
| field | enum | title یا body |
| evidence | string | بخش مبهم/ادعا، شاهد دقیق موجود در همان field |
| reason | string | دلیل غیرخالی اینکه چرا ارزیابی ممکن نیست |
| requestedInformation | string | اطلاعات غیرخالی لازم؛ مثلاً منبع و معیار مقایسه |

اطلاعات مفقود ساخته نشوند. ورود پاسخ کاربر فقط از مسیر ویرایش و ذخیرهٔ مستقل عنوان/متن و درخواست بررسی تازه است؛ این مدل endpoint مکالمه یا پاسخ به سؤال ندارد.

## ContentReviewEnvelope — ساخته‌شده توسط سرور

| فیلد | نوع | قاعده |
| --- | --- | --- |
| schemaVersion | string | content_review_v1 |
| postId | Guid | از snapshot سروری، نه مدل |
| source.contentRevision | int | revision محتوای بررسی‌شده، هرگز revision تازه جایگزین نشود |
| source.capturedAt | UTC timestamp | زمان snapshot |
| freshness.checkedAt | UTC timestamp | زمان بازخوانی سروری پیش از تحویل |
| freshness.isCurrentContent | bool | فقط در checkedAt؛ revision و متن دقیق برابر با snapshot |
| freshness.requiresNewReview | bool | نقیض isCurrentContent |
| report | ContentReviewReport | فقط گزارش معتبر، بدون refusal/failure |

در محتواهای ویرایش‌شده و سپس بازگردانده‌شده به متن قبلی، افزایش revision همچنان نیاز بررسی تازه را نشان می‌دهد. Version گردش کار در freshness دخیل نیست؛ submit بدون تغییر متن آن را کهنه نمی‌کند. پس از دریافت، Client باید source.contentRevision را با جزئیات تازهٔ پست مقایسه کند؛ checkedAt تضمین آینده نیست. گزارش قدیمی برای snapshot معتبر است و نباید برای متن فعلی نمایش داده شود.

## Outcome و چرخهٔ عمر درخواست

| حالت | انتقال / نتیجه |
| --- | --- |
| Received | deadline از دریافت HTTP؛ احراز هویت و سپس precheck |
| Rejected | ورودی/مجوز/وجود/وضعیت نامعتبر؛ Provider فراخوانی نشود |
| SnapshotCaptured | title/body/revision ثابت؛ بدون lock یا write transaction |
| ProviderPending | یک تماس با token و بودجهٔ باقی‌مانده |
| Candidate | parsing و validation؛ کامل‌بودن گزارش هنوز به‌تنهایی کافی نیست |
| Refused | failure متمایز؛ هیچ envelope گزارش معتبر ندارد |
| TechnicalFailure / InvalidOutput | failure قابل پردازش؛ هرگز completed با آرایهٔ خالی نشود |
| Rechecked | مجوز و وجود/مالکیت دوباره از منبع تازه خوانده شوند |
| DeliveredCurrent / DeliveredStale | report معتبر با freshness؛ هیچ نوشتنی ندارد |
| TimedOut / Cancelled | حالت پایانی؛ پاسخ دیررس کنار گذاشته شود؛ درخواست تازه مستقل است |

حذف پست در recheck باعث post_not_found، لغو Permission باعث forbidden و اختلال DB باعث storage_unavailable می‌شود؛ حتی candidate معتبر تحویل نمی‌شود. مجوز قبل از جزئیات پست کنترل شود؛ غیرمالک نیز 404 یکسان بگیرد. اگر چند خطا هم‌زمان باشد deadline/قطع Client بر تحویل موفق مقدم است؛ سپس مجوز، وجود/مالکیت و بعد outcome گزارش. این ترتیب هیچ اطلاعات پست را به درخواست‌کنندهٔ بدون مجوز افشا نمی‌کند.

## Persistence و روابط

درخواست و گزارش فقط عمر همان درخواست/پاسخ را دارند؛ Aggregate، DbSet، Migration یا آرشیو تازه وجود ندارد. تنها رابطهٔ منطقی envelope با PostId و ContentRevision است و این metadata در سرور ساخته می‌شود. تمام مسیرها Title، Body، OwnerId، Status، ContentRevision، Version، IsDeleted، CurrentReviewId، Reviews، دلیل برگشت و Publication را دست‌نخورده نگه می‌دارند. آزمون باید این invariant را برای موفقیت، needs_input، refusal، failure و درخواست تکراری اثبات کند؛ تغییرات مستقل کاربر در حین آزمون از اثر AI تفکیک شوند.
