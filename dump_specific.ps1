$headers = @{ "X-User-Id"="admin"; "X-User-Roles"="Admin"; "X-User-Access"="5" }
$docs = Invoke-RestMethod -Uri "http://localhost:5000/api/documents" -Headers $headers

foreach ($target in @("Employement Policies", "Disciplinary Policy", "Employee Benefits")) {
    $d = $docs | Where-Object { $_.title -like "*$target*" }
    Write-Host "=========================================================="
    Write-Host "DOC: $($d.title)"
    Write-Host "=========================================================="
    $full = Invoke-RestMethod -Uri "http://localhost:5000/api/documents/$($d.id)" -Headers $headers
    Write-Host $full.content
}
