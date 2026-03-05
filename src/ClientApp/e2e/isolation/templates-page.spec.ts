import { expect } from '@playwright/test';
import { adminTest } from '../fixtures/auth.fixture';
import { createFormTemplateDto } from '../fixtures/mock-data';
import { mockGetTemplates, mockApiError } from '../helpers/api-mocks';

adminTest.describe('Templates page (isolation)', () => {
  adminTest('renders templates list with mocked data', async ({ adminPage }) => {
    const templates = [
      createFormTemplateDto({ name: 'Child Welfare Intake', type: 'ChildWelfare' }),
      createFormTemplateDto({ name: 'Adult Protective Services', type: 'AdultProtective' }),
    ];
    await mockGetTemplates(adminPage, templates);
    await adminPage.goto('/templates');

    await expect(adminPage.getByText('Child Welfare Intake')).toBeVisible();
    await expect(adminPage.getByText('Adult Protective Services')).toBeVisible();
  });

  adminTest('shows template heading', async ({ adminPage }) => {
    await mockGetTemplates(adminPage, []);
    await adminPage.goto('/templates');

    await expect(adminPage.getByRole('heading', { name: /template/i })).toBeVisible();
  });

  adminTest('handles empty templates list', async ({ adminPage }) => {
    await mockGetTemplates(adminPage, []);
    await adminPage.goto('/templates');

    await expect(adminPage.getByRole('heading', { name: /template/i })).toBeVisible();
  });

  adminTest('handles API error', async ({ adminPage }) => {
    await mockApiError(adminPage, '**/api/form-templates', 'SERVER_ERROR', 'Failed to load templates');
    await adminPage.goto('/templates');

    await expect(adminPage.getByRole('alert')).toBeVisible();
  });
});
