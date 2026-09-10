$questions = @(
    "What is the company's policy for employees working on Mars",
    "What is the work-from-home policy?",
    "What are the rules for probationers regarding leaves?",
    "How many public holidays do employees get in a year?",
    "What is the policy for accumulation and encashment of earned leaves?",
    "Can employees accept gifts or entertainment from suppliers?",
    "What is the maternity leave duration?",
    "What are the non-solicitation rules?"
)

foreach ($q in $questions) {
    Write-Host "================================================================="
    Write-Host "QUESTION: $q"
    Write-Host "-----------------------------------------------------------------"
    $payload = @{
        question = $q
        userClaims = @{
            userId = "usr-elena-05"
            userName = "Elena Rostova"
            department = "Executive"
            roles = @("Executive", "Board", "Admin")
            accessLevel = 5
        }
    } | ConvertTo-Json
    
    $resp = Invoke-RestMethod -Uri "http://localhost:5000/api/chat" -Method Post -ContentType "application/json" -Body $payload
    Write-Host "ANSWER:"
    Write-Host $resp.answer
    Write-Host "CITATIONS COUNT: $($resp.sources.Count)"
    Write-Host ""
}
