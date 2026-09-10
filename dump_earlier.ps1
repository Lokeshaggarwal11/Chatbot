$headers = @{ "X-User-Id"="admin"; "X-User-Roles"="Admin"; "X-User-Access"="5" }
$docs = Invoke-RestMethod -Uri "http://localhost:5000/api/documents" -Headers $headers

$ep = $docs | Where-Object { $_.title -like "*Employement*" }
$dp = $docs | Where-Object { $_.title -like "*Disciplinary*" }

Write-Host "================== EMPLOYMENT POLICIES ================"
$epFull = Invoke-RestMethod -Uri "http://localhost:5000/api/documents/$($ep.id)" -Headers $headers
Write-Host $epFull.content.Substring(0, [Math]::Min(3500, $epFull.content.Length))

Write-Host "================== DISCIPLINARY POLICY ================"
$dpFull = Invoke-RestMethod -Uri "http://localhost:5000/api/documents/$($dp.id)" -Headers $headers
Write-Host $dpFull.content.Substring(0, [Math]::Min(5000, $dpFull.content.Length))
