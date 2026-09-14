# مدل داده: نگارش و انتشار پست وبلاگ

## مرز Aggregate

`Post` Aggregate Root و تنها Aggregate این قابلیت است. تمام تغییر وضعیت از متدهای عمومی
معنادار آن عبور می‌کند. Repository فقط Aggregate کامل را بارگذاری و ذخیره می‌کند؛ هیچ
جزء داخلی آن مستقل ذخیره یا تغییر داده نمی‌شود.

## Post

| ویژگی | نوع مفهومی | الزام و معنا |
|---|---|---|
| `Id` | `PostId` | شناسهٔ یکتای پایدار پست؛ هنگام ایجاد تولید می‌شود |
| `AuthorId` | `AuthorId` | هویت مالک ایجادکننده؛ الزامی و پس از ایجاد تغییرناپذیر |
| `Title` | `PostTitle` | الزامی؛ خالی یا فقط فاصله مجاز نیست |
| `Content` | `PostContent` | الزامی؛ خالی یا فقط فاصله مجاز نیست |
| `Status` | `PostStatus` | هنگام ایجاد `Draft`؛ تنها گذار مجاز به `Published` |
| `PublishedAt` | زمان UTC اختیاری | برای Draft وجود ندارد؛ هنگام انتشار موفق دقیقاً یک‌بار ثبت می‌شود |

`version_token` و `xmin` ویژگی Domain نیستند. `version_token` یک مقدار تصادفی Shadow در
Infrastructure است که به ETag opaque تبدیل می‌شود؛ `xmin` ستون سیستمی PostgreSQL و Shadow
Concurrency Token برای Commit اتمیک است. هیچ‌یک در Domain یا بدنهٔ HTTP ظاهر نمی‌شوند.

## Value Objectها

### PostId

- شناسهٔ یکتا برای آدرس‌دهی پست است.
- مقدار پیش‌فرض یا نامعتبر در مرز ایجاد پذیرفته نمی‌شود.
- جزئیات تولید شناسه رفتار محصول نیست و در پیاده‌سازی انتخاب می‌شود؛ طرح UUID را برای
  سازگاری مستقل از پایگاه داده در نظر می‌گیرد.

### AuthorId

- نمایش درون‌سامانه‌ای هویت معتبر دریافتی از قابلیت احراز هویت بیرونی است.
- مقدار تهی یا فقط فاصله معتبر نیست.
- برابری آن مبنای قاعدهٔ مالکیت است.

### PostTitle

- رشتهٔ الزامی است.
- مقدار خالی یا مقداری که تمام آن فاصله است ایجاد نمی‌شود.
- Specification محدودیت طول، قالب یا محتوای دیگری تعیین نکرده و مدل نیز اضافه نمی‌کند.

### PostContent

- رشتهٔ الزامی است.
- مقدار خالی یا مقداری که تمام آن فاصله است ایجاد نمی‌شود.
- Specification محدودیت طول، قالب یا محتوای دیگری تعیین نکرده و مدل نیز اضافه نمی‌کند.

### PostStatus

- مقادیر مجاز: `Draft` و `Published`.
- مقدار اولیه همیشه `Draft` است.
- گذار معکوس یا وضعیت دیگری وجود ندارد.

## Invariantها

1. `AuthorId`، `Title` و `Content` معتبر برای ایجاد الزامی‌اند.
2. Post تازه همیشه `Draft`، بدون `PublishedAt` و متعلق به نویسندهٔ جاری است.
3. مالکیت پس از ایجاد تغییر نمی‌کند.
4. فقط مالک می‌تواند درخواست ویرایش یا انتشار را روی Aggregate اجرا کند.
5. فقط `Draft` قابل ویرایش یا انتشار است.
6. ویرایش باید Title و Content معتبر را با هم اعمال کند؛ شکست هر اعتبارسنجی کل تغییر را
   لغو و حالت قبلی را حفظ می‌کند.
7. انتشار موفق وضعیت را به `Published` تغییر و زمان قابل‌اعتماد را در `PublishedAt` ثبت
   می‌کند.
8. `Published` قابل ویرایش، انتشار دوباره یا بازگشت به `Draft` نیست.
9. شکست مالکیت، اعتبارسنجی، وضعیت یا Concurrency هیچ تغییر جزئی در Aggregate پایدارشده
   باقی نمی‌گذارد.

## عملیات Domain

### CreateDraft

**ورودی**: `PostId`، `AuthorId`، `PostTitle`، `PostContent`

**پیش‌شرط**: همهٔ Value Objectها معتبر باشند.

**پس‌شرط**: Post با مالک ثابت، `Draft` و `PublishedAt = null` ایجاد می‌شود.

### EditDraft

**ورودی**: `ActorAuthorId`، `PostTitle` جدید، `PostContent` جدید

**پیش‌شرط**: عامل مالک باشد، وضعیت `Draft` باشد و هر دو مقدار جدید معتبر باشند.

**پس‌شرط موفق**: Title و Content با هم تغییر و Status برابر `Draft` باقی می‌ماند.

**پس‌شرط شکست**: تمام حالت قبلی بدون تغییر می‌ماند.

### Publish

**ورودی**: `ActorAuthorId` و زمان جاری قابل‌اعتماد UTC

**پیش‌شرط**: عامل مالک، وضعیت `Draft` و Title/Content موجود معتبر باشند.

**پس‌شرط موفق**: Status برابر `Published` و `PublishedAt` برابر زمان ورودی می‌شود.

**پس‌شرط شکست**: حالت قبلی بدون تغییر می‌ماند.

## گذار وضعیت

```text
CreateDraft
    │
    ▼
  Draft ─── EditDraft ───► Draft
    │
    └────── Publish ─────► Published
                              │
                              └── هیچ گذار خروجی مجاز نیست
```

| حالت فعلی | عملیات | نتیجه |
|---|---|---|
| وجود ندارد | CreateDraft معتبر | `Draft` |
| `Draft` | EditDraft توسط مالک با محتوای معتبر | `Draft` با محتوای جدید |
| `Draft` | Publish توسط مالک | `Published` با زمان انتشار |
| `Draft` | عملیات نویسندهٔ غیرمالک | رد؛ بدون تغییر |
| `Published` | EditDraft، Publish یا بازگشت | رد؛ بدون تغییر |

## مدل ماندگاری

### جدول منطقی `posts`

| ستون | الزام | توضیح |
|---|---|---|
| `id` | Primary Key، غیرتهی | `PostId` |
| `author_id` | غیرتهی | مالک ثابت |
| `title` | غیرتهی | مقدار `PostTitle` |
| `content` | غیرتهی | مقدار `PostContent` |
| `status` | غیرتهی | `Draft` یا `Published` |
| `published_at` | اختیاری | فقط برای `Published` مقدار دارد |
| `version_token` | غیرتهی، Shadow | Version opaque برای تولید و اعتبارسنجی ETag؛ هنگام هر Mutation موفق تغییر می‌کند |
| `xmin` | سیستمی | توکن Optimistic Concurrency؛ در مدل Domain ظاهر نمی‌شود |

Constraint پایگاه داده باید سازگاری Status و PublishedAt را نیز پشتیبان Domain نگه دارد:
Draft بدون زمان انتشار و Published دارای زمان انتشار. Validation اصلی همچنان داخل Domain
باقی می‌ماند.

### Queryهای خوانندگان

- List: فقط `Published`، شامل همهٔ پست‌های منتشرشده و مرتب‌شده با `PublishedAt` نزولی.
- Detail: فقط Post با Status برابر `Published`؛ Draft مانند منبع غیرقابل مشاهده است.
- برای `PublishedAt` برابر، قاعدهٔ ثانویه تعریف نمی‌شود تا تصمیم محصولی تازه‌ای افزوده
  نشود.

## مدل Concurrency و نسخهٔ کلاینت

1. هنگام ایجاد Draft، Infrastructure یک `version_token` تصادفی ذخیره و ETag opaque متناظر
   را در Header پاسخ ایجاد برمی‌گرداند.
2. Client باید همان ETag را در `If-Match` هر EditDraft یا PublishDraft ارسال کند؛ Header
   مفقود برای Draft قابل‌دسترس به `428 Precondition Required` تبدیل می‌شود.
3. پس از اطمینان از قابل‌دسترس‌بودن Post برای عامل، Repository `version_token` موردانتظار
   را با مقدار ذخیره‌شده مقایسه می‌کند. عدم تطبیق به `412 Precondition Failed` تبدیل و
   Domain Action اجرا نمی‌شود.
4. در تطبیق موفق، رفتار Domain روی نسخهٔ بارگذاری‌شده اجرا می‌شود. قبل از Commit، یک
   `version_token` تصادفی تازه تعیین می‌شود.
5. Update با شرط Original `xmin` Commit می‌شود. نخستین Commit، `xmin` و `version_token` را
   تغییر می‌دهد و ETag جدید به‌دست می‌آید.
6. اگر Commit دیگری پس از بررسی `If-Match` زودتر موفق شود، Update عملیات بازنده هیچ Rowای
   تغییر نمی‌دهد و `DbUpdateConcurrencyException` به `412 Precondition Failed` تبدیل
   می‌شود.
7. فقط EditDraft موفق ETag تازه را در پاسخ برمی‌گرداند. PublishDraft ETag جدیدی برای
   ادامهٔ ویرایش ارائه نمی‌کند، زیرا Post منتشرشده در این قابلیت قابل ویرایش نیست.
8. هیچ Merge، Retry خودکار یا بازنویسی نتیجهٔ Commit نخست انجام نمی‌شود.
