$headers = @{
    "X-User-Id" = "admin"
    "X-User-Roles" = "Admin,Executive"
    "X-User-Access" = "5"
}

$docs = Invoke-RestMethod -Uri "http://localhost:5000/api/documents" -Method Get -Headers $headers
Write-Host "Total Documents in DB: $($docs.Count)"
foreach ($d in $docs) {
    Write-Host "ID: $($d.id) | Title: $($d.title) | Chunks: $($d.chunkCount) | File: $($d.fileName)"
}
