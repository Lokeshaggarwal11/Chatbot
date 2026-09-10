$questions = @(
    "What is the difference in validity period between a first written warning and a final written warning?",
    "Can an employee bring a lawyer to a disciplinary hearing?",
    "What happens if an associate fails to attend a disciplinary hearing three times without good reason?",
    "What is the annual wellness allowance amount?",
    "Which insurance company does Quokka Labs partner with for Mediclaim?",
    "What is the health coverage amount under the Group Mediclaim policy?",
    "By what date each month must wellness allowance bills be uploaded on the greythr portal?",
    "Can the wellness allowance be used for a spouse's gym membership?",
    "What documents are required for a preventive blood check-up reimbursement?"
)

for ($idx = 0; $idx -lt $questions.Count; $idx++) {
    $q = $questions[$idx]
    Write-Host "================================================================="
    Write-Host "[$($idx+11)] $q"
    Write-Host "-----------------------------------------------------------------"
    $payload = @{
        question = $q
        userClaims = @{
            userId = "usr-elena-05"
            userName = "Elena Rostova"
            department = "Executive"
            roles = @("Executive", "Board", "Admin", "Employee")
            accessLevel = 5
        }
        topK = 5
    } | ConvertTo-Json
    
    $resp = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" -Method Post -ContentType "application/json" -Body $payload
    Write-Host $resp.answer
    Write-Host ""
}
