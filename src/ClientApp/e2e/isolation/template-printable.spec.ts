import { expect } from '@playwright/test';
import { workerTest } from '../fixtures/auth.fixture';
import { apiOk, createFormTemplateDto } from '../fixtures/mock-data';

const templateId = '44444444-4444-4444-4444-444444444444';

function mockTemplate() {
  return createFormTemplateDto({
    id: templateId,
    name: 'Child Welfare Intake',
    description: 'Standard intake form for child welfare cases',
    type: 'ChildWelfare',
    fields: [
      { label: 'Child Name', fieldType: 'Text', isRequired: true, options: null },
      { label: 'Date of Birth', fieldType: 'Date', isRequired: true, options: null },
      { label: 'Case Type', fieldType: 'Select', isRequired: true, options: '["Neglect","Abuse","Abandonment"]' },
      { label: 'Urgent', fieldType: 'Checkbox', isRequired: false, options: null },
      { label: 'Notes', fieldType: 'TextArea', isRequired: false, options: null },
    ],
  });
}

async function setupRoute(page: import('@playwright/test').Page) {
  await page.route(`**/api/form-templates/${templateId}`, route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(apiOk(mockTemplate())),
    })
  );
}

workerTest.describe('Template printable form', () => {
  workerTest('switches between structure and form view', async ({ workerPage: page }) => {
    await setupRoute(page);
    await page.goto(`/templates/${templateId}`);

    // Structure view is default
    await expect(page.getByText('Field Structure')).toBeVisible();

    // Switch to form view
    await page.getByText('Printable Form').click();
    await expect(page.getByTestId('form-view')).toBeVisible();

    // Structure view should be hidden
    await expect(page.getByText('Field Structure')).not.toBeVisible();
  });

  workerTest('form view renders printable fields', async ({ workerPage: page }) => {
    await setupRoute(page);
    await page.goto(`/templates/${templateId}`);
    await page.getByText('Printable Form').click();

    await expect(page.getByTestId('printable-field-Child Name')).toBeVisible();
    await expect(page.getByTestId('printable-field-Date of Birth')).toBeVisible();
    await expect(page.getByTestId('printable-field-Case Type')).toBeVisible();
    await expect(page.getByTestId('printable-field-Urgent')).toBeVisible();
    await expect(page.getByTestId('printable-field-Notes')).toBeVisible();
  });

  workerTest('select field shows radio circles for options', async ({ workerPage: page }) => {
    await setupRoute(page);
    await page.goto(`/templates/${templateId}`);
    await page.getByText('Printable Form').click();

    const caseTypeField = page.getByTestId('printable-field-Case Type');
    await expect(caseTypeField).toContainText('Neglect');
    await expect(caseTypeField).toContainText('Abuse');
    await expect(caseTypeField).toContainText('Abandonment');
  });

  workerTest('required fields show asterisk', async ({ workerPage: page }) => {
    await setupRoute(page);
    await page.goto(`/templates/${templateId}`);
    await page.getByText('Printable Form').click();

    await expect(page.getByTestId('printable-field-Child Name')).toContainText('*');
    await expect(page.getByTestId('printable-field-Urgent')).not.toContainText('*');
  });
});
