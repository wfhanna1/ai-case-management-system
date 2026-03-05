import { test, expect } from '@playwright/test';

test.describe('Template management', () => {
  test.beforeEach(async ({ page }) => {
    // Login as admin to access templates
    await page.goto('/login');
    await page.getByLabel('email address').fill('admin@alpha.demo');
    await page.getByLabel('password').fill('Demo123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(/\/(dashboard|$)/);
  });

  test('templates page shows list of templates', async ({ page }) => {
    await page.goto('/templates');

    await expect(page.getByRole('heading', { name: 'Form Templates' })).toBeVisible();
    // Use .first() because template names may appear in description cells too
    await expect(page.getByRole('cell', { name: 'Child Welfare Intake' }).first()).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Adult Protective Services' }).first()).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Housing Assistance Application' }).first()).toBeVisible();
    await expect(page.getByRole('cell', { name: 'Mental Health Referral' }).first()).toBeVisible();
  });

  test('clicking template row navigates to detail page', async ({ page }) => {
    await page.goto('/templates');

    await page.getByText('Child Welfare Intake').click();
    await page.waitForURL(/\/templates\/.+/);

    await expect(page.getByRole('heading', { name: 'Child Welfare Intake' })).toBeVisible();
    await expect(page.getByText('Field Structure')).toBeVisible();
    await expect(page.getByText('Child Full Name')).toBeVisible();
  });

  test('template detail shows field structure', async ({ page }) => {
    await page.goto('/templates');
    await page.getByText('Child Welfare Intake').click();
    await page.waitForURL(/\/templates\/.+/);

    // Verify field types and required indicators
    await expect(page.getByText('Date of Birth')).toBeVisible();
    await expect(page.getByText('Text Input').first()).toBeVisible();
    await expect(page.getByText('Required').first()).toBeVisible();
  });

  test('template detail has print button', async ({ page }) => {
    await page.goto('/templates');
    await page.getByText('Child Welfare Intake').click();
    await page.waitForURL(/\/templates\/.+/);

    await expect(page.getByTestId('print-button')).toBeVisible();
  });

  test('back button returns to templates list', async ({ page }) => {
    await page.goto('/templates');
    await page.getByText('Child Welfare Intake').click();
    await page.waitForURL(/\/templates\/.+/);

    await page.getByRole('button', { name: 'Back to Templates' }).click();
    await page.waitForURL(/\/templates$/);

    await expect(page.getByRole('heading', { name: 'Form Templates' })).toBeVisible();
  });

  test('navigation bar has templates link', async ({ page }) => {
    await page.goto('/dashboard');

    await page.getByRole('button', { name: 'Templates' }).click();
    await page.waitForURL(/\/templates/);

    await expect(page.getByRole('heading', { name: 'Form Templates' })).toBeVisible();
  });
});
