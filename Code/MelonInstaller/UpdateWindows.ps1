param([int]$ServerProcess, [string]$DataDirectory, [switch]$Service, [switch]$LeaveStopped)
$ErrorActionPreference = 'Stop'
$stage = Get-Item -LiteralPath $PSScriptRoot
$root = $stage.Parent.FullName
if ($stage.Name -notmatch '^\.melon-update-[0-9a-f]{32}$') { throw 'Unexpected staging directory' }
$payload = Join-Path $stage.FullName 'payload'
$backup = Join-Path $stage.FullName 'previous'
$exe = Join-Path $root 'Server\MelonWebApi.exe'
$previous = [Collections.Generic.List[string]]::new()
$installed = [Collections.Generic.List[string]]::new()
$clean = $false
$ready = $false
try {
    $process = if ($ServerProcess -gt 0) { Get-Process -Id $ServerProcess -ErrorAction SilentlyContinue }
    if ($process -and !$process.WaitForExit(120000)) { throw 'Melon is still running.' }
    $ready = $true
    foreach ($controllerPath in 'Controller\MelonGUI.exe','CLI\MelonCLI.exe') {
        $controller = Join-Path $root $controllerPath
        $name = [IO.Path]::GetFileNameWithoutExtension($controllerPath)
        foreach ($app in Get-Process -Name $name -ErrorAction SilentlyContinue) {
            if ($app.Path -eq $controller -and !$app.WaitForExit(30000)) { throw 'Close the other Melon controllers and retry.' }
        }
    }
    foreach ($component in 'Server','Controller','CLI','Installer') {
        if ($component -in 'CLI','Installer' -and !(Test-Path -LiteralPath (Join-Path $payload $component))) { continue }
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $payload $component) -File -Recurse) {
            $relative = $file.FullName.Substring($payload.Length + 1)
            $destination = Join-Path $root $relative
            $old = Join-Path $backup $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            if ([IO.File]::Exists($destination)) {
                [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($old)) | Out-Null
                [IO.File]::Move($destination, $old)
                $previous.Add($relative)
            }
            [IO.File]::Move($file.FullName, $destination)
            $installed.Add($relative)
        }
    }
    $clean = $true
} catch {
    $_ | Out-File -LiteralPath (Join-Path $stage.FullName 'error.log')
    foreach ($file in $installed) { [IO.File]::Delete((Join-Path $root $file)) }
    foreach ($file in $previous) { [IO.File]::Move((Join-Path $backup $file), (Join-Path $root $file)) }
}
if ($ready -and !$LeaveStopped) {
    if ($Service) { Start-Service -Name Melon }
    else { Start-Process -FilePath $exe -ArgumentList @('--dataPath',('"'+$DataDirectory.TrimEnd('\')+'"')) -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden }
}
if ($clean) { Remove-Item -LiteralPath $stage.FullName -Recurse -Force }
