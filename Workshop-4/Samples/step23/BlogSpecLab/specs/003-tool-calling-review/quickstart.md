# اجرای گام ۲۲ — Tool Calling

این Snapshot یک کپی مستقل از Workshop-4/Samples/step21/BlogSpecLab است.
مسیر قبلی POST /api/author/posts/{id}/content-review بدون تغییر باقی مانده است.
مسیر تازه POST /api/author/posts/{id}/content-review-tools است.

## Restore، Build و تست قطعی

دستورها را از ریشهٔ BlogSpecLab در step22 اجرا کنید:

    dotnet restore BlogSpecLab.slnx
    dotnet tool restore
    dotnet build BlogSpecLab.slnx --no-restore
    dotnet test BlogSpecLab.slnx --no-build --no-restore --filter 'Category!=AIQuality&Category!=AIProvider'

تست‌های ReadCurrentPostToolTests، ToolContentReviewTransportTests و
ToolContentReviewApiTests، قرارداد ابزار، دو تماس مدل و حفاظت API را بررسی می‌کنند.

## اجرای مدل واقعی

از تنظیم‌های همان قابلیت ۰۰۲ استفاده می‌شود. Credential باید به‌صورت محرمانه در متغیر محیطی
AI__ContentReview__ApiKey فراهم شده باشد؛ آن را در فایل یا فرمان قابل اشتراک ثبت نکنید.

    $env:AI__ContentReview__Enabled = 'true'
    $env:AI__ContentReview__Model = 'gpt-6-luna'
    dotnet run --no-launch-profile --project src/03.Endpoints/Api

دیتابیس، Migration و Credential نویسنده را براساس docs/operations/local-access.md آماده کنید.
با Enabled=false یا کلید غایب، بازبینی مجاز پاسخ 503 با ai_review_unavailable دارد و CRUD قبلی کار می‌کند.

## نمونهٔ درخواست

برای یک پست واقعی متعلق به نویسندهٔ جاری:

    $base = $env:BLOG_API_BASE_URL.TrimEnd('/')
    $headers = @{ Authorization = "Bearer $env:BLOG_AUTHOR_TOKEN" }
    $postId = '<شناسهٔ واقعی پست>'
    $result = Invoke-RestMethod -Method Post -Uri "$base/api/author/posts/$postId/content-review-tools" -Headers $headers
    $result.report.status
    $result.report.issues
    $result.report.missingInformation

Body یا If-Match ارسال نکنید. گزارش کمکی است و نه تأیید انتشار.

## ترتیب نمایش در کلاس

۱. ReadCurrentPostTool.cs: نام، JSON Schema و اعتبارسنجی دقیق آرگومان.
۲. OpenAiToolContentReviewGateway.cs: Tool Choice اجباری، store=false، تماس نخست بدون متن پست.
۳. Tool Result: Runtime دادهٔ Snapshot مجاز را برمی‌گرداند و با Call ID به مدل معرفی می‌کند.
۴. تماس دوم: درخواست گزارش با content_review_v1 و همان Validator قبلی.
۵. ContentReviewRequestCoordinator و PostContentReviewService: کنترل مجوز، Scope، Deadline، Refresh و Non-Mutation.
۶. مقایسهٔ دو Endpoint: مسیر قبلی بدون Tool، مسیر جدید با یک Tool Call واقعی.

## وضعیت اجرای واقعی

فایل‌های تست و کد نوشته شده‌اند؛ Build، Provider Smoke و Acceptance فقط وقتی PASS محسوب می‌شوند
که واقعاً اجرا و نتایجشان ثبت شده باشد. نسخهٔ گام ۲۱ همچنان دست‌نخورده است.
