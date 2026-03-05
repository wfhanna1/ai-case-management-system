import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Minimal valid PDF content (1-page blank PDF)
const MINIMAL_PDF = Buffer.from(
  '%PDF-1.4\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n' +
  '2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n' +
  '3 0 obj<</Type/Page/MediaBox[0 0 612 792]/Parent 2 0 R>>endobj\n' +
  'xref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n' +
  '0000000115 00000 n \ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n190\n%%EOF'
);

function createTestPdf(name: string): string {
  const filePath = path.join(__dirname, name);
  fs.writeFileSync(filePath, MINIMAL_PDF);
  return filePath;
}

test.describe('Template view to document upload flow', () => {
  let testPdfPath: string;

  test.afterEach(() => {
    if (testPdfPath && fs.existsSync(testPdfPath)) {
      fs.unlinkSync(testPdfPath);
    }
  });

  test.describe('as IntakeWorker', () => {
    test.beforeEach(async ({ page }) => {
      await page.goto('/login');
      await page.getByLabel('email address').fill('worker@alpha.demo');
      await page.getByLabel('password').fill('Demo123!');
      await page.getByRole('button', { name: 'Sign In' }).click();
      await page.waitForURL(/\/(dashboard|$)/);
    });

    test('view template, then upload a PDF and verify it appears in documents list', async ({ page }) => {
      // Step 1: Navigate to templates and open a template
      await page.goto('/templates');
      await expect(page.getByRole('heading', { name: 'Form Templates' })).toBeVisible();
      await page.getByText('Child Welfare Intake').click();
      await page.waitForURL(/\/templates\/.+/);

      // Step 2: Verify template detail with print/PDF button
      await expect(page.getByRole('heading', { name: 'Child Welfare Intake' })).toBeVisible();
      await expect(page.getByText('Field Structure')).toBeVisible();
      const printButton = page.getByTestId('print-button');
      await expect(printButton).toBeVisible();
      await expect(printButton).toHaveText(/Print \/ PDF/);

      // Step 3: Navigate to upload page
      await page.getByRole('button', { name: 'Upload' }).click();
      await page.waitForURL(/\/upload/);
      await expect(page.getByRole('heading', { name: 'Upload Document' })).toBeVisible();

      // Step 4: Select the template in the upload form
      await page.getByLabel('Template (optional)').click();
      await page.getByRole('option', { name: 'Child Welfare Intake' }).click();

      // Step 5: Upload a PDF file
      testPdfPath = createTestPdf('child-welfare-form.pdf');
      const fileInput = page.locator('#file-input');
      await fileInput.setInputFiles(testPdfPath);
      await expect(page.getByText('child-welfare-form.pdf')).toBeVisible();

      // Step 6: Submit the upload
      await page.getByRole('main').getByRole('button', { name: 'Upload' }).click();
      await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 15000 });

      // Step 7: Navigate to documents and verify the uploaded file appears
      await page.getByRole('button', { name: 'View Documents' }).click();
      await page.waitForURL(/\/documents/);
      await expect(page.getByRole('heading', { name: 'Documents' })).toBeVisible();
      await expect(page.getByRole('cell', { name: 'child-welfare-form.pdf' }).first()).toBeVisible({ timeout: 10000 });
      await expect(page.getByText('Submitted').first()).toBeVisible();
    });

    test('upload a PDF without template and verify it appears in documents tab', async ({ page }) => {
      // Step 1: Go directly to upload page
      await page.goto('/upload');
      await expect(page.getByRole('heading', { name: 'Upload Document' })).toBeVisible();

      // Step 2: Upload a PDF file without selecting a template
      testPdfPath = createTestPdf('intake-scan.pdf');
      const fileInput = page.locator('#file-input');
      await fileInput.setInputFiles(testPdfPath);
      await expect(page.getByText('intake-scan.pdf')).toBeVisible();

      // Step 3: Submit
      await page.getByRole('main').getByRole('button', { name: 'Upload' }).click();
      await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 15000 });

      // Step 4: Click View Documents to navigate to documents tab
      await page.getByRole('button', { name: 'View Documents' }).click();
      await page.waitForURL(/\/documents/);

      // Step 5: Verify the uploaded document is visible in the table
      await expect(page.getByRole('cell', { name: 'intake-scan.pdf' }).first()).toBeVisible({ timeout: 10000 });
    });

    test('upload another file after first upload and verify both appear', async ({ page }) => {
      await page.goto('/upload');

      // Upload first file
      testPdfPath = createTestPdf('report-one.pdf');
      await page.locator('#file-input').setInputFiles(testPdfPath);
      await page.getByRole('main').getByRole('button', { name: 'Upload' }).click();
      await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 15000 });

      // Click Upload Another
      await page.getByRole('button', { name: 'Upload Another' }).click();
      await expect(page.getByRole('heading', { name: 'Upload Document' })).toBeVisible();

      // Upload second file
      const secondPdfPath = createTestPdf('report-two.pdf');
      await page.locator('#file-input').setInputFiles(secondPdfPath);
      await page.getByRole('main').getByRole('button', { name: 'Upload' }).click();
      await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 15000 });

      // Navigate to documents and verify both appear
      await page.getByRole('button', { name: 'View Documents' }).click();
      await page.waitForURL(/\/documents/);
      await expect(page.getByRole('cell', { name: 'report-one.pdf' }).first()).toBeVisible({ timeout: 10000 });
      await expect(page.getByRole('cell', { name: 'report-two.pdf' }).first()).toBeVisible({ timeout: 10000 });

      // Cleanup second file
      if (fs.existsSync(secondPdfPath)) fs.unlinkSync(secondPdfPath);
    });
  });
});
