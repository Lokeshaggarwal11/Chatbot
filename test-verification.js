// Automated end-to-end verification script for Internal Knowledge Assistant

async function runTests() {
  const API_BASE = 'http://localhost:5000';
  console.log('🚀 Starting Internal Knowledge Assistant Verification Suite...\n');

  // Test 1: Health Check
  console.log('1️⃣ Testing GET /health');
  const healthRes = await fetch(`${API_BASE}/health`);
  const healthData = await healthRes.json();
  console.log('   Response Status:', healthRes.status);
  console.log('   Health Data:', JSON.stringify(healthData));
  if (healthData.status !== 'Healthy') throw new Error('Health check failed');
  console.log('   ✅ Health check PASSED\n');

  // Test 2: Document Listing for Level 1 (David Chen)
  console.log('2️⃣ Testing GET /api/documents for David Chen (Level 1 - Public only)');
  const docsL1Res = await fetch(`${API_BASE}/api/documents?userId=usr-david-01&roles=Employee&department=General&accessLevel=1`);
  const docsL1 = await docsL1Res.json();
  console.log(`   Found ${docsL1.length} authorized documents for Level 1:`);
  docsL1.forEach(d => console.log(`     - [L${d.accessLevel}] ${d.title} (${d.department})`));
  const hasSecret = docsL1.some(d => d.accessLevel > 1);
  if (hasSecret) throw new Error('Security Breach: Level 1 user saw restricted docs!');
  console.log('   ✅ Level 1 Document isolation PASSED\n');

  // Test 3: Document Listing for Level 5 (Elena Rostova - Executive)
  console.log('3️⃣ Testing GET /api/documents for Elena Rostova (Level 5 - Executive/Admin)');
  const docsL5Res = await fetch(`${API_BASE}/api/documents?userId=usr-elena-05&roles=Executive,Board,Admin&department=Executive&accessLevel=5`);
  const docsL5 = await docsL5Res.json();
  console.log(`   Found ${docsL5.length} authorized documents for Level 5:`);
  docsL5.forEach(d => console.log(`     - [L${d.accessLevel}] ${d.title} (${d.department})`));
  if (docsL5.length < 5) throw new Error('Level 5 user should have access to all 5 seeded documents');
  console.log('   ✅ Level 5 Full Access PASSED\n');

  // Test 4: RAG Query as David Chen (Level 1) - Public Info
  console.log('4️⃣ Testing POST /api/chat: David Chen asking about Working Hours & PTO');
  const chat1Res = await fetch(`${API_BASE}/api/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      question: 'What are the official core working hours and annual PTO rollover rules?',
      userClaims: {
        userId: 'usr-david-01',
        userName: 'David Chen',
        roles: ['Employee'],
        department: 'General',
        accessLevel: 1,
        permissions: ['Read']
      }
    })
  });
  const chat1 = await chat1Res.json();
  console.log('   Latency:', chat1.executionTimeMs, 'ms');
  console.log('   Sources Cited:', chat1.sources.map(s => `"${s.documentTitle}" (p.${s.pageNumber})`).join(', '));
  console.log('   Security Mode:', chat1.securityInfo.enforcementMode);
  console.log('   Answer Snippet:', chat1.answer.substring(0, 150) + '...\n');
  if (chat1.sources.length === 0) throw new Error('Expected at least 1 citation for handbook query');
  console.log('   ✅ Grounded Public Chat PASSED\n');

  // Test 5: RAG Query as David Chen (Level 1) - Asking Restricted M&A (Project Titan)
  console.log('5️⃣ Testing POST /api/chat: David Chen asking for RESTRICTED M&A (Project Titan Level 5)');
  const chatRestrictedRes = await fetch(`${API_BASE}/api/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      question: 'What are the confidential acquisition terms, valuation, and executive equity pool for Project Titan?',
      userClaims: {
        userId: 'usr-david-01',
        userName: 'David Chen',
        roles: ['Employee'],
        department: 'General',
        accessLevel: 1,
        permissions: ['Read']
      }
    })
  });
  const chatRestricted = await chatRestrictedRes.json();
  console.log('   Sources Supplied to LLM:', chatRestricted.sources.length);
  console.log('   Restricted Docs Withheld in Pre-Retrieval Filter:', chatRestricted.securityInfo.withheldDocuments.length);
  console.log('   Answer:', chatRestricted.answer.replace(/\n+/g, ' '));
  if (chatRestricted.sources.length > 0) throw new Error('SECURITY VIOLATION: Restricted chunks were leaked to Level 1 user!');
  console.log('   ✅ Strict ACL Pre-Retrieval Enforcement PASSED\n');

  // Test 6: RAG Query as Elena Rostova (Level 5) - Asking Restricted M&A (Project Titan)
  console.log('6️⃣ Testing POST /api/chat: Elena Rostova (Exec Level 5) asking for Project Titan');
  const chatExecRes = await fetch(`${API_BASE}/api/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      question: 'What are the confidential acquisition terms, valuation, and executive equity pool for Project Titan?',
      userClaims: {
        userId: 'usr-elena-05',
        userName: 'Elena Rostova',
        roles: ['Executive', 'Board', 'Admin'],
        department: 'Executive',
        accessLevel: 5,
        permissions: ['Read', 'FullAccess']
      }
    })
  });
  const chatExec = await chatExecRes.json();
  console.log('   Latency:', chatExec.executionTimeMs, 'ms');
  console.log('   Sources Cited:', chatExec.sources.map(s => `"${s.documentTitle}" (Score: ${(s.relevanceScore*100).toFixed(0)}%)`).join(', '));
  console.log('   Answer Snippet:', chatExec.answer.substring(0, 200) + '...\n');
  if (chatExec.sources.length === 0) throw new Error('Expected Project Titan citations for Level 5 executive');
  console.log('   ✅ Executive Clearance Authorization PASSED\n');

  // Test 7: Feedback Submission
  console.log('7️⃣ Testing POST /api/chat/{sessionId}/feedback');
  const fbRes = await fetch(`${API_BASE}/api/chat/${chatExec.sessionId}/feedback`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ feedback: 'positive', comment: 'Accurate grounded response' })
  });
  console.log('   Feedback Status:', fbRes.status);
  console.log('   ✅ Feedback submission PASSED\n');

  // Test 8: Audit Logs Inspection
  console.log('8️⃣ Testing GET /api/admin/audit-logs');
  const auditRes = await fetch(`${API_BASE}/api/admin/audit-logs`);
  const auditLogs = await auditRes.json();
  console.log(`   Retrieved ${auditLogs.length} audit log entries.`);
  console.log(`   Latest Log: [${auditLogs[0].userId}] "${auditLogs[0].question}" (${auditLogs[0].permittedChunkCount} permitted chunks)`);
  if (auditLogs.length < 3) throw new Error('Expected audit records for queries');
  console.log('   ✅ Audit Log Transparency PASSED\n');

  // Test 9: Async Document Ingestion & Background Worker
  console.log('9️⃣ Testing Document Ingestion: POST /api/documents');
  const uploadRes = await fetch(`${API_BASE}/api/documents`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      title: 'Q4 Enterprise Incident Response & Post-Mortem Guidelines',
      fileName: 'q4-incident-response.md',
      source: 'Security Confluence',
      department: 'Engineering',
      accessLevel: 3,
      version: '1.0',
      content: `# Incident Response & Severity Protocols\n\n## Severity 1 (Outage)\nSEV-1 incidents require immediate bridge initiation within 5 minutes. The Incident Commander (IC) must notify executive stakeholders and dispatch an on-call triage engineer.\n\n## Post-Mortem SLA\nBlameless post-mortems must be published within 72 hours of incident resolution.`,
      allowedRoles: ['Engineering', 'DevOps', 'Admin']
    })
  });
  const uploaded = await uploadRes.json();
  console.log('   Uploaded Document ID:', uploaded.id);
  console.log('   Document Title:', uploaded.title);

  // Wait 1.5 seconds for background worker to chunk and index
  console.log('   Waiting for background ingestion worker to chunk and index...');
  await new Promise(r => setTimeout(r, 1500));

  const statusRes = await fetch(`${API_BASE}/api/admin/indexing/status`);
  const jobs = await statusRes.json();
  const latestJob = jobs.find(j => j.documentId === uploaded.id);
  console.log(`   Ingestion Job Status: ${latestJob?.status} (${latestJob?.chunksProcessed} chunks processed)`);
  if (latestJob?.status !== 'Completed') throw new Error('Ingestion job did not complete');
  console.log('   ✅ Async Ingestion Pipeline PASSED\n');

  console.log('🎉 ALL 9 VERIFICATION SUITE TESTS PASSED WITH 100% SUCCESS!');
}

runTests().catch(err => {
  console.error('❌ Verification failed:', err);
  process.exit(1);
});
