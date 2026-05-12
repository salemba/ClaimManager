import { createBrowserRouter, Navigate } from 'react-router-dom';
import { AuthenticatedLayout } from '../layouts/AuthenticatedLayout';
import { LoginForm } from '../../features/auth/components/LoginForm';
import { DashboardPage } from '../routes/DashboardPage';
import { WorkbenchPlaceholderPage } from '../routes/WorkbenchPlaceholderPage';
import { CreateClaimPage } from '../../features/claims/routes/CreateClaimPage';
import { EditClaimPage } from '../../features/claims/routes/EditClaimPage';
import { defaultWorkbenchRoute, primaryWorkbenchRoutes } from './workbench';

const claimsWorkbenchRoute = primaryWorkbenchRoutes.find((route) => route.key === 'claims');

export const router = createBrowserRouter([
  {
    path: '/sign-in',
    element: <LoginForm />,
  },
  {
    path: '/',
    element: <AuthenticatedLayout />,
    children: [
      {
        handle: {
          workbench: defaultWorkbenchRoute,
        },
        index: true,
        element: <DashboardPage />,
      },
      ...primaryWorkbenchRoutes
        .filter((route) => route.path && route.key !== 'claims')
        .map((route) => ({
          path: route.path,
          handle: {
            workbench: route,
          },
          element: <WorkbenchPlaceholderPage meta={route} />,
        })),
      ...(claimsWorkbenchRoute
        ? [
            {
              path: claimsWorkbenchRoute.path,
              handle: {
                workbench: claimsWorkbenchRoute,
              },
              element: <CreateClaimPage />,
            },
            {
              path: `${claimsWorkbenchRoute.path}/:claimId/edit`,
              handle: {
                workbench: claimsWorkbenchRoute,
              },
              element: <EditClaimPage />,
            },
          ]
        : []),
    ],
  },
  {
    path: '*',
    element: <Navigate to="/" replace />,
  },
]);