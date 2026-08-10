# راهنمای کارگاه Spec Kit با Codex

این راهنما برای اجرای یک آزمایشگاه کوتاه روی ویندوز و PowerShell نوشته شده است. مسیر آن از پوشهٔ خالی شروع می‌شود و تا نخستین نوبت کدنویسی پیش می‌رود.

> اصل کارگاه: هیچ مرحله‌ای را فقط به‌دلیل تولید شدن فایل‌ها تمام‌شده ندانید. خروجی را بخوانید، با نیاز واقعی بسنجید، سپس Commit کنید و به مرحلهٔ بعد بروید.

## پیش‌نیازها و نصب

| ابزار | کاربرد | نصب در PowerShell | کنترل نصب |
|---|---|---|---|
| Git | تاریخچه و نقطهٔ بازگشت تصمیم‌ها و کد | `winget install --id Git.Git -e` | `git --version` |
| Node.js LTS | اجرای Codex CLI | `winget install --id OpenJS.NodeJS.LTS -e` | `node --version` و `npm --version` |
| Codex CLI | اجرای عامل کدنویسی در ریشهٔ پروژه | `npm install -g @openai/codex` | `codex --version` |
| Python 3.11+ | پیش‌نیاز ابزارهای Python مانند Spec Kit | `winget install --id Python.Python.3.12 -e` | `python --version` |
| uv | نصب و اجرای مطمئن ابزارهای Python | `winget install --id astral-sh.uv -e` | `uv --version` |
| Spec Kit | ساخت workflow و Skillهای SDD | `uv tool install specify-cli --from git+https://github.com/github/spec-kit.git` | `specify --help` |

پس از نصب، PowerShell را یک‌بار ببند و دوباره باز کن تا مسیر ابزارها به‌روز شود. در نخستین اجرای `codex`، با حساب ChatGPT وارد شو.

راهنمای رسمی [Codex CLI](https://learn.chatgpt.com/docs/codex/cli) می‌گوید Codex را از داخل پوشهٔ پروژه اجرا کنید. برای Syntax و نسخهٔ فعلی Spec Kit نیز پیش از کارگاه، راهنمای [GitHub Spec Kit](https://github.com/github/spec-kit) را چک کنید؛ ممکن است نام بعضی گزینه‌ها میان نسخه‌ها تغییر کند.

## مسیر کارگاه

### ۱. ساخت پوشهٔ آزمایشگاه

```powershell
mkdir BlogSpecLab
Set-Location BlogSpecLab
Get-Location
```

**چرا:** هر قابلیت و تمام خروجی‌های آن باید در یک ریشهٔ مشخص قرار گیرند.

**انتظار:** PowerShell مسیر `...\BlogSpecLab` را نشان دهد؛ پوشه هنوز خالی است.

**بازبینی:** نام پروژه بدون فاصله و قابل‌خواندن باشد. از همین مسیر همهٔ فرمان‌های بعدی را اجرا کنید.

### ۲. آغاز Git و ثبت هویت محلی

```powershell
git init
git branch -M main
git config user.name "نام شما"
git config user.email "email@example.com"
git status
```

**چرا:** از اولین تصمیم معماری تا آخرین خط کد، تاریخچه و امکان بازگشت داریم. تنظیم نام و ایمیل با `git config` فقط برای همین Repository است.

**انتظار:** پوشهٔ مخفی `.git` ایجاد شود و `git status` شاخهٔ `main` و Working tree تمیز را نمایش دهد.

**بازبینی:** با `git config --local --list` مطمئن شوید `user.name` و `user.email` درست‌اند؛ اطلاعات شخصی یا سازمانی اشتباه وارد نشده باشد.

### ۳. نصب ساختار Spec Kit در همان Repository

```powershell
specify init --here --integration codex --script ps
git status --short
Get-ChildItem -Force
```

**چرا:** Spec Kit قالب‌ها، حافظهٔ پروژه، ساختار Featureها و دستورالعمل‌هایی را اضافه می‌کند که Codex بتواند فرآیند SDD را یکدست اجرا کند. گزینهٔ `--here` یعنی Repository فعلی و `--integration codex` یعنی تولید دستورهای سازگار با Codex.

**انتظار:** معمولاً پوشه‌های `.specify`، `.agents` و `specs` و بسته به نسخه فایل‌های راهنما ایجاد می‌شوند. مهم‌ترین فایل اولیه ` .specify/memory/constitution.md` است که در مرحلهٔ بعد تکمیل می‌شود.

**بازبینی:** فقط مطمئن نشوید فایل ایجاد شده؛ `git status --short` را بخوانید و بررسی کنید ساختار در ریشهٔ درست پروژه ساخته شده است. فایل‌های تولیدشدهٔ ابزار را بی‌دلیل دستی جابه‌جا نکنید.

### ۴. Commit کردن Bootstrap

```powershell
git add -A
git diff --cached --check
git commit -m "chore: bootstrap SpecKit with Codex integration"
git status --short
```

**چرا:** این Commit نقطهٔ شروع قابل‌بازگشت آزمایشگاه است؛ هر تغییر بعدی را می‌توان با آن مقایسه کرد.

**انتظار:** Commit ایجاد شود و خروجی آخر خالی باشد.

**بازبینی:** پیش از Commit، با `git diff --cached` مطمئن شوید فقط فایل‌های Bootstrap وارد Commit می‌شوند. خطای whitespace نباید وجود داشته باشد.

### ۵. ورود به Codex از ریشهٔ پروژه

```powershell
codex
```

**چرا:** Codex باید از ریشهٔ Repository شروع شود تا Git، `.specify` و Skillهای افزوده‌شده را ببیند.

**انتظار:** رابط Codex باز شود و مسیر فعلی همان `BlogSpecLab` باشد. در اولین بار، فرآیند ورود به حساب را کامل کنید.

**بازبینی:** در Codex دستور `/status` را بزنید و مسیر و سطح دسترسی را ببینید. برای کارگاه، حالت تأیید فرمان‌ها را طوری انتخاب کنید که تغییرات و فرمان‌ها قابل مشاهده باشند.

از اینجا به بعد، موارد داخل کادرهای متنی را **داخل Codex** می‌فرستیم، نه در PowerShell.

### ۶. تعریف اصول ثابت پروژه — Constitution

```text
$speckit-constitution

برای این پروژه [فناوری، معماری، قواعد تست، امنیت، API، کیفیت و Git] را
به اصول قابل‌سنجش و غیرقابل‌مذاکره تبدیل کن.
```

**چرا:** قبل از تعریف قابلیت، مرز تصمیم‌ها را مشخص می‌کنیم؛ مثلاً «تست قبل از پیاده‌سازی»، «API قراردادی» یا «عدم افشای دادهٔ Draft».

**خروجی اصلی:** `.specify/memory/constitution.md`

**بازبینی:** اصول باید اجرایی و قابل‌سنجش باشند، نه شعار. فناوری یا طراحی مربوط به یک Feature خاص را بی‌دلیل به قانون دائمی پروژه تبدیل نکنید.

**پس از تأیید:** خارج از Codex یا با درخواست از Codex، این تغییر را جداگانه Commit کنید.

```powershell
git add .specify/memory/constitution.md
git commit -m "docs: define project constitution"
git status --short
```

### ۷. تبدیل PRD به نیازمندی قابل‌آزمون — Specify

```text
$speckit-specify

این PRD را به یک Specification رفتاری و قابل‌آزمون تبدیل کن:
[متن یا فایل PRD را اینجا بده]
```

**چرا:** PRD زبان کسب‌وکار دارد. در Specification، رفتار سیستم، User Story، نیازمندی عملکردی و معیار پذیرش روشن می‌شوند؛ هنوز دربارهٔ جدول، کلاس یا فریم‌ورک تصمیم نمی‌گیریم.

**خروجی‌های معمول:**

```text
specs/001-feature-name/spec.md
specs/001-feature-name/checklists/requirements.md
```

**بازبینی:**

- آیا هر User Story ارزش مستقل و معیار پذیرش روشن دارد؟
- آیا نیازمندی‌ها با «باید» نوشته شده‌اند و قابل آزمون‌اند؟
- آیا از جزئیات پیاده‌سازی فنی دور مانده‌ایم؟
- آیا ابهام‌ها در Checklist دیده شده‌اند؟

پس از اصلاح و تأیید `spec.md`، آن را Commit کنید.

### ۸. پاسخ به ابهام‌های پرهزینه — Clarify

```text
$speckit-clarify
```

**چرا:** فقط ابهام‌هایی را رفع می‌کنیم که پاسخشان رفتار یا قرارداد سیستم را تغییر می‌دهد؛ مانند تقدم خطاها در یک درخواست هم‌زمان.

**خروجی:** تکمیل کنترل‌شدهٔ `spec.md` و ثبت تصمیم‌های رفتاری.

**بازبینی:** پاسخ باید دقیق، محدود و قابل‌آزمون باشد. در این مرحله طراحی فنی جدید وارد نکنید. اگر پاسخ سؤال را نمی‌دانید، آن را حدس نزنید؛ از مالک محصول بپرسید.

پس از پایان Clarify، دوباره `spec.md` را بخوانید و Commit کنید.

### ۹. طراحی فنی — Plan

```text
$speckit-plan
```

**چرا:** حالا که «چه چیزی» قطعی است، تصمیم می‌گیریم «چگونه» آن را بسازیم: معماری، مدل داده، قرارداد API، وابستگی‌ها و راه اجرای سناریوی end-to-end.

**خروجی‌های معمول:**

```text
specs/001-feature-name/plan.md
specs/001-feature-name/research.md
specs/001-feature-name/data-model.md
specs/001-feature-name/contracts/openapi.yaml
specs/001-feature-name/quickstart.md
```

**بازبینی:** هر نیازمندی `spec.md` باید در Plan پوشش داشته باشد؛ قرارداد API، مدل داده و Quickstart نباید با هم تناقض داشته باشند. تصمیم‌های فنی مهم باید دلیل داشته باشند.

پس از تأیید همهٔ خروجی‌ها، Commit کنید.

### ۱۰. شکستن طراحی به Taskهای قابل‌اجرا — Tasks

```text
$speckit-tasks
```

**چرا:** Plan به کارهای کوچک، مرتب و قابل‌واگذاری تبدیل می‌شود تا اجرای عامل قابل‌کنترل باشد.

**خروجی اصلی:** `specs/001-feature-name/tasks.md`

**بازبینی:** هر Task باید شناسه، ترتیب وابستگی، فایل یا محدودهٔ تغییر و تعریف روشن «تمام‌شدن» داشته باشد. آزمون باید پیش از رفتار متناظر بیاید: Red → Implementation → Green.

پس از تأیید `tasks.md`، آن را Commit کنید.

### ۱۱. تحلیل سازگاری پیش از کد — Analyze

```text
$speckit-analyze
```

**چرا:** پیش از ایجاد هزینهٔ کدنویسی، ناسازگاری میان Constitution، Specification، Plan، قرارداد API و Taskها را پیدا می‌کنیم.

**خروجی:** گزارش شکاف‌ها و اصلاح‌های لازم؛ این مرحله نباید کد تولید کند.

**بازبینی:** هر تناقض را در سند درست اصلاح کنید؛ مثلاً مسئلهٔ رفتار در `spec.md`، طراحی در `plan.md` و ترتیب اجرا در `tasks.md`. بعد از اصلاح، در صورت تغییر اسناد Commit کنید.

### ۱۲. آغاز پیاده‌سازی محدود و تست‌محور — Implement

```text
$speckit-implement

فقط T001 تا T003 را اجرا کن.
پیش از پیاده‌سازی هر رفتار، آزمون متناظر را بنویس و ابتدا قرمز بودن آن را بررسی کن.
پس از تکمیل هر Task، همان Task را در tasks.md علامت بزن.
از Constitution، Specification، Plan و قرارداد API تخطی نکن.
در پایان، فایل‌های تغییرکرده و نتیجهٔ build/test را گزارش کن.
```

**چرا:** پیاده‌سازی را در دسته‌های کوچک نگه می‌داریم تا Diff قابل‌بازبینی، تست‌پذیر و قابل‌برگشت باشد.

**خروجی:** کد، آزمون‌های سبز و علامت‌خوردن Taskهای تکمیل‌شده در `tasks.md`.

**بازبینی هر نوبت:**

```powershell
git status --short
git diff --check
git diff
# فرمان‌های متناسب با فناوری پروژه، برای نمونه:
dotnet build
dotnet test
```

فقط وقتی همهٔ آزمون‌ها سبزند، Diff با اسناد سازگار است و Taskها درست علامت خورده‌اند، Commit کنید. سپس دستهٔ کوچک بعدی Taskها را اجرا کنید.

## نقشهٔ کوتاه مسیر

```text
پوشهٔ خالی
→ Git
→ Spec Kit init
→ Codex
→ Constitution
→ Specify
→ Clarify
→ Plan
→ Tasks
→ Analyze
→ Implement (در دسته‌های کوچک و TDD)
```

در این مسیر، خروجی هر مرحله ورودی مرحلهٔ بعدی است؛ پس کیفیت کار از سرعت تولید فایل مهم‌تر است.
