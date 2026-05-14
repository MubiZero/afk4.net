param(
    [string] $ServiceName = 'AFK4.Agent.Service',

    [int] $DelaySeconds = 5
)

$ErrorActionPreference = 'Stop'

$scriptBlock = {
    param($Name, $Delay)
    Start-Sleep -Seconds $Delay
    Restart-Service -Name $Name -Force
}

Start-Job -ScriptBlock $scriptBlock -ArgumentList $ServiceName, $DelaySeconds | Out-Null
exit 0
