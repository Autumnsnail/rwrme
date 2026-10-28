$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceRoot = Join-Path $projectRoot '.unity-mcp-source'
$commit = 'd49ae2953580f3481beb1e084a1da2682f0b5610'
if (-not (Test-Path -LiteralPath $sourceRoot)) {
    & git clone --depth 1 --branch v10.0.0 https://github.com/CoplayDev/unity-mcp.git $sourceRoot
    if ($LASTEXITCODE -ne 0) { throw 'Unity MCP download failed.' }
}
$actualCommit = & git -C $sourceRoot rev-parse HEAD
if ($LASTEXITCODE -ne 0 -or $actualCommit -ne $commit) { throw "Unexpected Unity MCP source version: $actualCommit" }
& uv sync --project (Join-Path $sourceRoot 'Server') --no-dev --frozen
if ($LASTEXITCODE -ne 0) { throw 'Unity MCP dependency installation failed.' }
& (Join-Path $PSScriptRoot 'Start-UnityMcp.ps1')
