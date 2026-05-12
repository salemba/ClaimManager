import { useQuery } from '@tanstack/react-query';
import MenuRounded from '@mui/icons-material/MenuRounded';
import PersonOutlineRounded from '@mui/icons-material/PersonOutlineRounded';
import StorageRounded from '@mui/icons-material/StorageRounded';
import LogoutRounded from '@mui/icons-material/LogoutRounded';
import {
  Box,
  Button,
  CircularProgress,
  Drawer,
  IconButton,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Paper,
  Stack,
  Typography,
} from '@mui/material';
import { Navigate, NavLink, Outlet, useMatches, useNavigate } from 'react-router-dom';
import { logout, getWorkspace } from '../../features/auth/api/authApi';
import { useShellStore } from '../../shared/state/shellStore';
import { PageSurface } from '../../shared/ui/PageSurface';
import { StatusBadge } from '../../shared/ui/StatusBadge';
import { defaultWorkbenchRoute, primaryWorkbenchRoutes, type WorkbenchRouteHandle } from '../router/workbench';

export function AuthenticatedLayout() {
  const navigate = useNavigate();
  const matches = useMatches();
  const navigationOpen = useShellStore((state) => state.navigationOpen);
  const openNavigation = useShellStore((state) => state.openNavigation);
  const closeNavigation = useShellStore((state) => state.closeNavigation);
  const workspaceQuery = useQuery({
    queryKey: ['workspace'],
    queryFn: getWorkspace,
  });

  const activeRoute =
    [...matches]
      .reverse()
      .map((match) => (match.handle as WorkbenchRouteHandle | undefined)?.workbench)
      .find(Boolean) ?? defaultWorkbenchRoute;

  if (workspaceQuery.error) {
    return <Navigate to="/sign-in" replace />;
  }

  if (workspaceQuery.isLoading || !workspaceQuery.data) {
    return (
      <PageSurface centered maxWidth={680}>
        <Paper sx={{ p: 4 }}>
          <Stack spacing={2} sx={{ alignItems: 'center' }}>
            <CircularProgress aria-label="Loading secure workspace" />
            <Typography variant="h3">Loading secure workspace...</Typography>
            <Typography color="text.secondary">
              Establishing the authenticated workbench and retrieving current workspace context.
            </Typography>
          </Stack>
        </Paper>
      </PageSurface>
    );
  }

  const handleSignOut = async () => {
    await logout();
    navigate('/sign-in');
  };

  const navigation = (
    <Paper component="nav" aria-label="Primary" sx={{ p: 2.5, position: 'relative' }}>
      <Stack spacing={2.5}>
        <div>
          <Typography variant="overline" color="text.secondary">
            Workbench navigation
          </Typography>
          <Typography variant="h3">ClaimManager</Typography>
          <Typography color="text.secondary">
            Stable, route-oriented navigation for claims operations.
          </Typography>
        </div>

        <List disablePadding>
          {primaryWorkbenchRoutes.map((route) => {
            const Icon = route.icon;
            const isDashboard = route.to === '/';
            const selected = activeRoute.key === route.key;

            return (
              <ListItem key={route.key} disablePadding sx={{ mb: 1 }}>
                <ListItemButton
                  component={NavLink}
                  to={route.to}
                  end={isDashboard}
                  onClick={closeNavigation}
                  selected={selected}
                  sx={{
                    borderRadius: 3,
                    alignItems: 'flex-start',
                    '&.active, &.Mui-selected': {
                      bgcolor: 'primary.light',
                    },
                  }}
                >
                  <ListItemIcon sx={{ minWidth: 40, color: selected ? 'primary.dark' : 'text.secondary', mt: 0.25 }}>
                    <Icon fontSize="small" />
                  </ListItemIcon>
                  <ListItemText
                    primary={route.navLabel}
                    secondary={route.description}
                    slotProps={{
                      primary: { sx: { fontWeight: 700 } },
                      secondary: { sx: { color: 'text.secondary' } },
                    }}
                  />
                </ListItemButton>
              </ListItem>
            );
          })}
        </List>
      </Stack>
    </Paper>
  );

  return (
    <PageSurface>
      <a className="skip-link" href="#main-content">
        Skip to main content
      </a>

      <Drawer
        open={navigationOpen}
        onClose={closeNavigation}
        sx={{
          display: { xs: 'block', md: 'none' },
          '& .MuiDrawer-paper': { width: 320, p: 2, bgcolor: 'background.default' },
        }}
      >
        {navigation}
      </Drawer>

      <Box
        sx={{
          display: 'grid',
          gridTemplateColumns: { xs: '1fr', md: '280px minmax(0, 1fr)' },
          gap: 3,
          alignItems: 'start',
        }}
      >
        <Box sx={{ display: { xs: 'none', md: 'block' }, position: 'sticky', top: 32 }}>
          {navigation}
        </Box>

        <Stack spacing={3}>
          <Paper component="header" sx={{ p: { xs: 3, md: 4 } }}>
            <Stack spacing={2.5}>
              <Stack
                direction={{ xs: 'column', sm: 'row' }}
                spacing={2}
                sx={{ justifyContent: 'space-between', alignItems: { xs: 'flex-start', sm: 'center' } }}
              >
                <Stack spacing={1.5}>
                  <Typography variant="overline" color="text.secondary">
                    {activeRoute.eyebrow}
                  </Typography>
                  <Typography variant="h1">{activeRoute.title}</Typography>
                  <Typography color="text.secondary" sx={{ maxWidth: 760 }}>
                    {activeRoute.description}
                  </Typography>
                </Stack>

                <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center' }}>
                  <IconButton
                    aria-label="Open primary navigation"
                    onClick={openNavigation}
                    sx={{ display: { xs: 'inline-flex', md: 'none' } }}
                  >
                    <MenuRounded />
                  </IconButton>
                  <Button type="button" variant="contained" color="primary" startIcon={<LogoutRounded />} onClick={handleSignOut}>
                    Sign out
                  </Button>
                </Stack>
              </Stack>

              <Box
                component="section"
                aria-label="Workspace summary"
                sx={{
                  display: 'grid',
                  gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, minmax(0, 1fr))' },
                  gap: 2,
                }}
              >
                <Paper variant="outlined" sx={{ p: 2.25 }}>
                  <Stack spacing={1}>
                    <Typography variant="overline" color="text.secondary">
                      Signed in as
                    </Typography>
                    <Typography variant="subtitle1">{workspaceQuery.data.user.email}</Typography>
                    <StatusBadge tone="info" label={`Roles: ${workspaceQuery.data.user.roles.join(', ')}`} icon={<PersonOutlineRounded fontSize="small" />} />
                  </Stack>
                </Paper>

                <Paper variant="outlined" sx={{ p: 2.25 }}>
                  <Stack spacing={1}>
                    <Typography variant="overline" color="text.secondary">
                      Workspace services
                    </Typography>
                    <Typography variant="subtitle1">Database connectivity</Typography>
                    <StatusBadge
                      tone={workspaceQuery.data.databaseAvailable ? 'success' : 'critical'}
                      label={workspaceQuery.data.databaseAvailable ? 'Connected' : 'Unavailable'}
                      icon={<StorageRounded fontSize="small" />}
                    />
                  </Stack>
                </Paper>

                <Paper variant="outlined" sx={{ p: 2.25 }}>
                  <Stack spacing={1}>
                    <Typography variant="overline" color="text.secondary">
                      Seeded workload
                    </Typography>
                    <Typography variant="subtitle1">{workspaceQuery.data.claims.length} active seeded claims</Typography>
                    <StatusBadge tone="neutral" label="Placeholder workflow data" />
                  </Stack>
                </Paper>
              </Box>
            </Stack>
          </Paper>

          <Box component="main" id="main-content">
            <Outlet context={workspaceQuery.data} />
          </Box>
        </Stack>
      </Box>
    </PageSurface>
  );
}