$questions = @(
    # Code of Ethics
    "What is the maximum value of a gift an employee can accept without it becoming a conflict of interest?",
    "Who must approve a conflict of interest before it arises?",
    "How long is the non-solicitation period after termination?",
    "List the six situations that may give rise to a Conflict of Interest under the Code of Ethics.",
    "What happens to remuneration or stock options an employee receives from an outside assignment held on behalf of the Company?",
    "Under which SEBI regulation are all Associates considered `"Insiders`"?",

    # Disciplinary Policy
    "Who are the three members of the Disciplinary Committee (by role, not name)?",
    "How many working days does the Disciplinary Committee have to complete an investigation?",
    "How long does a first written warning remain valid?",
    "Walk me through the three steps of the Formal Disciplinary Process.",
    "What is the difference in validity period between a first written warning and a final written warning?",
    "Can an employee bring a lawyer to a disciplinary hearing?",
    "What happens if an associate fails to attend a disciplinary hearing three times without good reason?",

    # Employee Benefits
    "What is the annual wellness allowance amount?",
    "Which insurance company does Quokka Labs partner with for Mediclaim?",
    "What is the health coverage amount under the Group Mediclaim policy?",
    "By what date each month must wellness allowance bills be uploaded on the greythr portal?",
    "Can the wellness allowance be used for a spouse's gym membership?",
    "What documents are required for a preventive blood check-up reimbursement?",
    "Does unused wellness allowance carry forward to the next year?",

    # Employment Policies
    "What are Quokka Labs' standard working hours?",
    "How long is the probation period?",
    "How many days of notice must an employee give before resigning?",
    "Within how many days is the Full & Final settlement processed?",
    "How many days per month can an employee work from home under the hybrid policy, and is there a limit on consecutive days?",
    "Is the hybrid work policy applicable during an employee's notice period?",
    "Who owns inventions and patents created by an employee during employment?"
)

$i = 1
foreach ($q in $questions) {
    Write-Host "================================================================="
    Write-Host "[$i/27] QUESTION: $q"
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
    Write-Host "CITATIONS ($($resp.sources.Count)):"
    foreach ($s in $resp.sources) {
        Write-Host "  - $($s.documentTitle) (Page $($s.pageNumber)) [Score: $($s.relevanceScore)]"
    }
    Write-Host ""
    $i++
}
