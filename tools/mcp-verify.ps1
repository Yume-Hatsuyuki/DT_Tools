<#
.SYNOPSIS
    DT_Tools MCP 桥接验证脚本（Windows PowerShell 5.1 与 PowerShell 7 均可运行）。

.DESCRIPTION
    依次验证：initialize、通知、tools/list、五个工具的正常调用、错误路径与鉴权。
    运行前需要：游戏已启动，MCP 已启用（McpOptions.Enabled = true）。

.PARAMETER HostName
    MCP 监听地址，对应 McpOptions.ListenIp（默认 127.0.0.1，只能本机访问）。

.PARAMETER Port
    MCP 端口，对应 McpOptions.Port（默认 19452，修改后需重启游戏）。

.PARAMETER Token
    访问密码，对应 McpOptions.Password。游戏里密码为空时不传。

.PARAMETER Section
    get_config 验证使用的配置段名，默认 SitOnChairs。

.EXAMPLE
    .\mcp-verify.ps1

.EXAMPLE
    .\mcp-verify.ps1 -Token "你的密码"
#>
[CmdletBinding()]
param(
    [string]$HostName = "127.0.0.1",
    [int]$Port = 19452,
    [string]$Token = "",
    [string]$Section = "SitOnChairs"
)

$ErrorActionPreference = "Stop"
$Url = "http://${HostName}:${Port}/mcp"
$script:PassCount = 0
$script:FailCount = 0

function Write-Check {
    param([string]$Name, [bool]$Ok, [string]$Detail = "")
    if ($Ok) {
        $script:PassCount++
        Write-Host ("[PASS] " + $Name + " " + $Detail) -ForegroundColor Green
    } else {
        $script:FailCount++
        Write-Host ("[FAIL] " + $Name + " " + $Detail) -ForegroundColor Red
    }
}

function ConvertFrom-JsonSafe {
    param([string]$Text)
    try { return ($Text | ConvertFrom-Json) } catch { return $null }
}

function New-RpcBody {
    param($Payload)
    return ($Payload | ConvertTo-Json -Depth 20 -Compress)
}

Add-Type -AssemblyName System.Net.Http
$script:Http = New-Object -TypeName System.Net.Http.HttpClient
$script:Http.Timeout = [TimeSpan]::FromSeconds(20)

function Invoke-Mcp {
    param([string]$Body, [string]$AuthToken = $Token)
    $method = [System.Net.Http.HttpMethod]::Post
    $req = New-Object -TypeName System.Net.Http.HttpRequestMessage -ArgumentList $method, $Url
    $req.Content = New-Object -TypeName System.Net.Http.StringContent -ArgumentList $Body, ([System.Text.Encoding]::UTF8), "application/json"
    if ($AuthToken) {
        $req.Headers.Authorization = New-Object -TypeName System.Net.Http.Headers.AuthenticationHeaderValue -ArgumentList "Bearer", $AuthToken
    }
    try {
        $resp = $script:Http.SendAsync($req).GetAwaiter().GetResult()
        $raw = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        return [pscustomobject]@{ Status = [int]$resp.StatusCode; Raw = [string]$raw }
    } catch {
        return [pscustomobject]@{ Status = 0; Raw = [string]$_.Exception.Message }
    }
}

function Invoke-Tool {
    param([string]$Name, $Arguments = @{})
    $body = New-RpcBody @{ jsonrpc = "2.0"; id = 100; method = "tools/call"; params = @{ name = $Name; arguments = $Arguments } }
    $r = Invoke-Mcp $body
    $j = ConvertFrom-JsonSafe $r.Raw
    $text = ""
    $isError = $true
    if ($j -and $j.result) {
        $isError = [bool]$j.result.isError
        if ($j.result.content) { $text = [string]$j.result.content[0].text }
    }
    return [pscustomobject]@{ Status = $r.Status; Json = $j; Text = $text; IsError = $isError; Raw = $r.Raw }
}

Write-Host ("== DT_Tools MCP 验证  " + $Url + " ==") -ForegroundColor Cyan

$init = Invoke-Mcp (New-RpcBody @{ jsonrpc = "2.0"; id = 1; method = "initialize"; params = @{ protocolVersion = "2025-06-18"; capabilities = @{}; clientInfo = @{ name = "mcp-verify"; version = "1.0" } } })
Write-Check "1. initialize 返回 HTTP 200" ($init.Status -eq 200) ("(HTTP " + $init.Status + ")")
if ($init.Status -ne 200) {
    if ($init.Status -eq 503) { Write-Host "MCP 未启用（503 MCP bridge disabled）：请在游戏里打开 McpOptions.Enabled。" -ForegroundColor Yellow }
    elseif ($init.Status -eq 401) { Write-Host "需要密码：请加 -Token 参数。" -ForegroundColor Yellow }
    else { Write-Host ("无法连接或响应异常：" + $init.Raw) -ForegroundColor Yellow }
    Write-Host ("结果：通过 " + $script:PassCount + "，失败 " + $script:FailCount) -ForegroundColor Cyan
    exit 1
}
$initJ = ConvertFrom-JsonSafe $init.Raw
Write-Check "2. 协议版本协商为 2025-06-18" ($initJ.result.protocolVersion -eq "2025-06-18") ("(" + $initJ.result.protocolVersion + ")")
Write-Check "3. 声明 tools 能力" ($null -ne $initJ.result.capabilities.tools)

$note = Invoke-Mcp (New-RpcBody @{ jsonrpc = "2.0"; method = "notifications/initialized" })
Write-Check "4. notifications/initialized 返回 202（无回包）" ($note.Status -eq 202) ("(HTTP " + $note.Status + ")")

$list = Invoke-Mcp (New-RpcBody @{ jsonrpc = "2.0"; id = 2; method = "tools/list" })
$listJ = ConvertFrom-JsonSafe $list.Raw
$names = @()
if ($listJ -and $listJ.result -and $listJ.result.tools) { $names = @($listJ.result.tools | ForEach-Object { $_.name }) }
$expected = @("list_commands", "run_command", "get_config", "set_config", "read_log")
$missing = @($expected | Where-Object { $names -notcontains $_ })
Write-Check "5. tools/list 包含全部 5 个工具" ($missing.Count -eq 0) ("(实际: " + ($names -join ", ") + ")")

$t = Invoke-Tool "list_commands"
Write-Check "6. list_commands 调用成功" (-not $t.IsError) ("(HTTP " + $t.Status + ")")
Write-Check "7. list_commands 每条命令带 tags 字段" ($t.Text -match '"tags"')

$t = Invoke-Tool "run_command" @{ command = "/packets C_READY" }
Write-Check "8. run_command 执行 /packets C_READY" ((-not $t.IsError) -and ($t.Text -match "C_READY"))

$t = Invoke-Tool "get_config" @{ section = $Section }
Write-Check ("9. get_config 读取段 " + $Section) ((-not $t.IsError) -and ($t.Text -match [regex]::Escape($Section)))

$t = Invoke-Tool "read_log" @{ count = 5 }
Write-Check "10. read_log 读取最近 5 条" (-not $t.IsError)

$bad = Invoke-Mcp (New-RpcBody @{ jsonrpc = "2.0"; id = 3; method = "no/such/method" })
$badJ = ConvertFrom-JsonSafe $bad.Raw
Write-Check "11. 未知方法返回 -32601" ($badJ.error.code -eq -32601)

$t = Invoke-Tool "no_such_tool"
Write-Check "12. 未知工具返回 -32602" ($t.Json.error.code -eq -32602)

$p = Invoke-Mcp "{ this is not json"
$pJ = ConvertFrom-JsonSafe $p.Raw
Write-Check "13. 非法 JSON 返回 HTTP 400 与 -32700" (($p.Status -eq 400) -and ($pJ.error.code -eq -32700))

$t = Invoke-Tool "run_command" @{}
Write-Check "14. run_command 缺少 command 参数被拒绝" (($t.IsError) -or ($null -ne $t.Json.error))

if ($Token) {
    $noAuth = Invoke-Mcp (New-RpcBody @{ jsonrpc = "2.0"; id = 4; method = "tools/list" }) ""
    Write-Check "15. 未带密码返回 401" ($noAuth.Status -eq 401) ("(HTTP " + $noAuth.Status + ")")
    $wrong = Invoke-Mcp (New-RpcBody @{ jsonrpc = "2.0"; id = 5; method = "tools/list" }) "wrong-password"
    Write-Check "16. 错误密码返回 401" ($wrong.Status -eq 401) ("(HTTP " + $wrong.Status + ")")
} else {
    Write-Host "[SKIP] 15-16 鉴权：未提供 -Token（游戏里密码为空时无需检查）" -ForegroundColor Yellow
}

Write-Host ""
Write-Host ("结果：通过 " + $script:PassCount + "，失败 " + $script:FailCount) -ForegroundColor Cyan
if ($script:FailCount -gt 0) { exit 1 } else { exit 0 }
