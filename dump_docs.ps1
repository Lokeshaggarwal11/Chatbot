$headers = @{
    "X-User-Id" = "admin"
    "X-User-Name" = "Elena"
    "X-User-Roles" = "Admin,Executive"
    "X-User-Dept" = "Executive"
    "X-User-Access" = "5"
}

$docs = Invoke-RestMethod -Uri "http://localhost:5000/api/documents" -Method Get -Headers $headers

foreach ($doc in $docs) {
    Write-Host "==============================================="
    Write-Host "DOC ID: $($doc.id) | TITLE: $($doc.title)"
    Write-Host "==============================================="
    $full = Invoke-RestMethod -Uri "http://localhost:5000/api/documents/$($doc.id)" -Method Get -Headers $headers
    Write-Host $full.content
}
