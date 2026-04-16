# DEPLOYMENT SCRIPT - Call Automation GCCH Application
# ===============================================================================
#
# TARGET: gcch-contoso-app VM in Azure US Government Cloud
#
# The VM runs Kestrel directly (no IIS). The app is deployed to C:\app\CallAutomation
# and registered as a scheduled task that auto-starts on boot.
#
# PREREQUISITES:
# 1. Azure PowerShell module: Install-Module -Name Az -Force
# 2. .NET 8 SDK installed locally
# 3. Azure US Gov credentials with VM Run Command + Network permissions
#
# USAGE:
#   cd "<project-directory>"
#   .\DeploymentScript.ps1
#
# ===============================================================================

$SubscriptionId    = '80ae91d7-9d71-46a5-ad81-6a10c333007e'
$ResourceGroupName = 'waferwire-rg'
$VmName            = 'gcch-contoso-app'
$NsgName           = 'gcch-contoso-app-nsg'
$AppName           = 'Call_Automation_GCCH'
$RemoteAppDir      = 'C:\app\CallAutomation'
$RemoteTempDir     = 'C:\Temp'
$ChunkSizeBytes    = 500000

Write-Host "`n============================================" -ForegroundColor Cyan
Write-Host "  Deploying $AppName to $VmName"               -ForegroundColor Cyan
Write-Host "============================================"   -ForegroundColor Cyan

# ?? 1. Connect ????????????????????????????????????????????????????????????????
Write-Host "`n[1/7] Connecting to Azure US Government..." -ForegroundColor Yellow
Connect-AzAccount -Environment AzureUSGovernment -UseDeviceAuthentication
Set-AzContext -SubscriptionId $SubscriptionId

# ?? 2. Publish ????????????????????????????????????????????????????????????????
Write-Host "`n[2/7] Publishing .NET 8 application..." -ForegroundColor Yellow
$PublishFolder = '.\publish'
$ZipPath = '.\publish.zip'
if (Test-Path $PublishFolder) { Remove-Item $PublishFolder -Recurse -Force }
if (Test-Path $ZipPath)       { Remove-Item $ZipPath -Force }

dotnet publish -c Release -o $PublishFolder --nologo
if ($LASTEXITCODE -ne 0) { Write-Host "ERROR: publish failed" -ForegroundColor Red; exit 1 }

# ?? 3. Compress ???????????????????????????????????????????????????????????????
Write-Host "`n[3/7] Compressing..." -ForegroundColor Yellow
Compress-Archive -Path "$PublishFolder\*" -DestinationPath $ZipPath -Force
$zipSize = (Get-Item $ZipPath).Length
Write-Host "  ZIP: $([math]::Round($zipSize / 1MB, 2)) MB"

# ?? 4. Upload to VM in chunks ????????????????????????????????????????????????
Write-Host "`n[4/7] Uploading to VM..." -ForegroundColor Yellow
$remoteZip = "$RemoteTempDir\publish.zip"

Invoke-AzVMRunCommand -ResourceGroupName $ResourceGroupName -VMName $VmName `
    -CommandId 'RunPowerShellScript' `
    -ScriptString "New-Item '$RemoteTempDir' -ItemType Directory -Force|Out-Null; if(Test-Path '$remoteZip'){Remove-Item '$remoteZip' -Force}" | Out-Null

$zipBytes = [IO.File]::ReadAllBytes((Resolve-Path $ZipPath).Path)
$totalChunks = [math]::Ceiling($zipBytes.Length / $ChunkSizeBytes)

for ($i = 0; $i -lt $totalChunks; $i++) {
    $off = $i * $ChunkSizeBytes
    $len = [math]::Min($ChunkSizeBytes, $zipBytes.Length - $off)
    $ch  = New-Object byte[] $len
    [Array]::Copy($zipBytes, $off, $ch, 0, $len)
    $enc = [Convert]::ToBase64String($ch)

    $s = "`$c=[Convert]::FromBase64String('$enc');`$f=[IO.File]::Open('$remoteZip',[IO.FileMode]::Append,[IO.FileAccess]::Write);`$f.Write(`$c,0,`$c.Length);`$f.Close()"
    $num = $i + 1
    Write-Host "  Chunk $num/$totalChunks..." -NoNewline
    $ok = $false
    for ($retry = 0; $retry -lt 3 -and !$ok; $retry++) {
        try {
            Invoke-AzVMRunCommand -ResourceGroupName $ResourceGroupName -VMName $VmName `
                -CommandId 'RunPowerShellScript' -ScriptString $s -ErrorAction Stop | Out-Null
            $ok = $true
        } catch { Write-Host " (retry)" -NoNewline; Start-Sleep 5 }
    }
    if (!$ok) { Write-Host " FAILED" -ForegroundColor Red; exit 1 }
    Write-Host " ok" -ForegroundColor Green
}

# ?? 5. Deploy on VM ??????????????????????????????????????????????????????????
Write-Host "`n[5/7] Deploying on VM..." -ForegroundColor Yellow

$deployCmd = "Get-Process dotnet -EA SilentlyContinue|Stop-Process -Force -EA SilentlyContinue; Start-Sleep 2; if(Test-Path '$RemoteAppDir'){Remove-Item '$RemoteAppDir' -Recurse -Force -EA SilentlyContinue}; New-Item '$RemoteAppDir' -ItemType Directory -Force|Out-Null; Expand-Archive -Path '$remoteZip' -DestinationPath '$RemoteAppDir' -Force -EA Stop; New-Item '$RemoteAppDir\logs' -ItemType Directory -Force|Out-Null; Remove-Item '$remoteZip' -Force -EA SilentlyContinue; Write-Output `"DLL:`$(Test-Path '$RemoteAppDir\$AppName.dll')`"; Write-Output `"wwwroot:`$(Test-Path '$RemoteAppDir\wwwroot')`"; Write-Output `"Files:`$((Get-ChildItem '$RemoteAppDir' -Recurse -File).Count)`""

$r = Invoke-AzVMRunCommand -ResourceGroupName $ResourceGroupName -VMName $VmName `
    -CommandId 'RunPowerShellScript' -ScriptString $deployCmd
Write-Host "  $($r.Value[0].Message)"

# ?? 6. Create SSL cert + start Kestrel + firewall + scheduled task ????????????
Write-Host "`n[6/7] Configuring HTTPS certificate, firewall, and starting app..." -ForegroundColor Yellow

# Create self-signed cert on VM if it doesn't exist, then export PFX
$certScript = '$dn="gcch-contoso-app.usgovtexas.cloudapp.usgovcloudapi.net";$c=Get-ChildItem Cert:\LocalMachine\My|Where-Object{$_.Subject -like "*$dn*"};if(!$c){$c=New-SelfSignedCertificate -DnsName $dn -CertStoreLocation "Cert:\LocalMachine\My" -NotAfter (Get-Date).AddYears(5)};$pw=ConvertTo-SecureString "CallAuto2024!" -Force -AsPlainText;Export-PfxCertificate -Cert $c -FilePath "' + $RemoteAppDir + '\cert.pfx" -Password $pw|Out-Null;Write-Output "Cert:$($c.Thumbprint)"'
$enc = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($certScript))
$certCmd = "[IO.File]::WriteAllBytes('$RemoteTempDir\cert.ps1',[Convert]::FromBase64String('$enc'));powershell -ExecutionPolicy Bypass -File '$RemoteTempDir\cert.ps1'"
$r = Invoke-AzVMRunCommand -ResourceGroupName $ResourceGroupName -VMName $VmName `
    -CommandId 'RunPowerShellScript' -ScriptString $certCmd
Write-Host "  $($r.Value[0].Message)"

# Open firewall for both HTTP and HTTPS
$fwCmd = "netsh advfirewall firewall delete rule name='CallAutomation HTTP' 2>`$null; netsh advfirewall firewall add rule name='CallAutomation HTTP' dir=in action=allow protocol=tcp localport=80; netsh advfirewall firewall delete rule name='CallAutomation HTTPS' 2>`$null; netsh advfirewall firewall add rule name='CallAutomation HTTPS' dir=in action=allow protocol=tcp localport=443; Write-Output 'Firewall open: 80,443'"
$r = Invoke-AzVMRunCommand -ResourceGroupName $ResourceGroupName -VMName $VmName `
    -CommandId 'RunPowerShellScript' -ScriptString $fwCmd
Write-Host "  $($r.Value[0].Message)"

# Start Kestrel (reads HTTP+HTTPS endpoints from appsettings.json Kestrel config)
$startCmd = "Get-Process dotnet -EA SilentlyContinue|Stop-Process -Force -EA SilentlyContinue; Start-Sleep 3; `$env:ASPNETCORE_ENVIRONMENT='Production'; Start-Process -FilePath 'C:\Program Files\dotnet\dotnet.exe' -ArgumentList '$RemoteAppDir\$AppName.dll' -WorkingDirectory '$RemoteAppDir' -WindowStyle Hidden; Start-Sleep 10; try{`$r=Invoke-WebRequest -Uri 'http://localhost/swagger/v1/swagger.json' -UseBasicParsing -TimeoutSec 10; Write-Output `"HTTP test: `$(`$r.StatusCode)`"}catch{Write-Output `"HTTP test FAILED: `$(`$_.Exception.Message)`"}"
$r = Invoke-AzVMRunCommand -ResourceGroupName $ResourceGroupName -VMName $VmName `
    -CommandId 'RunPowerShellScript' -ScriptString $startCmd
Write-Host "  $($r.Value[0].Message)"

# Register scheduled task for auto-start on reboot (no --urls, Kestrel reads from appsettings.json)
$taskScript = 'schtasks /delete /tn "CallAutomationGCCH" /f 2>$null; $tr = ''\"C:\Program Files\dotnet\dotnet.exe\" \"' + $RemoteAppDir + '\' + $AppName + '.dll\"''; schtasks /create /tn "CallAutomationGCCH" /tr $tr /sc onstart /ru SYSTEM /rl HIGHEST /f; Write-Output "Task registered"'
$enc = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($taskScript))
$taskCmd = "[IO.File]::WriteAllBytes('$RemoteTempDir\task.ps1',[Convert]::FromBase64String('$enc')); powershell -ExecutionPolicy Bypass -File '$RemoteTempDir\task.ps1'"
$r = Invoke-AzVMRunCommand -ResourceGroupName $ResourceGroupName -VMName $VmName `
    -CommandId 'RunPowerShellScript' -ScriptString $taskCmd
Write-Host "  $($r.Value[0].Message)"

# ?? 7. Ensure NSG allows ports 80+443 + resolve URL ?????????????????????????
Write-Host "`n[7/7] Verifying NSG and resolving URL..." -ForegroundColor Yellow

$nsg = Get-AzNetworkSecurityGroup -Name $NsgName -ResourceGroupName $ResourceGroupName
foreach ($port in @(@{Name='HTTP';Port='80';Pri=310}, @{Name='HTTPS';Port='443';Pri=320})) {
    $rule = $nsg.SecurityRules | Where-Object { $_.DestinationPortRange -contains $port.Port -and $_.Direction -eq 'Inbound' -and $_.Access -eq 'Allow' }
    if (-not $rule) {
        $nsg | Add-AzNetworkSecurityRuleConfig -Name $port.Name -Description "Allow $($port.Name)" `
            -Access Allow -Protocol Tcp -Direction Inbound -Priority $port.Pri `
            -SourceAddressPrefix '*' -SourcePortRange '*' `
            -DestinationAddressPrefix '*' -DestinationPortRange $port.Port | Out-Null
        Write-Host "  Added NSG rule for port $($port.Port)" -ForegroundColor Yellow
    } else {
        Write-Host "  NSG port $($port.Port) already open" -ForegroundColor Green
    }
}
$nsg | Set-AzNetworkSecurityGroup | Out-Null

# Resolve public DNS
$vm = Get-AzVM -ResourceGroupName $ResourceGroupName -Name $VmName
$nicId = $vm.NetworkProfile.NetworkInterfaces[0].Id
$nic = Get-AzNetworkInterface -Name $nicId.Split('/')[-1] -ResourceGroupName $nicId.Split('/')[4]
$pipRef = $nic.IpConfigurations[0].PublicIpAddress
$appUrl = "https://<VM-DNS>/swagger"
if ($pipRef -and $pipRef.Id) {
    $pip = Get-AzPublicIpAddress -Name $pipRef.Id.Split('/')[-1] -ResourceGroupName $pipRef.Id.Split('/')[4]
    if ($pip.DnsSettings.Fqdn) { $appUrl = "https://$($pip.DnsSettings.Fqdn)/swagger" }
    elseif ($pip.IpAddress -and $pip.IpAddress -ne 'Not Assigned') { $appUrl = "https://$($pip.IpAddress)/swagger" }
}

# ?? Cleanup ???????????????????????????????????????????????????????????????????
Remove-Item $PublishFolder -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $ZipPath -Force -ErrorAction SilentlyContinue

Write-Host "`n============================================" -ForegroundColor Green
Write-Host "  Deployment complete!"                         -ForegroundColor Green
Write-Host "  URL: $appUrl"                                 -ForegroundColor Green
Write-Host "============================================"   -ForegroundColor Green
