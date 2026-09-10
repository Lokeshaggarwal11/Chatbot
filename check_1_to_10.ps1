$questions = @(
    "What is the maximum value of a gift an employee can accept without it becoming a conflict of interest?",
    "Who must approve a conflict of interest before it arises?",
    "How long is the non-solicitation period after termination?",
    "List the six situations that may give rise to a Conflict of Interest under the Code of Ethics.",
    "What happens to remuneration or stock options an employee receives from an outside assignment held on behalf of the Company?",
    "Under which SEBI regulation are all Associates considered `"Insiders`"?",
    "Who are the three members of the Disciplinary Committee (by role, not name)?",
    "How many working days does the Disciplinary Committee have to complete an investigation?",
    "How long does a first written warning remain valid?",
    "Walk me through the three steps of the Formal Disciplinary Process."
)

for ($idx = 0; $idx -lt $questions.Count; $idx++) {
    $q = $questions[$idx]
    Write-Host "================================================================="
    Write-Host "[$($idx+1)] $q"
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
