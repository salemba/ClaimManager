import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import { CssBaseline, ThemeProvider } from '@mui/material';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { SupervisorDashboard } from '../../../../../src/ClaimManager.Frontend/src/features/dashboard/components/SupervisorDashboard';
import type { SupervisorDashboard as SupervisorDashboardData } from '../../../../../src/ClaimManager.Frontend/src/features/dashboard/api/dashboardApi';
import { appTheme } from '../../../../../src/ClaimManager.Frontend/src/shared/ui/theme';
import { ApiError } from '../../../../../src/ClaimManager.Frontend/src/shared/api/client';

vi.mock('../../../../../src/ClaimManager.Frontend/src/features/dashboard/api/dashboardApi', () => ({
  getSupervisorDashboard: vi.fn(),
}));

vi.mock('../../../../../src/ClaimManager.Frontend/src/features/auth/api/authApi', () => ({
  getIntegrationHealth: vi.fn().mockResolvedValue({ entries: [], reportedAtUtc: new Date().toISOString() }),
}));

import { getSupervisorDashboard } from '../../../../../src/ClaimManager.Frontend/src/features/dashboard/api/dashboardApi';
const mockedGet = vi.mocked(getSupervisorDashboard);

const emptyDashboard: SupervisorDashboardData = {
  signals: { stuckCount: 0, agingCount: 0, attentionRequiredCount: 0, approvalPressureCount: 0 },
  blockerSummary: [],
  highRiskClaims: [],
  agingClaims: [],
  workloadDistribution: [],
  generatedAtUtc: '2026-05-15T10:00:00Z',
};

const populatedDashboard: SupervisorDashboardData = {
  signals: { stuckCount: 3, agingCount: 2, attentionRequiredCount: 1, approvalPressureCount: 1 },
  blockerSummary: [
    { blockerType: 'awaiting-payment-approval', count: 1, affectedOwnerCount: 1, agingClaimCount: 1 },
    { blockerType: 'Pending documentation', count: 2, affectedOwnerCount: 2, agingClaimCount: 0 },
  ],
  highRiskClaims: [
    {
      id: 'claim-1',
      claimNumber: 'CLM-0001',
      status: 'blocked',
      claimantName: 'Jordan Avery',
      blockerType: 'awaiting-payment-approval',
      ownedByUserId: 'adjuster-1',
      daysSinceCreated: 20,
      hasDataIntegrityWarning: true,
    },
  ],
  agingClaims: [
    {
      id: 'claim-2',
      claimNumber: 'CLM-0002',
      status: 'open',
      claimantName: 'Alex Morgan',
      blockerType: null,
      ownedByUserId: null,
      daysSinceCreated: 16,
      hasDataIntegrityWarning: false,
    },
  ],
  workloadDistribution: [
    { ownerId: 'adjuster-1', totalCount: 2, stuckCount: 1, agingCount: 1, blockerCount: 1 },
    { ownerId: 'adjuster-2', totalCount: 1, stuckCount: 0, agingCount: 0, blockerCount: 0 },
  ],
  generatedAtUtc: '2026-05-15T10:00:00Z',
};

function renderDashboard(options?: { seedData?: SupervisorDashboardData }) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: 0,
      },
    },
  });

  if (options?.seedData) {
    queryClient.setQueryData(['dashboard', 'supervisor'], options.seedData);
  }

  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={appTheme}>
        <CssBaseline />
        <MemoryRouter>
          <SupervisorDashboard />
        </MemoryRouter>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

function DestinationProbe() {
  const location = useLocation();
  const dashboardOrigin = (location.state as { dashboardOrigin?: { label: string; backTo?: string } } | null)?.dashboardOrigin;

  return (
    <>
      <div data-testid="destination-pathname">{location.pathname}</div>
      <div data-testid="destination-search">{location.search}</div>
      <div data-testid="destination-origin-label">{dashboardOrigin?.label ?? ''}</div>
      <div data-testid="destination-origin-back-to">{dashboardOrigin?.backTo ?? ''}</div>
    </>
  );
}

function renderDashboardRoutes(options?: { seedData?: SupervisorDashboardData }) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: 0,
      },
    },
  });

  if (options?.seedData) {
    queryClient.setQueryData(['dashboard', 'supervisor'], options.seedData);
  }

  return render(
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={appTheme}>
        <CssBaseline />
        <MemoryRouter initialEntries={['/']}>
          <Routes>
            <Route path="/" element={<SupervisorDashboard />} />
            <Route path="/claims" element={<DestinationProbe />} />
            <Route path="/claims/:claimId/edit" element={<DestinationProbe />} />
          </Routes>
        </MemoryRouter>
      </ThemeProvider>
    </QueryClientProvider>,
  );
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe('SupervisorDashboard', () => {
  it('shows loading state while fetching', () => {
    mockedGet.mockReturnValue(new Promise(() => {}));

    renderDashboard();

    expect(screen.getByText('Loading risk signals…')).toBeInTheDocument();
  });

  it('renders supervisor dashboard heading', async () => {
    renderDashboard({ seedData: emptyDashboard });

    expect(await screen.findByRole('heading', { name: 'Supervisor dashboard' })).toBeInTheDocument();
  });

  it('shows empty-state message when no high-risk claims exist', async () => {
    renderDashboard({ seedData: emptyDashboard });

    expect(
      await screen.findByText('No claims currently require immediate supervisor attention.'),
    ).toBeInTheDocument();
  });

  it('renders all four risk signal counts when populated', async () => {
    renderDashboard({ seedData: populatedDashboard });

    expect(await screen.findByLabelText('Stuck claims: 3')).toBeInTheDocument();
    expect(screen.getByLabelText('Aging claims: 2')).toBeInTheDocument();
    expect(screen.getByLabelText('Attention required: 1')).toBeInTheDocument();
    expect(screen.getByLabelText('Approval pressure: 1')).toBeInTheDocument();
  });

  it('renders high-risk claim preview rows when populated', async () => {
    renderDashboard({ seedData: populatedDashboard });

    const highRiskSection = await screen.findByRole('region', { name: 'High-risk claims' });

    expect(within(highRiskSection).getByText('CLM-0001')).toBeInTheDocument();
    expect(within(highRiskSection).getByText('Jordan Avery')).toBeInTheDocument();
    expect(within(highRiskSection).queryByText('CLM-0002')).not.toBeInTheDocument();
    expect(within(highRiskSection).queryByText('Alex Morgan')).not.toBeInTheDocument();
  });

  it('renders aging claim preview rows when populated', async () => {
    renderDashboard({ seedData: populatedDashboard });

    expect(await screen.findByRole('heading', { name: 'Aging claims' })).toBeInTheDocument();
    expect(screen.getByText('CLM-0002')).toBeInTheDocument();
    expect(screen.getByText('Alex Morgan')).toBeInTheDocument();
  });

  it('shows blocker detail in claim preview row', async () => {
    renderDashboard({ seedData: populatedDashboard });

    expect(await screen.findByText('Blocker: awaiting-payment-approval')).toBeInTheDocument();
  });

  it('shows data integrity warning indicator in claim preview row', async () => {
    renderDashboard({ seedData: populatedDashboard });

    expect(await screen.findByText('Data integrity issue')).toBeInTheDocument();
  });

  it('renders workload distribution section when multiple owners present', async () => {
    renderDashboard({ seedData: populatedDashboard });

    expect(await screen.findByRole('region', { name: 'Workload distribution' })).toBeInTheDocument();
    expect(screen.getByText('adjuster-1')).toBeInTheDocument();
    expect(screen.getByText('adjuster-2')).toBeInTheDocument();
  });

  it('hides workload distribution section when one or zero owners present', async () => {
    const singleOwnerDashboard = {
      ...populatedDashboard,
      workloadDistribution: [{ ownerId: 'adjuster-1', totalCount: 1, stuckCount: 0, agingCount: 0, blockerCount: 0 }],
    };
    renderDashboard({ seedData: singleOwnerDashboard });

    await screen.findByRole('heading', { name: 'Supervisor dashboard' });
    expect(screen.queryByRole('region', { name: 'Workload distribution' })).not.toBeInTheDocument();
  });

  it('shows adjusters text in blocker chip tooltip when affectedOwnerCount > 1', async () => {
    renderDashboard({ seedData: populatedDashboard });

    // The Tooltip title is set on the element, so we can find it by title
    expect(await screen.findByLabelText('Pending documentation (2) — 2 adjusters')).toBeInTheDocument();
  });

  it('shows aging claim warning in blocker chip when agingClaimCount > 0', async () => {
    renderDashboard({ seedData: populatedDashboard });

    expect(await screen.findByText('includes aging')).toBeInTheDocument();
  });

  it('shows access-denied message for non-supervisor users (403)', async () => {
    mockedGet.mockRejectedValue(new ApiError('Forbidden', 403));

    renderDashboard();

    expect(
      await screen.findByText('Supervisor or admin access is required to view operational risk signals.'),
    ).toBeInTheDocument();
  });

  it('shows error alert for unexpected failures', async () => {
    mockedGet.mockRejectedValue(new ApiError('Service unavailable', 503));

    renderDashboard();

    expect(await screen.findByText('Unable to load supervisor dashboard at this time.')).toBeInTheDocument();
  });

  it('preserves cached dashboard data and shows a warning when refresh fails', async () => {
    mockedGet.mockRejectedValue(new ApiError('Service unavailable', 503));

    renderDashboard({ seedData: populatedDashboard });

    expect(await screen.findByText('Showing the last known dashboard data. The latest refresh failed.')).toBeInTheDocument();
    expect(screen.getByText('CLM-0001')).toBeInTheDocument();
    expect(screen.getByText('CLM-0002')).toBeInTheDocument();
  });

  it('high-risk claim preview rows link to the claim detail page', async () => {
    renderDashboard({ seedData: populatedDashboard });

    const highRiskSection = await screen.findByRole('region', { name: 'High-risk claims' });
    const link = within(highRiskSection).getByRole('link', { name: 'Open claim CLM-0001' });

    expect(link).toHaveAttribute('href', '/claims/claim-1/edit');
  });

  it('aging claim preview rows link to the claim detail page', async () => {
    renderDashboard({ seedData: populatedDashboard });

    const agingSection = await screen.findByRole('region', { name: 'Aging claims' });
    const link = within(agingSection).getByRole('link', { name: 'Open claim CLM-0002' });

    expect(link).toHaveAttribute('href', '/claims/claim-2/edit');
  });

  it('blocker cluster chips are clickable buttons for drill-down navigation', async () => {
    renderDashboard({ seedData: populatedDashboard });

    const blockerSection = await screen.findByRole('region', { name: 'Blocker summary' });

    expect(within(blockerSection).getByRole('button', { name: /awaiting-payment-approval/ })).toBeInTheDocument();
    expect(within(blockerSection).getByRole('button', { name: /Pending documentation/ })).toBeInTheDocument();
  });

  it('high-risk claim drill-down preserves supervisor origin state into claim detail', async () => {
    const user = userEvent.setup();

    renderDashboardRoutes({ seedData: populatedDashboard });

    const highRiskSection = await screen.findByRole('region', { name: 'High-risk claims' });
    await user.click(within(highRiskSection).getByRole('link', { name: 'Open claim CLM-0001' }));

    expect(await screen.findByTestId('destination-pathname')).toHaveTextContent('/claims/claim-1/edit');
    expect(screen.getByTestId('destination-origin-label')).toHaveTextContent('High-risk claims');
    expect(screen.getByTestId('destination-origin-back-to')).toHaveTextContent('/');
  });

  it('blocker cluster drill-down lands on the filtered queue segment with supervisor origin state', async () => {
    const user = userEvent.setup();

    renderDashboardRoutes({ seedData: populatedDashboard });

    const blockerSection = await screen.findByRole('region', { name: 'Blocker summary' });
    await user.click(within(blockerSection).getByRole('button', { name: /awaiting-payment-approval/i }));

    expect(await screen.findByTestId('destination-pathname')).toHaveTextContent('/claims');
    expect(screen.getByTestId('destination-search')).toHaveTextContent('?blockerType=awaiting-payment-approval&hasBlocker=true');
    expect(screen.getByTestId('destination-origin-label')).toHaveTextContent('awaiting-payment-approval');
    expect(screen.getByTestId('destination-origin-back-to')).toHaveTextContent('/');
  });
});
