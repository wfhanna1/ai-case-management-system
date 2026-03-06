import { test, expect } from '@playwright/test';

test.describe('Registration flow E2E', () => {
  const uniqueEmail = `reg-e2e-${Date.now()}@test.com`;
  const password = 'TestPassword123!';

  test('fill register form, submit, and land on dashboard', async ({ page }) => {
    // Step 1: Navigate to registration page
    await page.goto('/register');
    await expect(page.getByRole('heading', { name: 'Create Account' })).toBeVisible();

    // Step 2: Fill out the registration form
    await page.getByLabel('Email').fill(uniqueEmail);
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByLabel('Confirm Password').fill(password);

    // Step 3: Submit the form
    await page.getByRole('button', { name: 'Register' }).click();

    // Step 4: Verify redirect to dashboard
    await expect(page).toHaveURL('/dashboard', { timeout: 10000 });

    // Step 5: Verify user email is displayed
    await expect(page.getByText(uniqueEmail)).toBeVisible();
  });

  test('registration with duplicate email shows error', async ({ page }) => {
    // Register the user first
    await page.goto('/register');
    await page.getByLabel('Email').fill(`dup-${Date.now()}@test.com`);
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByLabel('Confirm Password').fill(password);
    await page.getByRole('button', { name: 'Register' }).click();
    await expect(page).toHaveURL('/dashboard', { timeout: 10000 });

    // Sign out
    await page.getByRole('button', { name: 'Sign Out' }).click();
    await expect(page).toHaveURL('/login');

    // Try to register with the same email again -- use original uniqueEmail from first test
    // (we need a fresh duplicate)
    const dupEmail = `dup2-${Date.now()}@test.com`;
    await page.goto('/register');
    await page.getByLabel('Email').fill(dupEmail);
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByLabel('Confirm Password').fill(password);
    await page.getByRole('button', { name: 'Register' }).click();
    await expect(page).toHaveURL('/dashboard', { timeout: 10000 });

    // Sign out and try again with the same email
    await page.getByRole('button', { name: 'Sign Out' }).click();
    await page.goto('/register');
    await page.getByLabel('Email').fill(dupEmail);
    await page.getByLabel('Password', { exact: true }).fill(password);
    await page.getByLabel('Confirm Password').fill(password);
    await page.getByRole('button', { name: 'Register' }).click();

    // Verify error alert is shown
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 10000 });
  });
});
