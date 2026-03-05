import { test as base, type Page } from '@playwright/test';

export interface MockUser {
  id: string;
  email: string;
  roles: string[];
  tenantId: string;
}

const DEFAULT_WORKER: MockUser = {
  id: '00000000-0000-0000-0000-000000000010',
  email: 'worker@alpha.demo',
  roles: ['IntakeWorker'],
  tenantId: 'a1b2c3d4-0000-0000-0000-000000000001',
};

const DEFAULT_REVIEWER: MockUser = {
  id: '00000000-0000-0000-0000-000000000020',
  email: 'reviewer@alpha.demo',
  roles: ['Reviewer'],
  tenantId: 'a1b2c3d4-0000-0000-0000-000000000001',
};

const DEFAULT_ADMIN: MockUser = {
  id: '00000000-0000-0000-0000-000000000030',
  email: 'admin@alpha.demo',
  roles: ['Admin'],
  tenantId: 'a1b2c3d4-0000-0000-0000-000000000001',
};

async function injectAuth(page: Page, user: MockUser) {
  const authState = JSON.stringify({
    state: {
      user,
      token: 'mock-jwt-token',
      refreshToken: 'mock-refresh-token',
      isAuthenticated: true,
    },
    version: 0,
  });

  await page.addInitScript(
    ({ authJson, token }) => {
      localStorage.setItem('auth-storage', authJson);
      localStorage.setItem('auth_token', token);
    },
    { authJson: authState, token: 'mock-jwt-token' }
  );
}

export const workerTest = base.extend<{ workerPage: Page }>({
  workerPage: async ({ page }, use) => {
    await injectAuth(page, DEFAULT_WORKER);
    await use(page);
  },
});

export const reviewerTest = base.extend<{ reviewerPage: Page }>({
  reviewerPage: async ({ page }, use) => {
    await injectAuth(page, DEFAULT_REVIEWER);
    await use(page);
  },
});

export const adminTest = base.extend<{ adminPage: Page }>({
  adminPage: async ({ page }, use) => {
    await injectAuth(page, DEFAULT_ADMIN);
    await use(page);
  },
});

export const authTest = base.extend<{
  authenticatedPage: Page;
  mockUser: MockUser;
}>({
  mockUser: [DEFAULT_REVIEWER, { option: true }],
  authenticatedPage: async ({ page, mockUser }, use) => {
    await injectAuth(page, mockUser);
    await use(page);
  },
});

export { DEFAULT_WORKER, DEFAULT_REVIEWER, DEFAULT_ADMIN, injectAuth };
