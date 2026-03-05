import type { Page } from '@playwright/test';

export async function loginAsWorker(page: Page) {
  await page.goto('/login');
  await page.getByLabel('email address').fill('worker@alpha.demo');
  await page.getByLabel('password').fill('Demo123!');
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(/\/(dashboard|$)/);
}

export async function loginAsReviewer(page: Page) {
  await page.goto('/login');
  await page.getByLabel('email address').fill('reviewer@alpha.demo');
  await page.getByLabel('password').fill('Demo123!');
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(/\/(dashboard|$)/);
}

export async function loginAsAdmin(page: Page) {
  await page.goto('/login');
  await page.getByLabel('email address').fill('admin@alpha.demo');
  await page.getByLabel('password').fill('Demo123!');
  await page.getByRole('button', { name: 'Sign In' }).click();
  await page.waitForURL(/\/(dashboard|$)/);
}
