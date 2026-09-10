$headers = @{ "X-User-Id"="admin"; "X-User-Roles"="Admin"; "X-User-Access"="5" }
$docs = Invoke-RestMethod -Uri "http://localhost:5000/api/documents" -Headers $headers

foreach ($d in $docs) {
    Write-Host "=========================================================="
    Write-Host "DOC: $($d.title) (File: $($d.fileName))"
    Write-Host "=========================================================="
    $full = Invoke-RestMethod -Uri "http://localhost:5000/api/documents/$($d.id)" -Headers $headers
    Write-Host $full.content
}
