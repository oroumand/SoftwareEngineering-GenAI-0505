# Promptهای اجرای زنده

## دموی اول: Vibe Coding

### Prompt آغاز

```text
با .NET 10 یک Minimal Web API برای ورود اطلاعات کاربر بساز.
مدل ورودی دقیقاً ۶ property داشته باشد:
FirstName, LastName, Email, PhoneNumber, DateOfBirth, Country.
اطلاعات را فعلاً در حافظه نگه دار و endpointهای ثبت و فهرست کاربران را بساز.
پروژه را همین حالا ایجاد و اجرا کن.
```

در این روش هیچ تصمیم دیگری ندهید. اجازه دهید Agent فرضیات را پر کند. سپس فقط Happy Path را نمایش دهید.

### دو Prompt وصله‌ای اختیاری

```text
ایمیل تکراری نباید ثبت شود. درستش کن.
```

```text
سن کاربر و فرمت موبایل هم validate شود و خطاها مناسب باشند.
```

نکته‌ای که باید با صدای بلند بگویید: «ما پس از تولید کد تازه داریم قرارداد محصول را کشف می‌کنیم؛ هر patch ممکن است ساختار و رفتار قبلی را به شکل محلی تغییر دهد.»

## دموی دوم: SDD

### مرحله ۱ — تعریف نیت و تعداد propertyها

```text
قابلیت ثبت پروفایل کاربر را می‌خواهیم. ورودی دقیقاً ۶ property دارد:
FirstName, LastName, Email, PhoneNumber, DateOfBirth, Country.
فعلاً کد نزن. ابتدا فقط Spec را با user story، scope، acceptance scenario،
edge case، requirement و success criterion تولید کن.
```

### مرحله ۲ — Clarification

```text
ابهام‌های مادی Spec را مشخص کن؛ فقط سؤال‌هایی را بپرس که contract،
data model، security یا تجربه کاربر را تغییر می‌دهند.
```

پاسخ‌های ازپیش‌آماده:

- ایمیل در کل سیستم یکتا و case-insensitive است.
- تلفن باید E.164 باشد.
- حداقل سن ۱۳ سال است.
- Country کد ISO دوحرفی است.
- ذخیره‌سازی این دموی آموزشی in-memory است و persistence production خارج scope است.
- authentication و update/delete خارج scope هستند.

### مرحله ۳ — معماری

```text
بر اساس Spec تأییدشده Plan فنی بساز:
.NET 10، Onion Architecture با Domain/Application/Infrastructure/API،
CQRS با MediatR، Repository abstraction در Application،
پیاده‌سازی Repository در Infrastructure و Minimal API نازک.
Domain و Application نباید به Infrastructure وابسته باشند.
روش تست، مدیریت خطا، ساختار پروژه و dependency direction را صریح کن.
هنوز کد نزن.
```

### مرحله ۴ — تنظیم Agent

```text
AGENTS.md و constitution را بخوان. Spec، Plan و taskها منبع تصمیم هستند.
هیچ dependency یا لایه‌ای را حدس نزن؛ مرز Onion را حفظ کن؛ secret نساز؛
پس از تغییر restore/build/test را اجرا کن و فقط با evidence اعلام تکمیل کن.
```

### مرحله ۵ — Tasks و Analyze

```text
Plan را به taskهای کوچک، مرتب بر اساس dependency و متصل به requirementها
تبدیل کن. سپس Spec، Plan و Tasks را برای gap، conflict و requirement بدون
implementation/test تحلیل کن. کد نزن تا تحلیل بدون gap مادی باشد.
```

### مرحله ۶ — Implement

```text
taskها را به ترتیب اجرا کن. بعد از هر slice مرز dependency را کنترل کن.
در پایان restore، build و test را اجرا کن و traceability/evidence را به‌روزرسانی کن.
```

## جمله انتقال میان دو دمو

«در دموی اول، Agent هم‌زمان تحلیلگر محصول، معمار و برنامه‌نویس بود و هر سکوت را با حدس پر کرد. در دموی دوم، ما قبل از دادن اختیار پیاده‌سازی، مرجع حقیقت، مرز تصمیم و تعریف پایان کار را بستیم.»

