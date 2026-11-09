# وضعیت آماده‌سازی Implementation

تاریخ: 2026-10-12. **Validation، اجرای Test، Evaluation و Acceptance انجام نشده‌اند.**
این سند محل آمادهٔ ثبت شواهد در کارگاه بعد است؛ هیچ pass/fail رفتاری یا نتیجهٔ مدل ثبت نشده است.

## Development Check

Restore پروژه و Solution و build اجرا شدند. آخرین build کد تولیدشده موفق بود؛ build فقط compilation/dependency check است.
T049 باز است؛ tool restore، suite قطعی و regression به‌عنوان گام Validation این مرحله اجرا نشده‌اند.
Schema از artifact اصلی به resource پروژهٔ Infra.AI بسته‌بندی می‌شود؛ آزمون مقایسهٔ برابری نوشته شده ولی اجرا نشده است.
Constitution، Technical Baseline، Domain و migrationها تغییر نکرده‌اند. AI فقط به Application reference دارد.

## شواهدی که باید در جلسهٔ بعد ثبت شوند

| گام | زیرساخت آماده | وضعیت شواهد |
| --- | --- | --- |
| T025 | Application/Validator/Transport/Protocol/API/OpenAPI tests | اجرا نشده |
| T029 | needs_input مستقل، مختلط و ویرایش ذخیره‌شده | اجرا نشده |
| T040 | NonMutation/Concurrency/Timeout/Elapsed/Privacy/Configuration و regression قبلی | اجرا نشده |
| T041 | corpus_v1 با ۲۰ متن دارای ایراد و ۱۰ متن سالم، goldهای پیشنهادی | بازبینی و تثبیت انسانی انجام نشده |
| T045 | AIProvider opt-in، مدل اولیه، Schema و cancellation | تماس، benchmark و تثبیت Reasoning انجام نشده |
| T046 | سه run ثابت، AIQuality runner، فرم رأی و تابع scoring | run و داوری انسانی انجام نشده |
| T047 | پروتکل فهم راهنما با حداقل پنج نویسنده | مشارکت و رأی ثبت نشده |
| T049 | فرمان‌های restore/tool restore/build/test و filterها | فقط Development Build؛ Validation نشده |
| T050 | محل ثبت نگاشت نهایی FR/SC | نگاشت به شواهد نهایی و پذیرش انجام نشده |

## حدود و ریسک‌های باز

- APIهای Responses در SDK 2.14.0 experimental هستند؛ suppression فقط در فایل مصرف Responses است.
- دسترسی gpt-6-luna، پذیرش Schema، کیفیت فارسی، latency، Reasoning و کفایت 8192 توکن اندازه‌گیری نشده‌اند.
- alias مدل تضمین ثبات خروجی نیست؛ مدل واقعی و نسخه/hashها در runner ثبت خواهند شد.
- validator ساختار و شاهد exact را کنترل می‌کند؛ صحت معنایی یا نبود hallucination به Eval انسانی نیاز دارد.
- freshness فقط در checkedAt معتبر است؛ بعد از دریافت باید source revision با پست تازه مقایسه شود.
- scope دیررس متعلق به عملیات است؛ dependency غیرهمکار ممکن است منابع خودش را تا پایان task نگه دارد. تست‌های عمر scope و exception نوشته شده‌اند و باید اجرا شوند.
- corpus پیشنهادی قبل از اولین run باید انسانی بررسی و ثابت شود؛ hash اعلام‌شده بدون بازبینی انسانی شاهد gold معتبر نیست.
- هیچ Feature بعدی، ابزار، MCP، RAG، Agent، fallback، تماس اصلاحی یا اختیار نوشتن اضافه نشده است.
