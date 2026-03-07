import { test, expect } from '@playwright/test';

test.describe('Template printable form E2E', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
    await page.getByLabel('email address').fill('admin@alpha.demo');
    await page.getByLabel('password').fill('Demo123!');
    await page.getByRole('button', { name: 'Sign In' }).click();
    await page.waitForURL(/\/(dashboard|$)/);
  });

  test('template detail has structure and printable form toggle', async ({ page }) => {
    await page.goto('/templates');
    await page.getByText('Child Welfare Intake').click();
    await page.waitForURL(/\/templates\/.+/);

    // Structure view is default
    await expect(page.getByText('Field Structure')).toBeVisible();
    await expect(page.getByTestId('view-structure-btn')).toBeVisible();
    await expect(page.getByTestId('view-form-btn')).toBeVisible();
  });

  test('switching to printable form view renders form fields', async ({ page }) => {
    await page.goto('/templates');
    await page.getByText('Child Welfare Intake').click();
    await page.waitForURL(/\/templates\/.+/);

    await page.getByTestId('view-form-btn').click();
    await expect(page.getByTestId('form-view')).toBeVisible();

    // The form view should show the template name as a heading
    await expect(page.getByTestId('form-view').getByText('Child Welfare Intake')).toBeVisible();
  });

  test('printable form renders field-type-specific controls', async ({ page }) => {
    await page.goto('/templates');
    await page.getByText('Child Welfare Intake').click();
    await page.waitForURL(/\/templates\/.+/);

    await page.getByTestId('view-form-btn').click();
    const formView = page.getByTestId('form-view');
    await expect(formView).toBeVisible();

    // Check that field labels are present
    await expect(formView.getByText('Child Full Name')).toBeVisible();
    await expect(formView.getByText('Date of Birth')).toBeVisible();
  });

  test('switching back to structure view hides form', async ({ page }) => {
    await page.goto('/templates');
    await page.getByText('Child Welfare Intake').click();
    await page.waitForURL(/\/templates\/.+/);

    // Switch to form view
    await page.getByTestId('view-form-btn').click();
    await expect(page.getByTestId('form-view')).toBeVisible();

    // Switch back to structure
    await page.getByTestId('view-structure-btn').click();
    await expect(page.getByText('Field Structure')).toBeVisible();
    await expect(page.getByTestId('form-view')).not.toBeVisible();
  });

  test('structure view screenshot matches baseline', async ({ page }) => {
    await page.goto('/templates');
    await page.getByText('Child Welfare Intake').click();
    await page.waitForURL(/\/templates\/.+/);

    await expect(page.getByText('Field Structure')).toBeVisible();

    await expect(page).toHaveScreenshot('template-structure-view.png', {
      fullPage: true,
      maxDiffPixelRatio: 0.05,
    });
  });

  test('printable form view screenshot matches baseline', async ({ page }) => {
    await page.goto('/templates');
    await page.getByText('Child Welfare Intake').click();
    await page.waitForURL(/\/templates\/.+/);

    await page.getByTestId('view-form-btn').click();
    await expect(page.getByTestId('form-view')).toBeVisible();

    await expect(page).toHaveScreenshot('template-printable-form.png', {
      fullPage: true,
      maxDiffPixelRatio: 0.05,
    });
  });
});
