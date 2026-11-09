# راهنمای اجرای گام ۲۳ — Ingestion در Qdrant

این Snapshot از نسخهٔ اصلاح‌شدهٔ گام ۲۲ کپی شده و قابلیت‌های قبلی را حفظ می‌کند.
گام ۲۳ فقط مسیر آماده‌سازی دانش را اضافه می‌کند؛ Agent و Retrieval نداریم.

## ۱. Build و آزمون‌ها

دستورها از ریشهٔ BlogSpecLab در Workshop-4/Samples/step23 اجرا شوند:

    dotnet restore BlogSpecLab.slnx
    dotnet tool restore
    dotnet build BlogSpecLab.slnx --no-restore
    dotnet test BlogSpecLab.slnx --no-build --no-restore --filter 'Category!=AIQuality&Category!=AIProvider'

## ۲. نمایش آفلاین Chunking

    dotnet run --project src/04.Tools/KnowledgeIngest -- dry-run

خروجی باید پنج بخش Markdown با شناسهٔ UUID پایدار و بخشی از SHA-256 متن نشان دهد.
در این حالت نه Qdrant و نه OpenAI فراخوانی نمی‌شوند.

## ۳. پیش‌نیاز Ingestion واقعی

- .NET 10 SDK و دسترسی به Embedding API
- Qdrant نسخهٔ ۱.۱۹ روی http://localhost:6333 (یا سرویس HTTPS مجاز)
- Credential مناسب از Secret Store؛ مقدار آن هرگز در Repository یا Log ثبت نشود
- فقط سند آموزشی و ساختگی docs/editorial/official-policy-v1.md به Provider ارسال می‌شود

اگر Container قبلاً نصب و اجرا شده از همان استفاده کنید:

    docker ps --filter name=qdrant
    Invoke-RestMethod http://localhost:6333/collections

اگر Container ندارید، نصب Qdrant طبق زیرساخت محلی خودتان انجام شود؛ در این
Snapshot هیچ دستور حذف یا بازسازی دیتابیس تولیدی وجود ندارد.

## ۴. اجرای Ingestion

Credential را به شکل امن در محیط فرایند تنظیم کنید. در PowerShell:

    $env:QDRANT_URL = 'http://localhost:6333'
    # AI__EditorialKnowledge__ApiKey باید به‌صورت امن تعریف شده باشد؛ آن را چاپ نکنید.
    dotnet run --project src/04.Tools/KnowledgeIngest -- ingest

به شکل پیش‌فرض از Collection آموزشی زیر استفاده می‌شود:

    blogspec_editorial_text_embedding_3_small_v1

Embedding Model فعلی text-embedding-3-small است. تغییر مدل با فضای برداری ناسازگار
نباید بدون ساخت Collection نسخه‌دار تازه انجام شود.

## ۵. مشاهدهٔ داده‌های Qdrant

    $collection = 'blogspec_editorial_text_embedding_3_small_v1'
    Invoke-RestMethod "http://localhost:6333/collections/$collection"
    $body = '{"limit":10,"with_payload":true,"with_vector":false}'
    Invoke-RestMethod -Method Post -Uri "http://localhost:6333/collections/$collection/points/scroll" -ContentType 'application/json' -Body $body

در Scroll باید Metadata مانند documentId، version، section، contentSha256،
embeddingModel و accessScope دیده شود. دادهٔ نمونه عمومی و ساختگی است.

## ۶. نکات تدریس

- MarkdownSectionChunker: نقش عنوان‌های منطقی و خطر جدا کردن شرط از قاعده
- OpenAiEditorialEmbeddingGenerator: تبدیل Chunk به بردار؛ مدل Embedding با مدل Chat متفاوت است
- QdrantEditorialKnowledgeStore: ایجاد Collection، تطبیق ابعاد و Upsert Pointها
- EditorialKnowledgeIngestionService: آماده شدن کل Embeddingها قبل از نوشتن
- CLI: جدا بودن Ingestion اپراتوری از API نویسنده
- Unit Tests: اثبات شناسه‌های پایدار، اعتبارسنجی و جلوگیری از Upsert ناقص

## ۷. محدودیت این Snapshot

این گام هنوز Retrieval، دستیار RAG یا ارزیابی پاسخ ندارد. تغییر محتوا در همان نسخه
یا اضافه شدن نسخهٔ جدید به سیاست بازنشستگی Pointهای قدیمی نیاز دارد که در
ادامهٔ طراحی چرخهٔ دانش باید تعیین شود.

Build/Test و Provider Smoke تنها پس از اجرای واقعی و ثبت خروجی PASS محسوب می‌شوند.
