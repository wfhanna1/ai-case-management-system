import { expect } from '@playwright/test';
import { workerTest } from '../fixtures/auth.fixture';
import { createDocumentDto, createFormTemplateDto } from '../fixtures/mock-data';
import { mockGetTemplates, mockUploadDocument, mockUploadDocumentError } from '../helpers/api-mocks';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function createTestPdf(): string {
  const filePath = path.join(__dirname, 'test-upload.pdf');
  fs.writeFileSync(filePath, Buffer.from(
    '%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n' +
    '2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n' +
    '3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\n' +
    'xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n' +
    '0000000115 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n190\n%%EOF'
  ));
  return filePath;
}

workerTest.describe('Upload page (isolation)', () => {
  workerTest('renders upload page with template dropdown', async ({ workerPage }) => {
    const templates = [
      createFormTemplateDto({ name: 'Child Welfare Intake' }),
      createFormTemplateDto({ name: 'Adult Protective Services' }),
    ];
    await mockGetTemplates(workerPage, templates);
    await workerPage.goto('/upload');

    await expect(workerPage.getByRole('heading', { name: 'Upload Document' })).toBeVisible();
    await expect(workerPage.getByLabel('Template (optional)')).toBeVisible();
    await expect(workerPage.getByText('Drag and drop a file here')).toBeVisible();
  });

  workerTest('file input accepts files', async ({ workerPage }) => {
    await mockGetTemplates(workerPage, []);
    await workerPage.goto('/upload');

    const testFile = createTestPdf();
    try {
      const fileInput = workerPage.locator('#file-input');
      await fileInput.setInputFiles(testFile);
      await expect(workerPage.getByText('test-upload.pdf')).toBeVisible();
    } finally {
      fs.unlinkSync(testFile);
    }
  });

  workerTest('shows success message after upload', async ({ workerPage }) => {
    await mockGetTemplates(workerPage, []);
    const responseDoc = createDocumentDto({ originalFileName: 'test-upload.pdf', status: 'Submitted' });
    await mockUploadDocument(workerPage, responseDoc);
    await workerPage.goto('/upload');

    const testFile = createTestPdf();
    try {
      const fileInput = workerPage.locator('#file-input');
      await fileInput.setInputFiles(testFile);
      await workerPage.getByRole('main').getByRole('button', { name: 'Upload' }).click();
      await expect(workerPage.getByText('Upload Successful')).toBeVisible({ timeout: 5000 });
    } finally {
      fs.unlinkSync(testFile);
    }
  });

  workerTest('shows error message on upload failure', async ({ workerPage }) => {
    await mockGetTemplates(workerPage, []);
    await mockUploadDocumentError(workerPage, 'UPLOAD_FAILED', 'File too large');
    await workerPage.goto('/upload');

    const testFile = createTestPdf();
    try {
      const fileInput = workerPage.locator('#file-input');
      await fileInput.setInputFiles(testFile);
      await workerPage.getByRole('main').getByRole('button', { name: 'Upload' }).click();
      await expect(workerPage.getByRole('alert').first()).toBeVisible({ timeout: 5000 });
    } finally {
      fs.unlinkSync(testFile);
    }
  });
});
