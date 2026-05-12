import { createBrowserRouter, Navigate } from 'react-router-dom';
import { AuthenticatedLayout } from '../layouts/AuthenticatedLayout';
import { LoginForm } from '../../features/auth/components/LoginForm';
import { DashboardPage } from '../routes/DashboardPage';
import { WorkbenchPlaceholderPage } from '../routes/WorkbenchPlaceholderPage';
import { defaultWorkbenchRoute, primaryWorkbenchRoutes } from './workbench';

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
        .filter((route) => route.path)
        .map((route) => ({
          path: route.path,
          handle: {
            workbench: route,
          },
          element: <WorkbenchPlaceholderPage meta={route} />,
        })),
    ],
  },
  {
    path: '*',
    element: <Navigate to="/" replace />,
  },
]);