param(
    [string]$BaseUrl = 'https://localhost:7180',
    [Parameter(Mandatory)][string]$ProtectedFile
)
$ErrorActionPreference = 'Stop'

# PowerShell 7.6 / .NET 10؛ بسته فقط با همان حساب Windows قابل خواندن است.
$shared = Join-Path $env:ProgramFiles 'dotnet/shared/Microsoft.AspNetCore.App/10.0.12'
Get-ChildItem $shared -Filter '*.dll' | ForEach-Object {
    try { [void][Reflection.Assembly]::LoadFrom($_.FullName) } catch [BadImageFormatException] { }
}
$secretRoot = Join-Path $env:LOCALAPPDATA 'BlogSpecLab/Secrets'
$configure = [Action[Microsoft.AspNetCore.DataProtection.IDataProtectionBuilder]] {
    param($builder)
    [void][Microsoft.AspNetCore.DataProtection.DataProtectionBuilderExtensions]::SetApplicationName($builder, 'BlogSpecLab.LocalAccess')
}
$provider = [Microsoft.AspNetCore.DataProtection.DataProtectionProvider]::Create([IO.DirectoryInfo](Join-Path $secretRoot 'keys'), $configure)
$protector = $provider.CreateProtector('BlogSpecLab.LocalAccess.v1')
$accounts = [Microsoft.AspNetCore.DataProtection.DataProtectionCommonExtensions]::Unprotect($protector, [IO.File]::ReadAllText($ProtectedFile)) | ConvertFrom-Json
if ($accounts.Count -ne 11) { throw 'بستهٔ Provision باید یازده حساب داشته باشد.' }
$author = $accounts[0]; $otherAuthor = $accounts[1]; $editor = $accounts[5]; $dual = $accounts[10]

function Invoke-Blog {
    param([string]$Method, [string]$Path, $Account, $Body, [string]$ETag, [int]$Expected = 200, [string]$Code)
    $headers = @{}
    if ($Account) { $headers.Authorization = 'Bearer ' + $Account.Token }
    if ($ETag) { $headers['If-Match'] = $ETag }
    $options = @{ Method=$Method; Uri=$BaseUrl+$Path; Headers=$headers; SkipHttpErrorCheck=$true }
    if ($null -ne $Body) { $options.Body=$Body | ConvertTo-Json -Compress; $options.ContentType='application/json; charset=utf-8' }
    # Credential یا متن پاسخ در خروجی/خطا چاپ نمی‌شود.
    try { $response = Invoke-WebRequest @options } catch { throw 'درخواست HTTP ناموفق بود؛ میزبان و HTTPS را بررسی کنید.' }
    if ([int]$response.StatusCode -ne $Expected) { throw "HTTP نامنتظره: $Method $Path، $($response.StatusCode)، انتظار $Expected" }
    $text = if ($response.Content -is [byte[]]) { [Text.Encoding]::UTF8.GetString($response.Content) } else { $response.Content }
    $value = if ($text) { $text | ConvertFrom-Json } else { $null }
    if ($Code -and $value.code -ne $Code) { throw 'کد خطا با قرارداد سازگار نیست.' }
    if ($Path -match '^/api/(author|editor)' -or $Expected -ge 400) {
        if ($response.Headers['Cache-Control'] -notcontains 'no-store') { throw 'no-store غایب است.' }
    }
    Write-Host "$Method $Path => $Expected $Code"
    [pscustomobject]@{ Value=$value; ETag=($response.Headers.ETag | Select-Object -First 1); Headers=$response.Headers }
}

try {
    $null = Invoke-Blog GET '/api/author/posts' -Expected 401 -Code unauthenticated
    $p = Invoke-Blog POST '/api/author/posts' $author @{title='عنوان نمونه';body='متن نمونه'} -Expected 201
    $id = $p.Value.id; $initialTag = $p.ETag
    if ($p.Value.status -ne 'Draft' -or ($p.Value.allowedActions -join ',') -ne 'edit,delete,submit') { throw 'نمای Draft نامعتبر است.' }
    if (-not $p.Headers.Location) { throw 'Location غایب است.' }
    $null = Invoke-Blog GET "/api/posts/$id" -Expected 404 -Code post_not_found
    $null = Invoke-Blog GET "/api/author/posts/$id" $otherAuthor -Expected 404 -Code post_not_found
    $null = Invoke-Blog GET '/api/author/posts' $author
    $p = Invoke-Blog GET "/api/author/posts/$id" $author
    $null = Invoke-Blog POST "/api/author/posts/$id/submit" $author -Expected 428 -Code version_required
    $null = Invoke-Blog PUT "/api/author/posts/$id" $author @{title=' ';body='متن'} '*' 400 invalid_input
    $p = Invoke-Blog PUT "/api/author/posts/$id" $author @{title='عنوان اصلاح‌شده';body='متن اصلاح‌شده'} $p.ETag
    $null = Invoke-Blog POST "/api/author/posts/$id/submit" $author -ETag $initialTag -Expected 412 -Code stale_version
    $p = Invoke-Blog POST "/api/author/posts/$id/submit" $author -ETag $p.ETag
    $null = Invoke-Blog GET '/api/editor/posts' $editor
    $p = Invoke-Blog GET "/api/editor/posts/$id" $editor
    if (($p.Value.allowedActions -join ',') -ne 'requestChanges,approve') { throw 'اقدامات PendingReview نامعتبر است.' }
    $p = Invoke-Blog POST "/api/editor/posts/$id/request-changes" $editor @{reason='اصلاح عنوان لازم است'} $p.ETag
    $p = Invoke-Blog GET "/api/author/posts/$id" $author
    if ($p.Value.lastReview.reason -ne 'اصلاح عنوان لازم است') { throw 'دلیل برگشت غایب است.' }
    $p = Invoke-Blog PUT "/api/author/posts/$id" $author @{title='عنوان نهایی';body='متن نهایی'} $p.ETag
    $p = Invoke-Blog POST "/api/author/posts/$id/submit" $author -ETag $p.ETag
    $p = Invoke-Blog POST "/api/editor/posts/$id/approve" $editor -ETag $p.ETag
    if (($p.Value.allowedActions -join ',') -ne 'publish') { throw 'اقدامات Approved نامعتبر است.' }
    $p = Invoke-Blog POST "/api/editor/posts/$id/publish" $editor -ETag $p.ETag
    if ($p.Value.allowedActions.Count -ne 0) { throw 'Published باید اقدامات خالی داشته باشد.' }
    $publishedVersion = $p.Value.version; $publication = $p.Value.publication | ConvertTo-Json -Compress
    $public = Invoke-Blog GET "/api/posts/$id"
    if (($public.Value.PSObject.Properties.Name | Sort-Object) -join ',' -ne 'body,id,publishedAt,title') { throw 'PublicPostView نامعتبر است.' }
    $null = Invoke-Blog GET '/api/posts'
    $again = Invoke-Blog POST "/api/editor/posts/$id/publish" $editor -ETag $initialTag
    if ($again.Value.version -ne $publishedVersion -or ($again.Value.publication | ConvertTo-Json -Compress) -ne $publication) { throw 'انتشار تکراری تغییر ایجاد کرد.' }
    $null = Invoke-Blog PUT "/api/author/posts/$id" $author @{title='تغییر';body='تغییر'} $p.ETag 409 published_post_immutable
    $null = Invoke-Blog DELETE "/api/author/posts/$id" $author -ETag $p.ETag -Expected 409 -Code published_post_immutable
    $self = Invoke-Blog POST '/api/author/posts' $dual @{title='پست دو نقشی';body='متن'} -Expected 201
    $self = Invoke-Blog POST "/api/author/posts/$($self.Value.id)/submit" $dual -ETag $self.ETag
    $null = Invoke-Blog POST "/api/editor/posts/$($self.Value.id)/approve" $dual -ETag $self.ETag -Expected 403 -Code self_approval_forbidden
    $draft = Invoke-Blog POST '/api/author/posts' $author @{title='قابل حذف';body='متن'} -Expected 201
    $null = Invoke-Blog DELETE "/api/author/posts/$($draft.Value.id)" $author -ETag $draft.ETag -Expected 204
    $null = Invoke-Blog GET "/api/posts/$($draft.Value.id)" -Expected 404 -Code post_not_found
    & dotnet run --no-build --no-launch-profile --project src/03.Endpoints/Api -- access revoke-permissions $editor.UserId
    if ($LASTEXITCODE -ne 0) { throw 'لغو مجوز ناموفق بود.' }
    $null = Invoke-Blog GET '/api/editor/posts' $editor -Expected 403 -Code forbidden
    & dotnet run --no-build --no-launch-profile --project src/03.Endpoints/Api -- access revoke-credential $editor.CredentialId
    if ($LASTEXITCODE -ne 0) { throw 'لغو Credential ناموفق بود.' }
    $null = Invoke-Blog GET '/api/editor/posts' $editor -Expected 401 -Code unauthenticated
    Write-Host 'Demo واقعی تکمیل شد؛ این اجرا پذیرش انسانی محسوب نمی‌شود.'
}
finally {
    $accounts = $author = $otherAuthor = $editor = $dual = $provider = $protector = $null
}
