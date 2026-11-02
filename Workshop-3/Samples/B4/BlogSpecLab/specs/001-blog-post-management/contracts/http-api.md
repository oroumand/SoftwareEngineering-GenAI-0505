# قرارداد HTTP API

قرارداد مرجع این Feature است؛ انطباق API پیاده‌سازی‌شده در [شواهد اعتبارسنجی](../validation.md) ثبت شده است. مرجع [مدل داده](../data-model.md) و [راهنمای اجرا](../quickstart.md) است. مسیر پایه `/api`؛ Body و پاسخ موفق دارای محتوا `application/json` و خطا `application/problem+json` است. پاسخ 204 محتوا ندارد. شناسه‌ها UUID، نام فیلدها camelCase و زمان‌ها ISO 8601 در UTC هستند. عنوان، متن و پیام انسانی فارسی یا Unicode را پشتیبانی می‌کنند.

## هویت و ورودی قابل اعتماد

مسیرهای `/api/author` و `/api/editor` به `Authorization: Bearer <credential>` معتبر نیاز دارند؛ انتخاب Credential در [research.md](../research.md) ثبت می‌شود. OwnerId، وضعیت، نقش، تأییدکننده و منتشرکننده ورودی قابل انتساب توسط Client نیستند. فیلدهای اضافی در Body رد می‌شوند؛ درخواست مستقیم تغییر وضعیت به Published مسیر عمومی ندارد. Application مجوز فعلی و مالکیت را مستقل از کنترل اولیهٔ HTTP بررسی می‌کند.

## عملیات

| روش و مسیر | ورودی | موفقیت | دسترسی / رفتار |
| --- | --- | --- | --- |
| POST `/api/author/posts` | `{title, body}` | 201، PostView، Location و ETag | Author؛ ایجاد Draft با مالک هویت فعلی |
| GET `/api/author/posts` | ندارد | 200، آرایهٔ PostView | فقط پست‌های حذف‌نشدهٔ خود Author، شامل Published |
| GET `/api/author/posts/{id}` | ندارد | 200، PostView و ETag | Author مالک |
| PUT `/api/author/posts/{id}` | `{title, body}` و If-Match | 200، PostView و ETag | مالک؛ فقط منتشرنشده |
| DELETE `/api/author/posts/{id}` | If-Match | 204 | مالک؛ فقط منتشرنشده |
| POST `/api/author/posts/{id}/submit` | If-Match؛ Body ندارد | 200، PostView و ETag | مالک؛ Draft یا ChangesRequested |
| GET `/api/editor/posts` | ندارد | 200، آرایهٔ PostView | Editor؛ PendingReview و Approved |
| GET `/api/editor/posts/{id}` | ندارد | 200، PostView و ETag | Editor؛ PendingReview و Approved |
| POST `/api/editor/posts/{id}/request-changes` | `{reason}` و If-Match | 200، PostView و ETag | Editor؛ PendingReview |
| POST `/api/editor/posts/{id}/approve` | If-Match؛ Body ندارد | 200، PostView و ETag | Editor غیرمالک؛ PendingReview |
| POST `/api/editor/posts/{id}/publish` | If-Match؛ Body ندارد | 200، PostView و ETag | مجوز فعلی Editor؛ Approved با تأیید معتبر؛ تکرار مجاز مطابق بخش رقابت |
| GET `/api/posts` | ندارد | 200، آرایهٔ PublicPostView | ناشناس؛ فقط Published و حذف‌نشده |
| GET `/api/posts/{id}` | ندارد | 200، PublicPostView | ناشناس؛ فقط Published و حذف‌نشده |

فهرست‌ها در این نسخه بدون صفحه‌بندی، به ترتیب Id مرتب هستند؛ محدودیت ظرفیت تعریف نشده و صفحه‌بندی قابلیت جدید این گام نیست. صف Editor پست خود Editor را نیز نشان می‌دهد، ولی Approve همان پست رد می‌شود. دسترسی Editor به وضعیت‌های دیگر از مسیر جزئیات صف 404 است؛ دسترسی مالک دو نقشی از مسیر Author انجام می‌شود.

## پاسخ‌ها

PostView شامل `id, ownerId, title, body, status, contentRevision, version, lastReview, publication, allowedActions` است. `lastReview` تهی یا شامل `decision, reviewerId, decidedAt, contentRevision, reason` است؛ Snapshot داخلی تاریخچه در API عمومی نمایش داده نمی‌شود. `publication` تهی یا شامل `publisherId, publishedAt, approvedReviewId` است. allowedActions آرایهٔ نام اقدامات مجاز همین کاربر برای راهنمایی است و مجوز عملیات بعدی را تضمین نمی‌کند. مقادیر status دقیقاً نام‌های [مدل داده](../data-model.md) هستند.

`allowedActions` فقط شامل مقادیر زیر است؛ آرایه بدون تکرار و با همین ترتیب ثابت تولید می‌شود:

| مقدار | شرط درج |
| --- | --- |
| `edit` | کاربر مالک پست، دارای `CanWrite` فعلی و پست غیر `Published` باشد. |
| `delete` | کاربر مالک پست، دارای `CanWrite` فعلی و پست غیر `Published` باشد. |
| `submit` | کاربر مالک پست، دارای `CanWrite` فعلی و وضعیت `Draft` یا `ChangesRequested` باشد. |
| `requestChanges` | کاربر دارای `CanReview` فعلی و وضعیت `PendingReview` باشد. |
| `approve` | کاربر دارای `CanReview` فعلی، وضعیت `PendingReview` و کاربر غیرمالک باشد. |
| `publish` | کاربر دارای `CanPublish` فعلی، وضعیت `Approved` و تأیید معتبر محتوای فعلی موجود باشد. |

برای حساب غیرفعال یا پست `Published`، آرایه خالی است. استثنای Publish تکراری در بخش رقابت همچنان معتبر است و در `allowedActions` به‌عنوان اقدام جدید نمایش داده نمی‌شود. این آرایه فقط راهنمای Client است و مجوز اجرای عملیات بعدی محسوب نمی‌شود؛ تمام Permissionها، مالکیت، وضعیت و پیش‌شرط‌های عملیات هنگام اجرای درخواست دوباره در Application بررسی می‌شوند.

PublicPostView فقط `id, title, body, publishedAt` دارد؛ مالک، دلیل برگشت، تصمیم‌ها و Version مدیریتی عمومی نیستند. تمام پاسخ‌های مدیریتی و خطاها `Cache-Control: no-store` دارند؛ درخواست عمومی شناسهٔ منتشرنشده یا حذف‌شده 404 یکسان می‌دهد و محتوا افشا نمی‌کند.

## رقابت و تکرار

ETag قوی به‌شکل UUID داخل کوتیشن، نمایندهٔ Version است. تمام فرمان‌های پس از ایجاد به یک `If-Match` مشخص نیاز دارند؛ wildcard یا چند مقدار پذیرفته نمی‌شود. نبود آن 428 و قالب نامعتبر 400 است. Version قدیمی برای کاربر مجاز 412 با `code=stale_version` می‌دهد؛ در رقابت پس از خواندن و هنگام Commit نیز همین نتیجه برقرار است. تغییر وضعیت ناسازگار با Version فعلی 409 است. در خطای تعارض فقط پس از بررسی دسترسی، `currentStatus` و `currentVersion` و پیام «دوباره جزئیات را بخوانید» داده می‌شوند؛ محتوای پست در خطا قرار نمی‌گیرد.

استثنای محدود Publish تکراری: پس از هویت و مجوز فعلی و وجود If-Match معتبر از نظر قالب، اگر پست هم‌اکنون Published و دارای اطلاعات انتشار معتبر است، 200 با همان انتشار نخست بازگردد، حتی اگر Version ارائه‌شده قدیمی باشد. این پاسخ هیچ Commit جدیدی ندارد. دو Publish هم‌زمان: بازنده فقط بعد از بازخوانی و بررسی دوبارهٔ مجوز می‌تواند انتشار ثبت‌شده را برگرداند؛ اگر علت شکست چیز دیگری است 412 می‌دهد. در هیچ فرمان دیگری retry خودکار کسب‌وکاری انجام نمی‌شود.

## خطاها

ProblemDetails شامل `type, title, status, detail, instance, code` است؛ type برای نسخهٔ اولیه `about:blank` و پیام انسانی فارسی است. خطای فیلدی می‌تواند `errors` شامل title، body یا reason داشته باشد. `code` برای Client ثابت است.

| HTTP | code نمونه | کاربرد |
| --- | --- | --- |
| 400 | invalid_input | JSON نامعتبر، متن خالی، دلیل خالی، فیلد ممنوع یا قالب If-Match |
| 401 | unauthenticated | Credential غایب، نامعتبر یا منقضی؛ همراه WWW-Authenticate |
| 403 | forbidden / self_approval_forbidden | هویت معتبر فاقد مجوز عملیات یا خودتأییدی |
| 404 | post_not_found | ناموجود، حذف‌شده یا خارج از محدودهٔ مشاهدهٔ کاربر |
| 409 | invalid_state / published_post_immutable / approval_required | وضعیت ناسازگار، تغییر پس از انتشار، تأیید نامعتبر |
| 412 | stale_version | درخواست بر نسخهٔ قدیمی یا تعارض اتمی ذخیره |
| 428 | version_required | نبود If-Match |
| 503 | storage_unavailable | قفل/اختلال پایگاه داده پس از retry محدود زیرساخت؛ بدون تغییر جزئی |

کاربر فاقد دسترسی به پست، قبل از پاسخ تعارض یا وضعیت، 404 می‌گیرد؛ کاربر فاقد نقش مسیر 403 می‌گیرد. خطای غیرمنتظره 500 با پیام عمومی و بدون StackTrace، Credential یا محتوای حساس است. پیام خطا و allowedActions باید در ارزیابی SC-005 کمک کنند؛ تحقق این معیار نیازمند ارزیابی انسانی است.
