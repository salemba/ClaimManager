import DashboardRounded from '@mui/icons-material/DashboardRounded';
import FolderOpenRounded from '@mui/icons-material/FolderOpenRounded';
import RuleFolderRounded from '@mui/icons-material/RuleFolderRounded';
import ShieldOutlined from '@mui/icons-material/ShieldOutlined';
import type { SvgIconComponent } from '@mui/icons-material';

export interface WorkbenchRouteMeta {
  key: string;
  to: string;
  path?: string;
  navLabel: string;
  eyebrow: string;
  title: string;
  description: string;
  placeholderSummary: string;
  icon: SvgIconComponent;
}

export interface WorkbenchRouteHandle {
  workbench: WorkbenchRouteMeta;
}

export const primaryWorkbenchRoutes: WorkbenchRouteMeta[] = [
  {
    key: 'dashboard',
    to: '/',
    navLabel: 'Dashboard',
    eyebrow: 'Operational overview',
    title: 'Operational dashboard',
    description: 'Review workspace health, secure access context, and seeded claim activity in a stable workbench shell.',
    placeholderSummary: 'This route anchors daily operations and becomes the primary landing surface for claim-handling work.',
    icon: DashboardRounded,
  },
  {
    key: 'claims',
    to: '/claims',
    path: 'claims',
    navLabel: 'Claims',
    eyebrow: 'Claims queue',
    title: 'Claims work queue',
    description: 'Claim intake, assignment, and claim-file navigation will expand here in later stories.',
    placeholderSummary: 'This scaffold reserves a predictable route for claim-focused workflow panels without pretending the workflow is already built.',
    icon: FolderOpenRounded,
  },
  {
    key: 'approvals',
    to: '/approvals',
    path: 'approvals',
    navLabel: 'Approvals',
    eyebrow: 'Approval routing',
    title: 'Approvals workbench',
    description: 'Escalations, approval requests, and material workflow decisions will surface here.',
    placeholderSummary: 'This route keeps approval work visible in navigation before approval-specific components are introduced.',
    icon: RuleFolderRounded,
  },
  {
    key: 'governance',
    to: '/governance',
    path: 'governance',
    navLabel: 'Governance',
    eyebrow: 'Governance controls',
    title: 'Governance and controls',
    description: 'Audit, permissions, and policy configuration surfaces will live here as the platform expands.',
    placeholderSummary: 'This scaffold establishes a stable destination for control-plane capabilities without introducing governance features early.',
    icon: ShieldOutlined,
  },
];

export const defaultWorkbenchRoute = primaryWorkbenchRoutes[0];