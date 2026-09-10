$docs = Invoke-RestMethod -Uri "http://localhost:5000/api/documents?accessLevel=5&roles=Executive,Board,Admin,Employee" -Method Get
foreach ($d in $docs) {
    Write-Host "=================================================================="
    Write-Host "DOC ID: $($d.id) | TITLE: $($d.title)"
    Write-Host "=================================================================="
    $detail = Invoke-RestMethod -Uri "http://localhost:5000/api/documents/$($d.id)?accessLevel=5&roles=Executive,Board,Admin,Employee" -Method Get
    foreach ($c in $detail.chunks) {
        Write-Host "--- CHUNK PAGE: $($c.pageNumber) ---"
        Write-Host $c.chunkText
        Write-Host ""
    }
}
