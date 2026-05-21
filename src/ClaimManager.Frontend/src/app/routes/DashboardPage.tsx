import { Navigate, useOutletContext } from 'react-router-dom';
import type { WorkspacePayload } from '../../features/auth/api/authApi';
import { SupervisorDashboard } from '../../features/dashboard/components/SupervisorDashboard';
import { PageSurface } from '../../shared/ui/PageSurface';

export function DashboardPage() {
  const workspace = useOutletContext<WorkspacePayload>();
  const canViewSupervisorDashboard = workspace.user.roles.some((role) =>
    role.localeCompare('supervisor', undefined, { sensitivity: 'accent' }) === 0 ||
    role.localeCompare('admin', undefined, { sensitivity: 'accent' }) === 0);

  if (!canViewSupervisorDashboard) {
    return <Navigate to="/claims" replace />;
  }

  return (
    <PageSurface>
      <SupervisorDashboard />
    </PageSurface>
  );
}
