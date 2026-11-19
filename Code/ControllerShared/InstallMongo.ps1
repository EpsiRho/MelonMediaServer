# Embedded in MelonGUI and run after the operator selects the install option.
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$serviceName = 'MelonMongoDB'
$base = Join-Path $env:ProgramW6432 'MelonMongoDB'
$exe = Join-Path $base 'bin\mongod.exe'
$config = Join-Path $base 'mongod.cfg'
$command = '"{0}" --config "{1}" --service' -f $exe, $config
$lock = New-Object System.Threading.Mutex($false, 'Global\Melon.MongoDB.Setup')
if (!$lock.WaitOne(0)) { exit 2 }
try {
    $service = Get-CimInstance Win32_Service -Filter "Name='MelonMongoDB'"
    if ($service -and $service.PathName -ne $command) { exit 3 }
    if (!$service) {
        if (Get-NetTCPConnection -State Listen -LocalPort 27524 -ErrorAction SilentlyContinue) { exit 4 }
        New-Item -Type Directory -Path $base -Force | Out-Null
        if (!(Test-Path -LiteralPath $exe)) {
            $archive = Join-Path $base 'mongodb-8.0.38.zip'
            Invoke-WebRequest -UseBasicParsing -Uri 'https://fastdl.mongodb.org/windows/mongodb-windows-x86_64-8.0.38.zip' -OutFile $archive
            if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '85147AE32F9BB9A2E5A0A3875B0FFCC4423AB7A49BC4F75CB4BBC9811883D3DE') { exit 5 }
            $unpacked = Join-Path $base 'distribution'
            Expand-Archive -LiteralPath $archive -DestinationPath $unpacked -Force
            $source = Get-ChildItem -LiteralPath $unpacked -Filter mongod.exe -File -Recurse | Select-Object -First 1
            if (!$source) { exit 6 }
            [IO.Directory]::Move($source.Directory.FullName, (Join-Path $base 'bin'))
            Remove-Item -LiteralPath $archive
        }
        foreach ($part in 'data','logs') {
            $folder = Join-Path $base $part
            New-Item -Type Directory -Path $folder -Force | Out-Null
            & icacls.exe $folder /inheritance:r /grant:r '*S-1-5-18:(OI)(CI)F' '*S-1-5-32-544:(OI)(CI)F' '*S-1-5-19:(OI)(CI)M' | Out-Null
            if ($LASTEXITCODE) { exit 7 }
        }
        @"
storage:
  dbPath: '$base\data'
systemLog:
  destination: file
  path: '$base\logs\mongo.log'
  logAppend: true
net:
  bindIp: 127.0.0.1
  port: 27524
security:
  authorization: enabled
processManagement:
  windowsService:
    serviceName: MelonMongoDB
"@ | Set-Content -LiteralPath $config -Encoding UTF8
        $created = Invoke-CimMethod -ClassName Win32_Service -MethodName Create -Arguments @{
            Name=$serviceName; DisplayName='Melon MongoDB'; PathName=$command; ServiceType=[byte]16;
            ErrorControl=[byte]1; StartMode='Automatic'; DesktopInteract=$false; StartName='NT AUTHORITY\LocalService'
        }
        if ($created.ReturnValue -ne 0) { exit 8 }
    }
    Start-Service -Name $serviceName
    for ($count = 0; $count -lt 60; $count++) {
        $service = Get-CimInstance Win32_Service -Filter "Name='MelonMongoDB'"
        $listener = Get-NetTCPConnection -State Listen -LocalPort 27524 -ErrorAction SilentlyContinue
        if ($listener -and $listener.LocalAddress -eq '127.0.0.1' -and $listener.OwningProcess -eq $service.ProcessId) { exit 0 }
        Start-Sleep -Milliseconds 500
    }
    exit 9
} catch { exit 1 }
finally { $lock.ReleaseMutex(); $lock.Dispose() }
