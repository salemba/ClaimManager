import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, RouterProvider } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthenticatedLayout } from '../../../src/ClaimManager.Frontend/src/app/layouts/AuthenticatedLayout';
import { AppProviders } from '../../../src/ClaimManager.Frontend/src/app/providers/AppProviders';
import { DashboardPage } from '../../../src/ClaimManager.Frontend/src/app/routes/DashboardPage';
import { WorkbenchPlaceholderPage } from '../../../src/ClaimManager.Frontend/src/app/routes/WorkbenchPlaceholderPage';
import {
  defaultWorkbenchRoute,
  primaryWorkbenchRoutes,
} from '../../../src/ClaimManager.Frontend/src/app/router/workbench';
import { LoginForm } from '../../../src/ClaimManager.Frontend/src/features/auth/components/LoginForm';
import { ApiError } from '../../../src/ClaimManager.Frontend/src/shared/api/client';
import type { WorkspacePayload } from '../../../src/ClaimManager.Frontend/src/features/auth/api/authApi';
import { getWorkspace, login, logout } from '../../../src/ClaimManager.Frontend/src/features/auth/api/authApi';

vi.mock('../../../src/ClaimManager.Frontend/src/features/auth/api/authApi', () => ({
  getWorkspace: vi.fn(),
  login: vi.fn(),
  logout: vi.fn(),
}));

const mockedGetWorkspace = vi.mocked(getWorkspace);
const mockedLogin = vi.mocked(login);
const mockedLogout = vi.mocked(logout);

const workspaceFixture: WorkspacePayload = {
  user: {
    userId: 'adjuster-1',
    email: 'adjuster@claimmanager.local',
    roles: ['Adjuster', 'Supervisor'],
  },
  databaseAvailable: true,
  claims: [
    {
      claimNumber: 'CLM-1001',
      status: 'Open',
      createdAtUtc: '2026-05-10T09:30:00Z',
    },
    {
      claimNumber: 'CLM-1002',
      status: 'Pending Review',
      createdAtUtc: '2026-05-11T12:00:00Z',
    },
  ],
};

function renderWithProviders(router: ReturnType<typeof createMemoryRouter>) {
  return render(
    <AppProviders>
      <RouterProvider router={router} />
    </AppProviders>,
  );
}

function createWorkbenchRouter(initialEntries: string[]) {
  const claimsRoute = primaryWorkbenchRoutes.find((route) => route.key === 'claims');
  const approvalsRoute = primaryWorkbenchRoutes.find((route) => route.key === 'approvals');
  const governanceRoute = primaryWorkbenchRoutes.find((route) => route.key === 'governance');

  if (!claimsRoute || !approvalsRoute || !governanceRoute) {
    throw new Error('Expected primary workbench routes to be defined.');
  }

  return createMemoryRouter(
    [
      {
        path: '/sign-in',
        element: <LoginForm />,
      },
      {
        path: '/',
        element: <AuthenticatedLayout />,
        children: [
          {
            index: true,
            handle: {
              workbench: defaultWorkbenchRoute,
            },
            element: <DashboardPage />,
          },
          {
            path: claimsRoute.path,
            handle: {
              workbench: claimsRoute,
            },
            element: <WorkbenchPlaceholderPage meta={claimsRoute} />,
          },
          {
            path: approvalsRoute.path,
            handle: {
              workbench: approvalsRoute,
            },
            element: <WorkbenchPlaceholderPage meta={approvalsRoute} />,
          },
          {
            path: governanceRoute.path,
            handle: {
              workbench: governanceRoute,
            },
            element: <WorkbenchPlaceholderPage meta={governanceRoute} />,
          },
        ],
      },
    ],
    { initialEntries },
  );
}

describe('ClaimManager workbench foundation', () => {
  beforeEach(() => {
    mockedGetWorkspace.mockResolvedValue(workspaceFixture);
    mockedLogin.mockResolvedValue({
      userId: workspaceFixture.user.userId,
      email: workspaceFixture.user.email,
      roles: workspaceFixture.user.roles,
    });
    mockedLogout.mockResolvedValue(undefined);
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders the themed dashboard shell with accessible navigation and summary landmarks', async () => {
    const user = userEvent.setup();
    const router = createWorkbenchRouter(['/']);

    renderWithProviders(router);

    expect(await screen.findByRole('heading', { level: 1, name: 'Operational dashboard' })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: 'Primary' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { level: 2, name: 'Initial claims workspace' })).toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Workspace summary' })).toBeInTheDocument();
    expect(screen.getByText('Theme provider active')).toBeInTheDocument();

    await user.tab();
    expect(screen.getByRole('link', { name: 'Skip to main content' })).toHaveFocus();

    await user.tab();
    expect(screen.getByRole('link', { name: /Dashboard/i })).toHaveFocus();

    await user.tab();
    expect(screen.getByRole('link', { name: /Claims/i })).toHaveFocus();
  });

  it('renders predictable placeholder routes for workbench destinations', async () => {
    const router = createWorkbenchRouter(['/claims']);

    renderWithProviders(router);

    expect(await screen.findByRole('heading', { level: 1, name: 'Claims work queue' })).toBeInTheDocument();
    expect(screen.getByText('Placeholder route only. Workflow implementation remains intentionally out of scope for Story 1.2.')).toBeInTheDocument();

    const primaryNavigation = screen.getAllByRole('navigation', { name: 'Primary' });
    expect(
      primaryNavigation.some((navigation) =>
        within(navigation).getByRole('link', { name: /Claims/i }).getAttribute('aria-current') === 'page',
      ),
    ).toBe(true);
  });

  it('keeps sign-in failures accessible while preserving seeded account guidance', async () => {
    const user = userEvent.setup();
    const router = createMemoryRouter(
      [
        {
          path: '/sign-in',
          element: <LoginForm />,
        },
      ],
      { initialEntries: ['/sign-in'] },
    );

    mockedLogin.mockRejectedValueOnce(new ApiError('Invalid credentials.', 401));

    renderWithProviders(router);

    await user.click(await screen.findByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid credentials.');
    expect(screen.getByRole('region', { name: 'Seeded accounts' })).toBeInTheDocument();
    expect(screen.getByText('adjuster@claimmanager.local / Adjuster!2345')).toBeInTheDocument();
  });
});