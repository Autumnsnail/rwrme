$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$baseUrl = 'http://127.0.0.1:8088'
function Test-McpServer {
    try {
        $health = Invoke-RestMethod "$baseUrl/health" -TimeoutSec 2
        if ($health.message -ne 'MCP for Unity server is running' -or $health.version -ne '10.0.0') {
            throw 'Port 8088 is occupied by a different service or Unity MCP version.'
        }
        return $true
    } catch [System.Net.WebException] { return $false }
}
if (Test-McpServer) { Write-Output "Unity MCP is ready: $baseUrl/mcp"; return }
$serverExe = Join-Path $projectRoot '.unity-mcp-source/Server/.venv/Scripts/mcp-for-unity.exe'
if (-not (Test-Path -LiteralPath $serverExe)) { throw 'Run Tools/UnityMcp/Setup-UnityMcp.ps1 first.' }
$logRoot = Join-Path $projectRoot 'Library/UnityMcp'
New-Item -ItemType Directory -Force -Path $logRoot | Out-Null
$serverProcess = Start-Process -FilePath $serverExe -ArgumentList @(
    '--transport', 'http', '--http-url', $baseUrl, '--default-instance', 'rwrme'
) -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru `
    -RedirectStandardOutput (Join-Path $logRoot 'server.stdout.log') `
    -RedirectStandardError (Join-Path $logRoot 'server.stderr.log')
for ($attempt = 0; $attempt -lt 40; $attempt++) {
    if (Test-McpServer) { Write-Output "Unity MCP is ready: $baseUrl/mcp"; return }
    if ($serverProcess.HasExited) { throw "Unity MCP exited. See $logRoot/server.stderr.log" }
    Start-Sleep -Milliseconds 500
}
throw "Unity MCP did not become ready. See $logRoot/server.stderr.log"
