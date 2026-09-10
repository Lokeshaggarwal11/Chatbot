$headers = @{ "X-User-Id"="admin"; "X-User-Roles"="Admin"; "X-User-Access"="5" }
$docs = Invoke-RestMethod -Uri "http://localhost:5000/api/documents" -Headers $headers
$ep = $docs | Where-Object { $_.title -like "*Employement*" }
$full = Invoke-RestMethod -Uri "http://localhost:5000/api/documents/$($ep.id)" -Headers $headers
Write-Host $full.content
