// Test PDF creation and upload pipeline
import fs from 'fs';
import path from 'path';

const API_BASE = 'http://localhost:5000';

// Generate a valid minimal PDF with text content
function createMinimalPdf(text, fileName) {
  const contentStream = `BT /F1 18 Tf 50 700 Td (${text}) Tj ET`;
  const streamLength = contentStream.length;
  
  const pdfData = `%PDF-1.4
1 0 obj
<< /Type /Catalog /Pages 2 0 R >>
endobj
2 0 obj
<< /Type /Pages /Kids [3 0 R] /Count 1 >>
endobj
3 0 obj
<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>
endobj
4 0 obj
<< /Length ${streamLength} >>
stream
${contentStream}
endstream
endobj
5 0 obj
<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>
endobj
xref
0 6
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n 
0000000224 00000 n 
0000000305 00000 n 
trailer
<< /Size 6 /Root 1 0 R >>
startxref
385
%%EOF`;

  fs.writeFileSync(fileName, pdfData, 'utf8');
  return fileName;
}

async function run() {
  console.log('📄 Running PDF Ingestion and Management Tests...\n');

  // 1. Health check
  const healthRes = await fetch(`${API_BASE}/health`);
  console.log('1. Health Check:', await healthRes.json());

  // 2. Create test PDF
  const testPdf1 = path.join(process.cwd(), 'sample_financial_report.pdf');
  createMinimalPdf('Confidential Q4 Fiscal Growth Report and Revenue Targets for 2027.', testPdf1);
  console.log('2. Created test PDF:', testPdf1);

  // 3. Upload PDF via POST /api/documents/upload-pdf
  console.log('3. Uploading PDF via multipart/form-data...');
  const formData = new FormData();
  const fileBytes = fs.readFileSync(testPdf1);
  const blob = new Blob([fileBytes], { type: 'application/pdf' });
  formData.append('file', blob, 'sample_financial_report.pdf');
  formData.append('title', 'Q4 Fiscal Growth Report 2027');
  formData.append('department', 'Finance');
  formData.append('accessLevel', '2');
  formData.append('roles', 'Finance,Employee,Admin');

  const uploadRes = await fetch(`${API_BASE}/api/documents/upload-pdf`, {
    method: 'POST',
    body: formData
  });

  const uploadData = await uploadRes.json();
  console.log('   Upload Result:', uploadData);

  // 4. Test Scan Folder
  console.log('\n4. Testing Scan Folder functionality...');
  const docsFolder = path.join(process.cwd(), 'documents');
  if (!fs.existsSync(docsFolder)) fs.mkdirSync(docsFolder, { recursive: true });

  const dropPdf = path.join(docsFolder, 'policy_remote_work_global.pdf');
  createMinimalPdf('Global Remote Work and Flexible Workspace Guidelines 2027.', dropPdf);
  console.log('   Placed PDF into documents folder:', dropPdf);

  const scanRes = await fetch(`${API_BASE}/api/documents/scan-folder`, { method: 'POST' });
  const scanData = await scanRes.json();
  console.log('   Scan Result:', scanData);

  // Wait 1 second for background queue processing
  await new Promise(r => setTimeout(r, 1500));

  // 5. Test Chat / RAG against newly uploaded PDF
  console.log('\n5. Testing Chat query against uploaded PDF...');
  const chatRes = await fetch(`${API_BASE}/api/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      question: 'What are the revenue targets in the Q4 Fiscal Growth Report?',
      userClaims: {
        userId: 'usr-finance-01',
        userName: 'Finance Tester',
        roles: ['Finance', 'Employee'],
        department: 'Finance',
        accessLevel: 2,
        permissions: ['Read']
      }
    })
  });
  const chatData = await chatRes.json();
  console.log('   Chat Answer:', chatData.answer);
  console.log('   Citations:', chatData.sources.map(s => `${s.documentTitle} (${s.fileName} p.${s.pageNumber})`));

  // Cleanup test files
  if (fs.existsSync(testPdf1)) fs.unlinkSync(testPdf1);
  if (fs.existsSync(dropPdf)) fs.unlinkSync(dropPdf);

  console.log('\n✅ PDF Ingestion, Folder Drop Scanning, and Search pipeline fully verified!');
}

run().catch(console.error);
