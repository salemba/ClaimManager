import { useOutletContext } from 'react-router-dom';
import CheckCircleOutlineRounded from '@mui/icons-material/CheckCircleOutlineRounded';
import FolderOpenRounded from '@mui/icons-material/FolderOpenRounded';
import WarningAmberRounded from '@mui/icons-material/WarningAmberRounded';
import { Box, List, ListItem, Paper, Stack, Typography } from '@mui/material';
import type { WorkspacePayload } from '../../features/auth/api/authApi';
import { IntegrationHealthPanel } from '../../features/workspace/IntegrationHealthPanel';
import { PageSurface } from '../../shared/ui/PageSurface';
import { StatusBadge } from '../../shared/ui/StatusBadge';

function getClaimTone(status: string) {
  const normalizedStatus = status.toLowerCase();

  if (normalizedStatus.includes('review') || normalizedStatus.includes('pending')) {
    return 'warning' as const;
  }

  if (normalizedStatus.includes('open') || normalizedStatus.includes('active')) {
    return 'info' as const;
  }

  if (normalizedStatus.includes('closed') || normalizedStatus.includes('complete')) {
    return 'success' as const;
  }

  return 'neutral' as const;
}

export function DashboardPage() {
  const workspace = useOutletContext<WorkspacePayload>();

  return (
    <PageSurface>
      <Stack spacing={3}>
        <Paper component="section" aria-label="Initial claims workspace" sx={{ p: { xs: 3, md: 4 } }}>
          <Stack spacing={2}>
            <div>
              <Typography variant="overline" color="text.secondary">
                Foundation verification
              </Typography>
              <Typography variant="h2">Initial claims workspace</Typography>
            </div>
            <Typography color="text.secondary" sx={{ maxWidth: 720 }}>
              The dashboard now uses the shared theme foundation and workbench primitives while active claim intake and maintenance routes feed the operational queue.
            </Typography>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5}>
              <StatusBadge tone="info" label={`${workspace.claims.length} seeded claims available`} icon={<FolderOpenRounded fontSize="small" />} />
              <StatusBadge tone="success" label="Theme provider active" icon={<CheckCircleOutlineRounded fontSize="small" />} />
              <StatusBadge tone="warning" label="Approvals and governance routes still pending" icon={<WarningAmberRounded fontSize="small" />} />
            </Stack>
          </Stack>
        </Paper>

        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', xl: '1.7fr 1fr' },
            gap: 3,
          }}
        >
          <Paper component="section" sx={{ p: { xs: 3, md: 4 } }}>
            <Stack spacing={2.5}>
              <Typography variant="h3">Seeded claim activity</Typography>
              <List disablePadding sx={{ display: 'grid', gap: 1.5 }}>
                {workspace.claims.map((claim) => (
                  <ListItem
                    key={claim.claimNumber}
                    disableGutters
                    sx={{
                      display: 'grid',
                      gap: 1,
                      gridTemplateColumns: { xs: '1fr', sm: 'minmax(0, 1fr) auto' },
                      alignItems: 'center',
                      p: 2,
                      borderRadius: 3,
                      bgcolor: 'background.default',
                      border: (theme) => `1px solid ${theme.palette.divider}`,
                    }}
                  >
                    <Stack spacing={0.5}>
                      <Typography variant="subtitle1">{claim.claimNumber}</Typography>
                      <Typography color="text.secondary">Created {new Date(claim.createdAtUtc).toLocaleString()}</Typography>
                    </Stack>
                    <StatusBadge tone={getClaimTone(claim.status)} label={`Status: ${claim.status}`} />
                  </ListItem>
                ))}
              </List>
            </Stack>
          </Paper>

          <Stack spacing={3}>
            <IntegrationHealthPanel />

            <Paper component="section" variant="outlined" sx={{ p: 3 }}>
              <Typography variant="h3" gutterBottom>
                Accessibility baseline
              </Typography>
              <Typography color="text.secondary">
                Themed focus rings, landmark navigation, and explicit route headings are now part of the shared foundation rather than page-specific CSS.
              </Typography>
            </Paper>
          </Stack>
        </Box>
      </Stack>
    </PageSurface>
  );
}