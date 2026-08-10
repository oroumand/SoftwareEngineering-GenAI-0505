# راهنمای اعتبارسنجی طراحی: نگارش و انتشار پست وبلاگ

این راهنما پس از مرحلهٔ پیاده‌سازی برای اثبات End-to-End قابلیت استفاده می‌شود. در مرحلهٔ
Plan هیچ دستور ساخت پروژه، Migration یا Package اجرا نشده است.

## پیش‌نیازهای اجرای آینده

- .NET 10 SDK سازگار با `global.json`
- Docker برای PostgreSQL آزمون‌های یکپارچه
- دسترسی به یک هویت نویسندهٔ احرازشده از طریق Adapter آزمایشی یا محیطی

## مصنوعات مرجع

- رفتار محصول: [spec.md](./spec.md)
- تصمیم‌های فنی: [research.md](./research.md)
- Aggregate و Invariantها: [data-model.md](./data-model.md)
- قرارداد HTTP: [contracts/openapi.yaml](./contracts/openapi.yaml)

## دروازهٔ ساخت و آزمون

پس از وجود Solution پیاده‌سازی‌شده، مسیر پایهٔ اعتبارسنجی باید این باشد:

```powershell
dotnet restore BlogSpecLab.slnx
dotnet build BlogSpecLab.slnx --no-restore
dotnet test BlogSpecLab.slnx --no-build
```

نتیجهٔ مورد انتظار: Build، Domain Tests، Application Tests، Architecture Tests و API
Integration Tests همگی موفق باشند. Testهای Integration باید PostgreSQL ایزوله را با
Testcontainers اجرا کنند و به پایگاه دادهٔ مشترک توسعه وابسته نباشند.

## سناریو ۱: ایجاد Draft معتبر

1. هویت نویسندهٔ A را در Adapter هویت آزمون فعال کنید.
2. `POST /api/posts` را با Title و Content معتبر ارسال کنید.
3. شناسهٔ Post ایجادشده را ثبت کنید.
4. `GET /api/posts` را به‌عنوان Reader اجرا کنید.
5. `GET /api/posts/{id}` را برای همان شناسه اجرا کنید.

**انتظار**: ایجاد موفق است؛ Post در فهرست نیست؛ مشاهدهٔ مستقیم محتوای Draft را افشا
نمی‌کند.

## سناریو ۲: رد ورودی نامعتبر و Atomicity

1. ایجاد با Title خالی و سپس Content فقط فاصله را امتحان کنید.
2. یک Draft معتبر بسازید.
3. ویرایش آن را با یک مقدار معتبر و مقدار دیگر فقط فاصله امتحان کنید.

**انتظار**: ایجادهای نامعتبر هیچ Postای باقی نمی‌گذارند؛ ویرایش نامعتبر رد می‌شود و هر
دو مقدار قبلی Draft بدون تغییر می‌مانند.

## سناریو ۳: مالکیت

1. Draft را با نویسندهٔ A ایجاد کنید.
2. با هویت نویسندهٔ B ویرایش و انتشار همان شناسه را امتحان کنید.
3. با نویسندهٔ A وضعیت را دوباره بررسی کنید.

**انتظار**: هر دو عملیات B رد می‌شوند، محتوای Draft افشا نمی‌شود و داده بدون تغییر است.

## سناریو ۴: ویرایش و انتشار

1. نویسندهٔ A Draft خود را با Title و Content معتبر جدید ویرایش کند.
2. همان نویسنده Draft را منتشر کند.
3. Reader فهرست و Detail را دریافت کند.
4. نویسندهٔ A ویرایش، انتشار دوباره و بازگشت به Draft را امتحان کند.

**انتظار**: ویرایش موفق وضعیت را Draft نگه می‌دارد؛ انتشار وضعیت و PublishedAt را ثبت
می‌کند؛ Reader Title، Content و PublishedAt را می‌بیند؛ همهٔ تغییرهای پس از انتشار رد و
Post بدون تغییر باقی می‌ماند.

## سناریو ۵: فهرست منتشرشده‌ها

1. چند Post را در زمان‌های قابل کنترل و متفاوت منتشر کنید و دست‌کم یک Draft نگه دارید.
2. `GET /api/posts` را اجرا کنید.

**انتظار**: همهٔ Postهای Published و هیچ Draftی برگردانده نمی‌شوند؛ ترتیب بر اساس
PublishedAt از جدیدترین به قدیمی‌ترین است. برای زمان‌های برابر انتظار ثانویه تعریف نکنید.

## سناریو ۶: Optimistic Concurrency مبتنی بر ETag

1. نویسندهٔ A یک Draft ایجاد و مقدار Header `ETag` پاسخ را با نام `V1` ذخیره کند.
2. دو درخواست مستقل `PUT /api/posts/{id}` با عنوان/متن معتبر اما متفاوت بسازید؛ هر دو
   باید Header `If-Match: V1` داشته باشند.
3. درخواست نخست را اجرا و پاسخ موفق `204` و Header `ETag` تازه با نام `V2` را ثبت کنید.
4. درخواست دوم را، حتی اگر بعد از پایان درخواست نخست به سرور برسد، بدون تغییر `If-Match`
   اجرا کنید.
5. Post را از مسیر مالک یا دادهٔ آزمون بخوانید و مقدار نهایی را با نتیجهٔ درخواست نخست
   مقایسه کنید.

**انتظار**: دقیقاً یک درخواست موفق است و ETag تازه دریافت می‌کند؛ درخواست دیگر `412
Precondition Failed` با `application/problem+json` و نوع `precondition-failed` می‌گیرد.
دادهٔ نهایی فقط نتیجهٔ درخواست موفق است و هیچ بازنویسی یا Merge بی‌صدا رخ نمی‌دهد. مقدار
ETag opaque است و `xmin` یا جزئیات PostgreSQL را افشا نمی‌کند.

## سناریو ۷: قرارداد خطا و عدم افشا

برای Validation، هویت ناموجود، Post ناموجود، Draft غیرقابل مشاهده، `If-Match` مفقود،
ETag نامنطبق، وضعیت نامعتبر و خطای پیش‌بینی‌نشده نمونه بسازید.

**انتظار**: پاسخ‌ها مطابق OpenAPI از `application/problem+json` استفاده می‌کنند؛
`If-Match` مفقود برای Draft قابل‌دسترس `428 precondition-required`، ETag نامنطبق `412
precondition-failed` و وضعیت نامعتبر Domain `409 post-state-conflict` است. هیچ Stack
Trace، SQL، Exception، Secret یا محتوای Draft وجود ندارد و Draft با منبع غیرقابل مشاهده
قابل تمایز نیست.

## دروازهٔ معماری

Architecture Tests باید حداقل این موارد را شکست دهند:

- هر Reference یا Package خارجی در Domain
- وابستگی Application به Infrastructure یا API
- وابستگی Infrastructure به API
- دریافت Concrete Infrastructure در Controller
- Endpoint کسب‌وکاری خارج از Controller
- مصنوعات Posts خارج از پوشهٔ Aggregate مربوط در هر لایه

موفقیت تمام سناریوهای بالا و چک‌های Constitution شرط آمادگی برای Merge است.
