# Developer tooling only; this is not part of the messenger installer.
$ErrorActionPreference = 'Stop'

function ConvertTo-StitchApiKey {
    param([AllowNull()][AllowEmptyString()][string]$Value)
    $taskValue = if ($null -eq $Value) { '' } else { $Value.Trim() }
    if (!$taskValue) { throw 'No key was entered. Paste the API key from Stitch settings.' }
    if ($taskValue.Length -gt 8192) { throw 'The input is too long. Paste only the API key, not a configuration or command.' }
    # Validate safe HTTP header input, not an undocumented Google key format.
    if ($taskValue -cnotmatch '\A[\x21-\x7E]+\z') { throw 'The input contains spaces, line breaks, or non-ASCII characters. Paste only the API key.' }
    return $taskValue
}

try {
    Add-Type -AssemblyName System.Windows.Forms, System.Drawing, System.Security
    $taskCodexDir = if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $env:USERPROFILE '.codex' }
    $taskCredentialDir = Join-Path $taskCodexDir 'stitch'
    $taskHelper = Join-Path $taskCredentialDir 'headers.ps1'
    $taskConfig = Join-Path $taskCodexDir 'config.toml'
    $taskExisting = if (Test-Path -LiteralPath $taskConfig) { [IO.File]::ReadAllText($taskConfig) } else { '' }
    $taskMarker = '# yymessenger-stitch-local-credentials'
    if ($taskExisting -match '(?m)^\s*\[mcp_servers\.(?:stitch|"stitch")\]' -and !$taskExisting.Contains($taskMarker)) {
        throw 'An existing Stitch MCP configuration was found. It was not changed.'
    }

    $form = New-Object Windows.Forms.Form
    $form.Text = 'Stitch API key - local setup'
    $form.ClientSize = New-Object Drawing.Size(480, 185)
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    $label = New-Object Windows.Forms.Label
    $label.Text = "Paste a key from stitch.withgoogle.com/settings.`nSaved with Windows DPAPI for your account. Never send it in chat."
    $label.SetBounds(20, 18, 440, 48)
    $box = New-Object Windows.Forms.TextBox
    $box.UseSystemPasswordChar = $true
    $box.SetBounds(20, 78, 440, 27)
    $save = New-Object Windows.Forms.Button
    $save.Text = 'Save and connect'
    $save.SetBounds(210, 130, 130, 30)
    $save.DialogResult = 'OK'
    $cancel = New-Object Windows.Forms.Button
    $cancel.Text = 'Cancel'
    $cancel.SetBounds(350, 130, 110, 30)
    $cancel.DialogResult = 'Cancel'
    $form.Controls.AddRange(@($label, $box, $save, $cancel))
    $form.AcceptButton = $save
    $form.CancelButton = $cancel
    if ($form.ShowDialog() -ne [Windows.Forms.DialogResult]::OK) { $form.Dispose(); exit 0 }
    $taskInput = $box.Text
    $box.Clear()
    $form.Dispose()
    try { $taskKey = ConvertTo-StitchApiKey $taskInput } finally { $taskInput = $null }

    [IO.Directory]::CreateDirectory($taskCredentialDir) | Out-Null
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Stitch-Headers.ps1') -Destination $taskHelper -Force
    $taskKeyBytes = [Text.Encoding]::UTF8.GetBytes($taskKey)
    $taskKey = $null
    try {
        $taskProtected = [Security.Cryptography.ProtectedData]::Protect(
            $taskKeyBytes, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        [IO.File]::WriteAllBytes((Join-Path $taskCredentialDir 'key.dpapi'), $taskProtected)
    } finally { [Array]::Clear($taskKeyBytes, 0, $taskKeyBytes.Length) }

    if (!$taskExisting.Contains($taskMarker)) {
        # Only append our own new table; preserve the rest of the user's configuration.
        $taskCommand = 'powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' + $taskHelper + '"'
        $taskCommandJson = ConvertTo-Json -InputObject $taskCommand -Compress
        $taskBlock = @"

$taskMarker
[mcp_servers.stitch]
url = "https://stitch.googleapis.com/mcp"
http_headers_helper = $taskCommandJson
startup_timeout_sec = 30
tool_timeout_sec = 300
"@
        [IO.File]::AppendAllText($taskConfig, "`n" + $taskBlock + "`n", (New-Object Text.UTF8Encoding($false)))
    }
    [Windows.Forms.MessageBox]::Show(
        'Key saved locally. Restart Codex to load the Stitch MCP connection. Key validity will be checked by Stitch.',
        'Stitch setup complete') | Out-Null
} catch {
    # Never include API responses or a credential in an error message.
    [Windows.Forms.MessageBox]::Show($_.Exception.Message, 'Stitch setup failed') | Out-Null
    exit 1
}
