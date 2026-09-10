$questions = @(
    "What is the difference in validity period between a first written warning and a final written warning?",
    "Can an employee bring a lawyer to a disciplinary hearing?",
    "What happens if an associate fails to attend a disciplinary hearing three times without good reason?",
    "What is the annual wellness allowance amount?",
    "Which insurance company does Quokka Labs partner with for Mediclaim?",
    "What is the health coverage amount under the Group Mediclaim policy?",
    "By what date each month must wellness allowance bills be uploaded on the greythr portal?",
    "Can the wellness allowance be used for a spouse's gym membership?",
    "What documents are required for a preventive blood check-up reimbursement?",
    "Does unused wellness allowance carry forward to the next year?",
    "What are Quokka Labs' standard working hours?",
    "How long is the probation period?",
    "How many days of notice must an employee give before resigning?",
    "Within how many days is the Full & Final settlement processed?",
    "How many days per month can an employee work from home under the hybrid policy, and is there a limit on consecutive days?",
    "Is the hybrid work policy applicable during an employee's notice period?",
    "Who owns inventions and patents created by an employee during employment?"
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
