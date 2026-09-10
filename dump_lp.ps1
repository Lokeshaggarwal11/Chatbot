$headers = @{
    "X-User-Id" = "admin"
    "X-User-Name" = "Elena"
    "X-User-Roles" = "Admin,Executive"
    "X-User-Dept" = "Executive"
    "X-User-Access" = "5"
}

$docs = Invoke-RestMethod -Uri "http://localhost:5000/api/documents" -Method Get -Headers $headers
$lp = $docs | Where-Object { $_.title -like "*Leave*" }
$full = Invoke-RestMethod -Uri "http://localhost:5000/api/documents/$($lp.id)" -Method Get -Headers $headers
Write-Host $full.content
