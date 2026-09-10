$headers = @{ "X-User-Id"="admin"; "X-User-Roles"="Admin"; "X-User-Access"="5" }
$docs = Invoke-RestMethod -Uri "http://localhost:5000/api/documents" -Headers $headers
$ep = $docs | Where-Object { $_.title -like "*Employement*" }
$epFull = Invoke-RestMethod -Uri "http://localhost:5000/api/documents/$($ep.id)" -Headers $headers
Write-Host $epFull.content.Substring(500, [Math]::Min(2500, $epFull.content.Length - 500))
