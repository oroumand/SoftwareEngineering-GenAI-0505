# قرارداد HTTP بررسی کمکی محتوا

این مسیر طراحی‌شده هنوز پیاده‌سازی نشده است. قراردادهای [قابلیت اول](../../001-blog-post-management/contracts/http-api.md)، Authentication موجود و ProblemDetails با code پایدار حفظ می‌شوند.

## درخواست

`POST /api/author/posts/{id}/content-review`

- Authorization: Bearer credential معتبر مسیر موجود؛ نقش Editor تنها کافی نیست.
- Body خالی؛ title/body یا دستور بررسی از Client پذیرفته نشود. body غیرخالی یا id نامعتبر: 400/invalid_input؛ احراز هویت همچنان قبل از use case است.
- If-Match لازم نیست؛ header در این عملیات read-only استفاده نمی‌شود. OpenAPI این مسیر آن را required معرفی نکند. قرارداد mutationهای قبلی دست‌نخورده است.
- Application Permission فعلی CanWrite/IsActive، مالکیت، وجود و حذف‌نبودن، Draft/ChangesRequested و غیرخالی بودن محتوا را قبل از تماس AI کنترل کند.
- هر درخواست snapshot تازهٔ متن ذخیره‌شده می‌گیرد؛ هیچ cache، آرشیو، polling یا درخواست background ایجاد نمی‌شود.
- تمام پاسخ‌ها `Cache-Control: no-store`؛ گزارش JSON UTF-8 و خطا `application/problem+json` باشد.

## پاسخ 200

دو status معتبر report فقط completed و needs_input هستند. تعریف فیلدها در [data-model](../data-model.md) و شکل گزارش مدل در [Schema](model-output.schema.json) است؛ envelope را سرور می‌سازد.

نمونهٔ تکمیل‌شده بدون مشکل:

```json
{
  "schemaVersion": "content_review_v1",
  "postId": "10000000-0000-0000-0000-000000000001",
  "source": {
    "contentRevision": 1,
    "capturedAt": "2026-10-12T10:00:00Z"
  },
  "freshness": {
    "checkedAt": "2026-10-12T10:00:04Z",
    "isCurrentContent": true,
    "requiresNewReview": false
  },
  "report": {
    "status": "completed",
    "summary": "در این بررسی مشکل مشخصی شناسایی نشد؛ این نتیجه تضمین صحت یا تأیید انتشار نیست.",
    "issues": [],
    "missingInformation": []
  }
}
```

نمونهٔ report برای متن ذخیره‌شدهٔ «طبق گزارش، این روش بهترین نتیجه را دارد»:

```json
{
  "status": "needs_input",
  "summary": "ارزیابی ادعا به منبع و معیار مقایسه نیاز دارد.",
  "issues": [
    {
      "category": "unsupported_claim",
      "field": "body",
      "evidence": "این روش بهترین نتیجه را دارد",
      "explanation": "پشتوانه و معیار مقایسه مشخص نشده است؛ دربارهٔ درستی ادعا حکم داده نمی‌شود.",
      "suggestion": null
    }
  ],
  "missingInformation": [
    {
      "field": "body",
      "evidence": "طبق گزارش",
      "reason": "گزارش مرجع مشخص نیست.",
      "requestedInformation": "منبع گزارش و معیار مقایسهٔ نتیجه را در متن ذخیره‌شده مشخص کنید."
    }
  ]
}
```

این report در همان envelope پاسخ اول قرار می‌گیرد. issues خالی در needs_input مجاز است؛ completed الزاماً missingInformation خالی دارد. هر شاهد باید در field محتوای snapshot باشد. اصلاح و ارسال با اقدام مستقل نویسنده انجام می‌شوند.

## هم‌زمانی و تحویل

پیش از تحویل گزارش، Permission و وجود/مالکیت/حذف‌نبودن از دادهٔ تازه دوباره کنترل شوند. تغییر متن حین تماس: 200 با source revision قدیمی، isCurrentContent=false و requiresNewReview=true؛ هیچ گزارشی برای متن جدید ساخته نمی‌شود. تغییر صرف وضعیت بدون تغییر محتوا freshness را stale نمی‌کند و هیچ وضعیت قدیمی بازسازی نمی‌شود. حذف یا لغو مجوز حتی پس از candidate معتبر، پاسخ گزارش را متوقف می‌کند.

freshness فقط در checkedAt است؛ برای تغییر پس از دریافت، مصرف‌کننده revision پست تازه را با source.contentRevision مقایسه کند و نتیجهٔ قدیمی را بررسی متن فعلی معرفی نکند. هیچ ETag از گزارش به‌عنوان Version قابل mutation ساخته نمی‌شود؛ برای ویرایش/submit همچنان باید جزئیات پست و ETag موجود خوانده شود.

## خطاها و outcomeها

ProblemDetails شامل type=about:blank، title/status/detail/instance و code پایدار با پیام فارسی امن است. هیچ متن پست، raw output، refusal خام، خطای حساب/SDK، credential یا stack trace در خطا نباشد.

| HTTP | code | معنای قابل پردازش |
| --- | --- | --- |
| 400 | invalid_input | body ممنوع، شناسه یا محتوای ذخیره‌شده نامعتبر |
| 401 | unauthenticated | هویت غایب/نامعتبر؛ WWW-Authenticate: Bearer |
| 403 | forbidden | هویت معتبر فاقد Permission فعلی؛ شامل لغو حین بررسی |
| 404 | post_not_found | ناموجود/حذف‌شده/غیرمالک؛ پاسخ یکسان بدون افشا |
| 409 | invalid_state | وضعیت آغاز خارج از Draft/ChangesRequested |
| 422 | ai_review_refused | Provider صریحاً از بررسی امتناع کرده؛ گزارش معتبر نیست |
| 502 | ai_review_invalid_output | ساختار، شاهد، وضعیت یا پاسخ کامل قابل قبول نیست |
| 503 | ai_review_unavailable | خاموش/تنظیم ناقص AI، خطای شبکه، محدودیت حساب/سهمیه یا اختلال Provider |
| 503 | storage_unavailable | اختلال خواندن DB، از جمله recheck؛ گزارش تحویل نشود |
| 504 | ai_review_timeout | بودجهٔ مجاز بررسی تمام شده؛ پاسخ دیررس تحویل نشود |
| 500 | unexpected_error | خطای غیرمنتظره با پیام عمومی |

نمونهٔ refusal:

```json
{
  "type": "about:blank",
  "title": "سرویس از انجام این بررسی خودداری کرد.",
  "status": 422,
  "detail": "گزارش بررسی تولید نشد؛ می‌توانید متن را بازبینی کنید و دوباره درخواست دهید.",
  "instance": "/api/author/posts/10000000-0000-0000-0000-000000000001/content-review",
  "code": "ai_review_refused"
}
```

در خطای وضعیت فقط currentStatus/currentVersion مجاز طبق قرارداد موجود، پس از کنترل دسترسی، قابل ارائه‌اند؛ متن افشا نشود. پیش‌شرط مجوز قبل از lookup حساس بررسی شود. خطای هویت/مجوز مقدم بر افشای وجود/وضعیت است؛ deadline بر تحویل موفق دیررس مقدم است. قطع Client مسیر cancellation است، نه یک status گزارش؛ پاسخ برای اتصال قطع‌شده تضمین نمی‌شود.

## OpenAPI و پذیرش قرارداد

مسیر جدا با metadata اختصاصی گزارش، success 200 و خطاهای جدول مستند شود؛ DocumentPost موجود برای PostView و metadata mutation/ETag مناسب این پاسخ نیست. مسیر تازه بدون الزام If-Match و بدون ETag پاسخ مستند شود. قرارداد JSON report، enumها، nullable suggestion و envelope تعریف شود؛ codeهای failure نیز ثبت شوند. آزمون قرارداد، نبود الزام If-Match و ETag، no-store، 200های دوگانه، refusal و failure و عدم افشای داده را بررسی کند.
