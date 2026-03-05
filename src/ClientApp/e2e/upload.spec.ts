import { test, expect } from '@playwright/test';
import path from 'path';
import fs from 'fs';

test.describe('Document upload and list', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('email address').fill('worker@alpha.demo');
    await page.getByLabel('password').fill('Demo123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(/\/(dashboard|$)/);
  });

  test('upload page shows template dropdown and file input', async ({ page }) => {
    await page.goto('/upload');

    await expect(page.getByRole('heading', { name: 'Upload Document' })).toBeVisible();
    await expect(page.getByLabelText('Template (optional)')).toBeVisible();
    await expect(page.getByText('Drag and drop a file here')).toBeVisible();
  });

  test('upload a test file and verify success', async ({ page }) => {
    await page.goto('/upload');

    // Create a temp test file
    const testFilePath = path.join(__dirname, 'test-upload.txt');
    fs.writeFileSync(testFilePath, 'Test document content');

    const fileInput = page.locator('#file-input');
    await fileInput.setInputFiles(testFilePath);

    await expect(page.getByText('test-upload.txt')).toBeVisible();

    await page.getByRole('button', { name: 'Upload' }).click();

    await expect(page.getByText('Upload Successful')).toBeVisible({ timeout: 10000 });

    // Cleanup
    fs.unlinkSync(testFilePath);
  });

  test('documents page shows table with columns', async ({ page }) => {
    await page.goto('/documents');

    await expect(page.getByRole('heading', { name: 'Documents' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'File Name' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Status' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Submitted At' })).toBeVisible();
    await expect(page.getByRole('columnheader', { name: 'Processed At' })).toBeVisible();
  });

  test('navigation has upload and documents links', async ({ page }) => {
    await page.goto('/dashboard');

    await page.getByRole('button', { name: 'Upload' }).click();
    await page.waitForURL(/\/upload/);
    await expect(page.getByRole('heading', { name: 'Upload Document' })).toBeVisible();

    await page.getByRole('button', { name: 'Documents' }).click();
    await page.waitForURL(/\/documents/);
    await expect(page.getByRole('heading', { name: 'Documents' })).toBeVisible();
  });
});
