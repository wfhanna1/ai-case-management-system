import { defineConfig } from '@playwright/test';

export default defineConfig({
  timeout: 30000,
  retries: 0,
  use: {
    baseURL: 'http://localhost:3000',
    headless: true,
  },
  webServer: {
    command: 'npm run dev',
    url: 'http://localhost:3000',
    reuseExistingServer: true,
    timeout: 30000,
  },
  projects: [
    {
      name: 'isolation',
      testDir: './e2e/isolation',
    },
    {
      name: 'e2e',
      testDir: './e2e',
      testIgnore: ['**/isolation/**', '**/pipeline/**'],
    },
  ],
});
