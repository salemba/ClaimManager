import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { CssBaseline, ThemeProvider } from '@mui/material';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { IntegrationHealthPanel } from '../../../../../src/ClaimManager.Frontend/src/features/workspace/IntegrationHealthPanel';
import type { IntegrationHealthResponse } from '../../../../../src/ClaimManager.Frontend/src/features/auth/api/authApi';
import { appTheme } from '../../../../../src/ClaimManager.Frontend/src/shared/ui/theme';

vi.mock('../../../../../src/ClaimManager.Frontend/src/features/auth/api/authApi', () => ({
  getIntegrationHealth: vi.fn(),
}));

import { getIntegrationHealth } from '../../../../../src/ClaimManager.Frontend/src/features/auth/api/authApi';
const mockedGet = vi.mocked(getIntegrationHealth);

const degradedResponse: IntegrationHealthResponse = {
  entries: [
    { name: 'document-repository', status: 'degraded', description: 'Document repository is running in local stub mode — no BaseUrl configured', activeIncidentStartedAtUtc: '2026-05-14T17:55:00Z', lastResolvedIncidentAtUtc: null },
    { name: 'messaging', status: 'degraded', description: 'Messaging is running in local stub mode — no BaseUrl configured', activeIncidentStartedAtUtc: '2026-05-14T17:56:00Z', lastResolvedIncidentAtUtc: null },
    { name: 'payment-system', status: 'degraded', description: 'Payment system is running in local stub mode — no BaseUrl configured', activeIncidentStartedAtUtc: '2026-05-14T17:57:00Z', lastResolvedIncidentAtUtc: null },
    { name: 'policy-system', status: 'degraded', description: 'Policy system is running in local stub mode — no BaseUrl configured', activeIncidentStartedAtUtc: '2026-05-14T17:58:00Z', lastResolvedIncidentAtUtc: null },
  ],
  reportedAtUtc: '2026-05-14T18:00:00Z',
};

const healthyResponse: IntegrationHealthResponse = {
  entries: [
    { name: 'policy-system', status: 'healthy', description: 'Policy system operational.', activeIncidentStartedAtUtc: null, lastResolvedIncidentAtUtc: '2026-05-14T17:59:00Z' },
    { name: 'payment-system', status: 'healthy', description: 'Payment system operational.', activeIncidentStartedAtUtc: null, lastResolvedIncidentAtUtc: null },
  ],
  reportedAtUtc: '2026-05-14T18:00:00Z',
};

function renderPanel(options?: { seedData?: IntegrationHealthResponse }) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
        staleTime: 0,
      },
    },
  });

  if (options?.seedData) {
    queryClient.setQueryData(['integration-health'], options.seedData);
  }

  return {
    queryClient,
    ...render(
      <QueryClientProvider client={queryClient}>
        <ThemeProvider theme={appTheme}>
          <CssBaseline />
          <MemoryRouter>
            <IntegrationHealthPanel />
          </MemoryRouter>
        </ThemeProvider>
      </QueryClientProvider>,
    ),
  };
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe('IntegrationHealthPanel', () => {
  it('renders the panel heading', async () => {
    mockedGet.mockResolvedValue(degradedResponse);
    renderPanel();

    expect(await screen.findByRole('heading', { name: 'Integration Health' })).toBeInTheDocument();
  });

  it('renders all four boundary names when data loads', async () => {
    mockedGet.mockResolvedValue(degradedResponse);
    renderPanel();

    expect(await screen.findByText('Policy System')).toBeInTheDocument();
    expect(screen.getByText('Payment System')).toBeInTheDocument();
    expect(screen.getByText('Document Repository')).toBeInTheDocument();
    expect(screen.getByText('Messaging')).toBeInTheDocument();
  });

  it('shows degraded status chips for all degraded entries', async () => {
    mockedGet.mockResolvedValue(degradedResponse);
    renderPanel();

    await waitFor(() => {
      const chips = screen.getAllByText('degraded');
      expect(chips).toHaveLength(4);
    });
  });

  it('shows warning banner when one or more boundaries are degraded', async () => {
    mockedGet.mockResolvedValue(degradedResponse);
    renderPanel();

    expect(await screen.findByText(/4 integration boundaries are degraded/)).toBeInTheDocument();
    expect(screen.getByText(/Claim data freshness may be affected/)).toBeInTheDocument();
  });

  it('does not show warning banner when all boundaries are healthy', async () => {
    mockedGet.mockResolvedValue(healthyResponse);
    renderPanel();

    await screen.findByText('Policy System');
    expect(screen.queryByText(/degraded/i)).not.toBeInTheDocument();
  });

  it('shows each entry description text', async () => {
    mockedGet.mockResolvedValue(degradedResponse);
    renderPanel();

    const descriptions = await screen.findAllByText(/local stub mode/);
    expect(descriptions.length).toBe(4);
  });

  it('shows error alert when query fails', async () => {
    mockedGet.mockRejectedValue(new Error('network error'));
    renderPanel();

    expect(await screen.findByText('Unable to load integration health at this time.')).toBeInTheDocument();
  });

  it('shows singular wording for a single degraded boundary', async () => {
    mockedGet.mockResolvedValue({
      entries: [{ name: 'messaging', status: 'degraded', description: 'Stub mode.', activeIncidentStartedAtUtc: null, lastResolvedIncidentAtUtc: null }],
      reportedAtUtc: '2026-05-14T18:00:00Z',
    });
    renderPanel();

    expect(await screen.findByText(/1 integration boundary is degraded/)).toBeInTheDocument();
    expect(screen.getByText(/Messaging-only disruption may affect outbound updates/)).toBeInTheDocument();
  });

  it('shows an empty success state when the endpoint returns no entries', async () => {
    mockedGet.mockResolvedValue({
      entries: [],
      reportedAtUtc: '2026-05-14T18:00:00Z',
    });
    renderPanel();

    expect(await screen.findByText('No integration health boundaries are currently configured.')).toBeInTheDocument();
  });

  it('keeps the last known statuses visible when a refresh fails after cached data exists', async () => {
    mockedGet.mockRejectedValue(new Error('network error'));
    const { queryClient } = renderPanel({ seedData: degradedResponse });

    await queryClient.refetchQueries({ queryKey: ['integration-health'] });

    expect(await screen.findByText('Policy System')).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByText('Showing the last known integration health. The latest refresh failed.')).toBeInTheDocument();
    });
  });

  it('shows recovery evidence for a healthy boundary with a recorded incident history', async () => {
    mockedGet.mockResolvedValue(healthyResponse);
    renderPanel();

    expect(await screen.findByText(/Last incident recovered at/)).toBeInTheDocument();
  });

  it('shows unhealthy wording when an unhealthy boundary is returned', async () => {
    mockedGet.mockResolvedValue({
      entries: [
        {
          name: 'policy-system',
          status: 'unhealthy',
          description: 'Policy system unavailable.',
          activeIncidentStartedAtUtc: '2026-05-14T17:58:00Z',
          lastResolvedIncidentAtUtc: null,
        },
      ],
      reportedAtUtc: '2026-05-14T18:00:00Z',
    });
    renderPanel();

    expect(await screen.findByText(/1 integration boundary is unhealthy/)).toBeInTheDocument();
  });
});
