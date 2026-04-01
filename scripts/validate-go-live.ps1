param(
    [string]$ApiBaseUrl = "http://localhost:5146/api",
    [string]$UserMail = "",
    [string]$MailboxId = "",
    [Parameter(Mandatory = $true)]
    [string]$StoragePath,
    [string]$OutputDirectory = ".\artifacts\dev-logs",
    [int]$TimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$resolvedOutputDirectory = Join-Path $repoRoot $OutputDirectory
New-Item -Path $resolvedOutputDirectory -ItemType Directory -Force | Out-Null

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logPrefix = "go-live-validation-$timestamp"
$outLogPath = Join-Path $resolvedOutputDirectory "$logPrefix.out.log"
$errLogPath = Join-Path $resolvedOutputDirectory "$logPrefix.err.log"
$summaryPath = Join-Path $resolvedOutputDirectory "$logPrefix.summary.json"

New-Item -Path $outLogPath -ItemType File -Force | Out-Null
New-Item -Path $errLogPath -ItemType File -Force | Out-Null

function Write-OutLog {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "o"), $Message
    Add-Content -Path $outLogPath -Value $line
    Write-Host $line
}

function Write-ErrLog {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "o"), $Message
    Add-Content -Path $errLogPath -Value $line
    Write-Host $line
}

function Invoke-ApiRequest {
    param(
        [ValidateSet("GET", "POST")]
        [string]$Method,
        [string]$Uri,
        [string]$JsonBody = ""
    )

    $handler = New-Object System.Net.Http.HttpClientHandler
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)

    try {
        if ($Method -eq "GET") {
            $response = $client.GetAsync($Uri).GetAwaiter().GetResult()
        }
        else {
            $content = New-Object System.Net.Http.StringContent($JsonBody, [System.Text.Encoding]::UTF8, "application/json")
            $response = $client.PostAsync($Uri, $content).GetAwaiter().GetResult()
        }

        $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return [PSCustomObject]@{
            StatusCode = [int]$response.StatusCode
            Body = $body
            Error = ""
        }
    }
    catch {
        return [PSCustomObject]@{
            StatusCode = 0
            Body = ""
            Error = $_.Exception.Message
        }
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

function TryParse-Json {
    param([string]$Body)

    if ([string]::IsNullOrWhiteSpace($Body)) {
        return $null
    }

    try {
        return $Body | ConvertFrom-Json -Depth 20
    }
    catch {
        return $null
    }
}

Write-OutLog "Go-live validation started."
Write-OutLog "API base URL: $ApiBaseUrl"
Write-OutLog "Storage path under validation: $StoragePath"

$graphEndpoint = "$($ApiBaseUrl.TrimEnd('/'))/health/graph"
if (-not [string]::IsNullOrWhiteSpace($UserMail) -and -not [string]::IsNullOrWhiteSpace($MailboxId)) {
    $graphEndpoint = "$graphEndpoint?userMail=$([Uri]::EscapeDataString($UserMail))&mailboxId=$([Uri]::EscapeDataString($MailboxId))"
}

Write-OutLog "Calling Graph health endpoint: $graphEndpoint"
$graphResult = Invoke-ApiRequest -Method "GET" -Uri $graphEndpoint
Write-OutLog "Graph health status code: $($graphResult.StatusCode)"
if (-not [string]::IsNullOrWhiteSpace($graphResult.Body)) {
    Add-Content -Path $outLogPath -Value $graphResult.Body
}
if (-not [string]::IsNullOrWhiteSpace($graphResult.Error)) {
    Write-ErrLog "Graph health request error: $($graphResult.Error)"
}

$graphPayload = TryParse-Json -Body $graphResult.Body
$graphPass = $false
if ($graphResult.StatusCode -eq 200 -and $null -ne $graphPayload) {
    $graphPass = $graphPayload.isSuccess -eq $true
}

if ($graphPass) {
    Write-OutLog "Graph gate: PASS"
}
else {
    Write-ErrLog "Graph gate: FAIL"
}

$storageEndpoint = "$($ApiBaseUrl.TrimEnd('/'))/settings/storage-access/check"
$storageBody = @{ path = $StoragePath } | ConvertTo-Json -Depth 5 -Compress

Write-OutLog "Calling storage access endpoint: $storageEndpoint"
$storageResult = Invoke-ApiRequest -Method "POST" -Uri $storageEndpoint -JsonBody $storageBody
Write-OutLog "Storage check status code: $($storageResult.StatusCode)"
if (-not [string]::IsNullOrWhiteSpace($storageResult.Body)) {
    Add-Content -Path $outLogPath -Value $storageResult.Body
}
if (-not [string]::IsNullOrWhiteSpace($storageResult.Error)) {
    Write-ErrLog "Storage check request error: $($storageResult.Error)"
}

$storagePayload = TryParse-Json -Body $storageResult.Body
$storagePass = $false
if ($storageResult.StatusCode -eq 200 -and $null -ne $storagePayload) {
    $storagePass = ($storagePayload.success -eq $true) -and ($storagePayload.canRead -eq $true) -and ($storagePayload.canWrite -eq $true)
}

if ($storagePass) {
    Write-OutLog "UNC/storage gate: PASS"
}
else {
    Write-ErrLog "UNC/storage gate: FAIL"
}

$summary = [PSCustomObject]@{
    executedAtUtc = (Get-Date).ToUniversalTime().ToString("o")
    apiBaseUrl = $ApiBaseUrl
    evidence = [PSCustomObject]@{
        outLog = $outLogPath
        errLog = $errLogPath
        summary = $summaryPath
    }
    gates = @(
        [PSCustomObject]@{
            name = "GraphHealth"
            passed = $graphPass
            statusCode = $graphResult.StatusCode
            userMail = $UserMail
            mailboxId = $MailboxId
        },
        [PSCustomObject]@{
            name = "StorageAccess"
            passed = $storagePass
            statusCode = $storageResult.StatusCode
            path = $StoragePath
        }
    )
    overallPassed = ($graphPass -and $storagePass)
}

$summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryPath -Encoding UTF8
Write-OutLog "Summary written to: $summaryPath"

if (-not $summary.overallPassed) {
    Write-ErrLog "Go-live validation finished with FAIL."
    exit 1
}

Write-OutLog "Go-live validation finished with PASS."
exit 0
