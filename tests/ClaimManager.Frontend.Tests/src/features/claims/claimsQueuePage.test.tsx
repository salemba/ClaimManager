import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppProviders } from '../../../../../src/ClaimManager.Frontend/src/app/providers/AppProviders';
import { ClaimsQueuePage } from '../../../../../src/ClaimManager.Frontend/src/features/claims/routes/ClaimsQueuePage';
import type { ClaimsPage, ClaimSummary } from '../../../../../src/ClaimManager.Frontend/src/features/claims/types/Claim';
import * as claimsApiModule from '../../../../../src/ClaimManager.Frontend/src/features/claims/api/claimsApi';

vi.mock('../../../../../src/ClaimManager.Frontend/src/features/claims/api/claimsApi');

const mockClaim: ClaimSummary = {
  id: 'claim-1',
  claimNumber: 'CLM-0001',
  status: 'open',
  claimantName: 'Jordan Avery',
  policyNumber: 'POL-2026-0001',
  lossDateUtc: '2026-05-08T00:00:00Z',
  createdAtUtc: '2026-05-11T00:00:00Z',
  updatedAtUtc: null,
  blockerType: null,
  blockerReason: null,
  ownedByUserId: 'adjuster-1',
  hasDataIntegrityWarning: false,
  policySyncedAtUtc: null,
  paymentSyncedAtUtc: null,
  documentSyncedAtUtc: null,
};

function makePage(items: ClaimSummary[], overrides?: Partial<ClaimsPage<ClaimSummary>>): ClaimsPage<ClaimSummary> {
  return { items, page: 1, pageSize: 20, totalCount: items.length, ...overrides };
}

function LocationProbe() {
  const location = useLocation();

  return <div data-testid="location-search">{location.search}</div>;
}

function EditLocationProbe() {
  const location = useLocation();
  const dashboardOrigin = (location.state as { dashboardOrigin?: { label: string; backTo?: string } } | null)?.dashboardOrigin;

  return (
    <>
      <div data-testid="edit-location-pathname">{location.pathname}</div>
      <div data-testid="edit-origin-label">{dashboardOrigin?.label ?? ''}</div>
      <div data-testid="edit-origin-back-to">{dashboardOrigin?.backTo ?? ''}</div>
    </>
  );
}

function renderPage(initialEntry: string | { pathname: string; search?: string; state?: unknown } = '/claims') {
  return render(
    <AppProviders>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route
            path="/claims"
            element={(
              <>
                <ClaimsQueuePage />
                <LocationProbe />
              </>
            )}
          />
        </Routes>
      </MemoryRouter>
    </AppProviders>,
  );
}

function renderQueueAndClaimRoutes(initialEntry: string | { pathname: string; search?: string; state?: unknown }) {
  return render(
    <AppProviders>
      <MemoryRouter initialEntries={[initialEntry]}>
        <Routes>
          <Route path="/claims" element={<ClaimsQueuePage />} />
          <Route path="/claims/:claimId/edit" element={<EditLocationProbe />} />
        </Routes>
      </MemoryRouter>
    </AppProviders>,
  );
}

beforeEach(() => {
  vi.useRealTimers();
});

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe('ClaimsQueuePage', () => {
  it('renders a list item for each claim returned by the API', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([mockClaim]));

    renderPage();

    expect(await screen.findByText('CLM-0001')).toBeInTheDocument();
    expect(screen.getByText('Jordan Avery')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Open claim CLM-0001/i })).toBeInTheDocument();
  });

  it('displays "No claims match the current filters." when totalCount is 0 and a filter is active', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([]));

    renderPage('/claims?status=open');

    expect(await screen.findByText(/No claims match the current filters/i)).toBeInTheDocument();
  });

  it('displays "No claims have been filed yet." when totalCount is 0 and no filter is active', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([]));

    renderPage('/claims');

    expect(await screen.findByText(/No claims have been filed yet/i)).toBeInTheDocument();
  });

  it('calls getClaims with status param when status filter is set in the URL', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([mockClaim]));

    renderPage('/claims?status=open');

    await screen.findByText('CLM-0001');

    expect(vi.mocked(claimsApiModule.getClaims)).toHaveBeenCalledWith(
      expect.objectContaining({ status: 'open' }),
    );
  });

  it('updates the URL and refetches when the status filter changes', async () => {
    const user = userEvent.setup();
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([mockClaim]));

    renderPage();

    await screen.findByText('CLM-0001');

    fireEvent.mouseDown(screen.getByRole('combobox', { name: 'Status' }));
    await user.click(await screen.findByRole('option', { name: 'open' }));

    await waitFor(() => {
      expect(vi.mocked(claimsApiModule.getClaims)).toHaveBeenLastCalledWith(
        expect.objectContaining({ status: 'open', page: 1 }),
      );
    });

    expect(screen.getByTestId('location-search')).toHaveTextContent('?status=open');
  });

  it('calls getClaims with hasBlocker=true when hasBlocker filter is set in the URL', async () => {
    const blockedClaim: ClaimSummary = { ...mockClaim, blockerType: 'awaiting-payment-approval' };
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([blockedClaim]));

    renderPage('/claims?hasBlocker=true');

    await screen.findByText('CLM-0001');

    expect(vi.mocked(claimsApiModule.getClaims)).toHaveBeenCalledWith(
      expect.objectContaining({ hasBlocker: true }),
    );
  });

  it('updates the URL and refetches when the blocker checkbox is toggled', async () => {
    const user = userEvent.setup();
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([{ ...mockClaim, blockerType: 'awaiting-payment-approval' }]));

    renderPage();

    await screen.findByText('CLM-0001');
    await user.click(screen.getByRole('checkbox', { name: 'Has blocker' }));

    await waitFor(() => {
      expect(vi.mocked(claimsApiModule.getClaims)).toHaveBeenLastCalledWith(
        expect.objectContaining({ hasBlocker: true, page: 1 }),
      );
    });

    expect(screen.getByTestId('location-search')).toHaveTextContent('?hasBlocker=true');
  });

  it('"Clear filters" button is visible when a filter is active', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([mockClaim]));

    renderPage('/claims?status=open');

    await screen.findByText('CLM-0001');

    expect(screen.getByRole('button', { name: /Clear all filters/i })).toBeInTheDocument();
  });

  it('"Clear filters" button is not shown when no filter is active', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([mockClaim]));

    renderPage('/claims');

    await screen.findByText('CLM-0001');

    expect(screen.queryByRole('button', { name: /Clear all filters/i })).not.toBeInTheDocument();
  });

  it('"Next" pagination button is disabled when on the last page', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(
      makePage([mockClaim], { page: 2, pageSize: 20, totalCount: 25 }),
    );

    renderPage('/claims?page=2');

    await screen.findByText('CLM-0001');

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /Next page/i })).toBeDisabled();
    });
  });

  it('preserves deep-linked search and page state on first render', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(
      makePage([mockClaim], { page: 2, pageSize: 20, totalCount: 40 }),
    );

    renderPage('/claims?search=Jordan&page=2');

    await screen.findByText('CLM-0001');
    await waitFor(
      () => {
        expect(screen.getByDisplayValue('Jordan')).toBeInTheDocument();
        expect(screen.getByTestId('location-search')).toHaveTextContent('?search=Jordan&page=2');
        expect(screen.getByText('Page 2 of 2')).toBeInTheDocument();
      },
      { timeout: 1000 },
    );
  });

  it('clears filters back to the base queue URL', async () => {
    const user = userEvent.setup();
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([mockClaim]));

    renderPage('/claims?search=Jordan&status=open&hasBlocker=true&page=2');

    await screen.findByText('CLM-0001');
    await user.click(screen.getByRole('button', { name: /Clear all filters/i }));

    await waitFor(() => {
      expect(vi.mocked(claimsApiModule.getClaims)).toHaveBeenLastCalledWith(
        expect.objectContaining({
          search: undefined,
          status: undefined,
          hasBlocker: undefined,
          page: 1,
        }),
      );
    });

    expect(screen.getByTestId('location-search')).toHaveTextContent('');
  });

  it('shows dashboard origin banner when opened from a blocker drill-down', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([mockClaim]));

    renderPage({
      pathname: '/claims',
      state: { dashboardOrigin: { label: 'awaiting-payment-approval', backTo: '/?panel=queue-blockers' } },
    });

    expect(await screen.findByText('Opened from Supervisor dashboard: awaiting-payment-approval')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Back to dashboard' })).toHaveAttribute('href', '/?panel=queue-blockers');
  });

  it('does not show dashboard origin banner when navigated to directly', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(makePage([mockClaim]));

    renderPage('/claims');

    await screen.findByText('CLM-0001');
    expect(screen.queryByText(/Opened from Supervisor dashboard/)).not.toBeInTheDocument();
  });

  it('forwards dashboard origin and filtered queue path when opening a claim from a blocker drill-down', async () => {
    const user = userEvent.setup();

    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(
      makePage([{ ...mockClaim, blockerType: 'awaiting-payment-approval' }]),
    );

    renderQueueAndClaimRoutes({
      pathname: '/claims',
      search: '?blockerType=awaiting-payment-approval&hasBlocker=true',
      state: { dashboardOrigin: { label: 'awaiting-payment-approval', backTo: '/?panel=queue-blockers' } },
    });

    await user.click(await screen.findByRole('button', { name: /Open claim CLM-0001/i }));

    expect(await screen.findByTestId('edit-location-pathname')).toHaveTextContent('/claims/claim-1/edit');
    expect(screen.getByTestId('edit-origin-label')).toHaveTextContent('awaiting-payment-approval');
    expect(screen.getByTestId('edit-origin-back-to')).toHaveTextContent('/claims?blockerType=awaiting-payment-approval&hasBlocker=true');
  });

  it('renders page summary from API page metadata when the URL page is invalid', async () => {
    vi.mocked(claimsApiModule.getClaims).mockResolvedValue(
      makePage([mockClaim], { page: 1, pageSize: 20, totalCount: 25 }),
    );

    renderPage('/claims?page=abc');

    await screen.findByText('CLM-0001');

    expect(screen.getByText('Showing 1–20 of 25 claims')).toBeInTheDocument();
    expect(screen.getByText('Page 1 of 2')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Previous page/i })).toBeDisabled();
  });
});
