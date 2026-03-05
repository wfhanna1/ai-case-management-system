import { test, expect } from '@playwright/test';

test.describe('Login page validation', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/login');
  });

  test('blur empty email field shows inline error', async ({ page }) => {
    const emailField = page.getByLabel('email address');
    await emailField.focus();
    await emailField.blur();

    await expect(page.getByText('Email is required.')).toBeVisible();
  });

  test('blur empty password field shows inline error', async ({ page }) => {
    const passwordField = page.getByLabel('password');
    await passwordField.focus();
    await passwordField.blur();

    await expect(page.getByText('Password is required.')).toBeVisible();
  });

  test('submit with invalid credentials shows server error alert', async ({ page }) => {
    await page.getByLabel('email address').fill('nobody@example.com');
    await page.getByLabel('password').fill('WrongPassword1');
    await page.getByRole('button', { name: 'Sign In' }).click();

    await expect(page.getByRole('alert')).toBeVisible();
  });
});

test.describe('Register page validation', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/register');
  });

  test('invalid email format shows inline error', async ({ page }) => {
    const emailField = page.getByLabel('Email');
    await emailField.fill('not-an-email');
    await emailField.blur();

    await expect(page.getByText('Enter a valid email address.')).toBeVisible();
  });

  test('weak password shows inline error', async ({ page }) => {
    const passwordField = page.getByLabel('Password', { exact: true });
    await passwordField.fill('short');

    await expect(page.getByText('Password must be at least 8 characters.')).toBeVisible();
  });

  test('mismatched confirm password shows inline error', async ({ page }) => {
    await page.getByLabel('Password', { exact: true }).fill('ValidPass1');
    const confirmField = page.getByLabel('Confirm Password');
    await confirmField.fill('DifferentPass1');
    await confirmField.blur();

    await expect(page.getByText('Passwords do not match.')).toBeVisible();
  });
});
