import { expect } from '@playwright/test';
import { workerTest } from '../fixtures/auth.fixture';
import { mockGetCases } from '../helpers/api-mocks';
import {
  createCaseDto,
  createSearchCasesResultDto,
} from '../fixtures/mock-data';

workerTest.describe('CasesPage', () => {
  workerTest('renders cases list', async ({ workerPage: page }) => {
    const cases = [
      createCaseDto({ subjectName: 'John Doe', documentCount: 3 }),
      createCaseDto({ subjectName: 'Jane Smith', documentCount: 1 }),
    ];
    const result = createSearchCasesResultDto(cases, 2);
    await mockGetCases(page, result);

    await page.goto('/cases');

    await expect(page.getByRole('heading', { name: 'Cases' })).toBeVisible();
    await expect(page.getByText('John Doe')).toBeVisible();
    await expect(page.getByText('Jane Smith')).toBeVisible();
  });

  workerTest('shows empty state', async ({ workerPage: page }) => {
    const result = createSearchCasesResultDto([], 0);
    await mockGetCases(page, result);

    await page.goto('/cases');

    await expect(page.getByTestId('no-cases')).toBeVisible();
    await expect(page.getByTestId('no-cases')).toHaveText('No cases found');
  });

  workerTest('shows error alert on API failure', async ({ workerPage: page }) => {
    await page.route(/\/api\/cases(\?.*)?$/, route =>
      route.fulfill({
        status: 500,
        contentType: 'application/json',
        body: JSON.stringify({ data: null, error: { code: 'SERVER_ERROR', message: 'Internal error' } }),
      })
    );

    await page.goto('/cases');

    await expect(page.getByTestId('cases-error')).toBeVisible();
  });

  workerTest('navigates to case detail on row click', async ({ workerPage: page }) => {
    const caseItem = createCaseDto({ subjectName: 'John Doe' });
    const result = createSearchCasesResultDto([caseItem], 1);
    await mockGetCases(page, result);

    // Mock the case detail endpoint too
    await page.route(`**/api/cases/${caseItem.id}`, route =>
      route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          data: { ...caseItem, documents: [] },
          error: null,
        }),
      })
    );

    await page.goto('/cases');
    await page.getByText('John Doe').click();

    await expect(page).toHaveURL(new RegExp(`/cases/${caseItem.id}`));
  });
});
