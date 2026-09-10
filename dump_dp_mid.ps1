$headers = @{ "X-User-Id"="admin"; "X-User-Roles"="Admin"; "X-User-Access"="5" }
$docs = Invoke-RestMethod -Uri "http://localhost:5000/api/documents" -Headers $headers
$dp = $docs | Where-Object { $_.title -like "*Disciplinary*" }
$dpFull = Invoke-RestMethod -Uri "http://localhost:5000/api/documents/$($dp.id)" -Headers $headers
Write-Host $dpFull.content.Substring(4000, [Math]::Min(7000, $dpFull.content.Length - 4000))
