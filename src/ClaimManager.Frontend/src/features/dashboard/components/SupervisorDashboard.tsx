import { useQuery } from '@tanstack/react-query';
import BlockRounded from '@mui/icons-material/BlockRounded';
import ErrorOutlineRounded from '@mui/icons-material/ErrorOutlineRounded';
import HourglassTopRounded from '@mui/icons-material/HourglassTopRounded';
import PriorityHighRounded from '@mui/icons-material/PriorityHighRounded';
import { Alert, Box, Chip, Paper, Stack, Typography } from '@mui/material';
import { Link as RouterLink, useNavigate } from 'react-router-dom';
import { getSupervisorDashboard } from '../api/dashboardApi';
import type { BlockerGroupSummary, DashboardClaimPreview, SupervisorDashboardSignals } from '../api/dashboardApi';
import { IntegrationHealthPanel } from '../../workspace/IntegrationHealthPanel';
import { ApiError } from '../../../shared/api/client';

function SignalCard({ label, count, icon, tone }: { label: string; count: number; icon: React.ReactNode; tone: 'error' | 'warning' | 'info' | 'default' }) {
  const bgColor = {
    error: 'error.main',
    warning: 'warning.main',
    info: 'info.main',
    default: 'action.hover',
  }[tone];

  const textColor = tone === 'default' ? 'text.primary' : `${tone}.contrastText`;

  return (
    <Paper
      component="article"
      variant="outlined"
      sx={{
        p: { xs: 2.5, md: 3 },
        bgcolor: count > 0 ? bgColor : 'background.paper',
        borderColor: count > 0 ? bgColor : 'divider',
        color: count > 0 ? textColor : 'text.primary',
      }}
      aria-label={`${label}: ${count}`}
    >
      <Stack spacing={1}>
        <Stack direction="row" spacing={1} sx={{ alignItems: 'center' }}>
          {icon}
          <Typography variant="caption" sx={{ fontWeight: 'medium', opacity: count > 0 ? 0.9 : 1 }}>
            {label}
          </Typography>
        </Stack>
        <Typography variant="h2" component="span" sx={{ fontSize: { xs: '2rem', md: '2.5rem' }, lineHeight: 1 }}>
          {count}
        </Typography>
      </Stack>
    </Paper>
  );
}

function SignalGrid({ signals }: { signals: SupervisorDashboardSignals }) {
  return (
    <Box
      sx={{
        display: 'grid',
        gridTemplateColumns: { xs: '1fr 1fr', md: 'repeat(4, 1fr)' },
        gap: 2,
      }}
    >
      <SignalCard
        label="Stuck claims"
        count={signals.stuckCount}
        icon={<BlockRounded fontSize="small" />}
        tone={signals.stuckCount > 0 ? 'error' : 'default'}
      />
      <SignalCard
        label="Aging claims"
        count={signals.agingCount}
        icon={<HourglassTopRounded fontSize="small" />}
        tone={signals.agingCount > 0 ? 'warning' : 'default'}
      />
      <SignalCard
        label="Attention required"
        count={signals.attentionRequiredCount}
        icon={<ErrorOutlineRounded fontSize="small" />}
        tone={signals.attentionRequiredCount > 0 ? 'error' : 'default'}
      />
      <SignalCard
        label="Approval pressure"
        count={signals.approvalPressureCount}
        icon={<PriorityHighRounded fontSize="small" />}
        tone={signals.approvalPressureCount > 0 ? 'warning' : 'default'}
      />
    </Box>
  );
}

function BlockerSummarySection({ summary }: { summary: BlockerGroupSummary[] }) {
  const navigate = useNavigate();

  if (summary.length === 0) {
    return null;
  }

  return (
    <Paper component="section" variant="outlined" sx={{ p: { xs: 2.5, md: 3 } }} aria-label="Blocker summary">
      <Stack spacing={2}>
        <Typography variant="h3">Queue blockers</Typography>
        <Stack direction="row" spacing={1} sx={{ flexWrap: 'wrap', gap: 1 }}>
          {summary.map((group) => (
            <Chip
              key={group.blockerType}
              label={`${group.blockerType} (${group.count})`}
              color="error"
              variant="outlined"
              size="small"
              clickable
              onClick={() =>
                navigate(
                  `/claims?blockerType=${encodeURIComponent(group.blockerType)}&hasBlocker=true`,
                  { state: { dashboardOrigin: { label: group.blockerType, backTo: '/' } } },
                )
              }
            />
          ))}
        </Stack>
      </Stack>
    </Paper>
  );
}

function ClaimPreviewRow({ claim, signalLabel }: { claim: DashboardClaimPreview; signalLabel: string }) {
  const isHighRisk = claim.hasDataIntegrityWarning || claim.blockerType != null;

  return (
    <RouterLink
      to={`/claims/${claim.id}/edit`}
      state={{ dashboardOrigin: { label: signalLabel, backTo: '/' } }}
      aria-label={`Open claim ${claim.claimNumber}`}
      style={{ textDecoration: 'none', color: 'inherit', display: 'block' }}
    >
      <Stack
        direction={{ xs: 'column', sm: 'row' }}
        spacing={1.5}
        sx={{
          alignItems: { sm: 'flex-start' },
          py: 1.5,
          px: 1,
          borderRadius: 1,
          '&:hover': { bgcolor: 'action.hover' },
        }}
      >
        <Stack spacing={0.25} sx={{ minWidth: 130 }}>
          <Typography variant="body2" sx={{ fontWeight: 'medium' }}>
            {claim.claimNumber}
          </Typography>
          <Typography variant="caption" color="text.secondary">
            {claim.status}
          </Typography>
        </Stack>

        <Stack spacing={0.25} sx={{ flex: 1 }}>
          <Typography variant="body2">{claim.claimantName}</Typography>
          {claim.blockerType && (
            <Typography variant="caption" color="error.main">
              Blocker: {claim.blockerType}
            </Typography>
          )}
          {claim.hasDataIntegrityWarning && (
            <Typography variant="caption" color="warning.main">
              Data integrity issue
            </Typography>
          )}
        </Stack>

        <Stack spacing={0.25} sx={{ textAlign: { sm: 'right' }, flexShrink: 0 }}>
          <Typography variant="caption" color={claim.daysSinceCreated >= 14 ? 'error.main' : 'text.secondary'}>
            {claim.daysSinceCreated} {claim.daysSinceCreated === 1 ? 'day' : 'days'} old
          </Typography>
          {claim.ownedByUserId && (
            <Typography variant="caption" color="text.secondary">
              {claim.ownedByUserId}
            </Typography>
          )}
        </Stack>

        {isHighRisk && (
          <Box sx={{ display: { xs: 'none', sm: 'flex' }, alignItems: 'flex-start', pt: 0.25 }}>
            <PriorityHighRounded fontSize="small" color="error" aria-label="High risk" />
          </Box>
        )}
      </Stack>
    </RouterLink>
  );
}

function ClaimPreviewSection({
  claims,
  eyebrow,
  title,
  emptyMessage,
  ariaLabel,
  signalLabel,
}: {
  claims: DashboardClaimPreview[];
  eyebrow: string;
  title: string;
  emptyMessage: string;
  ariaLabel: string;
  signalLabel: string;
}) {
  return (
    <Paper component="section" sx={{ p: { xs: 2.5, md: 3 } }} aria-label={ariaLabel}>
      <Stack spacing={2}>
        <div>
          <Typography variant="overline" color="text.secondary">
            {eyebrow}
          </Typography>
          <Typography variant="h3">{title}</Typography>
        </div>

        {claims.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            {emptyMessage}
          </Typography>
        ) : (
          <Stack divider={<Box sx={{ borderBottom: '1px solid', borderColor: 'divider' }} />}>
            {claims.map((claim) => (
              <ClaimPreviewRow key={claim.id} claim={claim} signalLabel={signalLabel} />
            ))}
          </Stack>
        )}
      </Stack>
    </Paper>
  );
}

export function SupervisorDashboard() {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['dashboard', 'supervisor'],
    queryFn: getSupervisorDashboard,
    staleTime: 30_000,
    refetchInterval: 60_000,
    refetchOnMount: 'always',
  });

  const isForbidden = isError && error instanceof ApiError && error.status === 403;
  const hasLoadedData = data !== undefined;
  const showFatalError = isError && !hasLoadedData && !isForbidden;
  const showCachedDataWarning = isError && hasLoadedData && !isForbidden;

  return (
    <Stack spacing={3}>
      <Paper component="section" sx={{ p: { xs: 2.5, md: 3 } }} aria-label="Supervisor risk signals">
        <Stack spacing={2.5}>
          <div>
            <Typography variant="overline" color="text.secondary">
              Operational risk
            </Typography>
            <Typography variant="h2">Supervisor dashboard</Typography>
          </div>

          {isForbidden ? (
            <Alert severity="info">
              Supervisor or admin access is required to view operational risk signals.
            </Alert>
          ) : isLoading ? (
            <Typography variant="body2" color="text.secondary">
              Loading risk signals…
            </Typography>
          ) : showFatalError ? (
            <Alert severity="error">
              Unable to load supervisor dashboard at this time.
            </Alert>
          ) : data ? (
            <Stack spacing={2.5}>
              {showCachedDataWarning ? (
                <Alert severity="warning">
                  Showing the last known dashboard data. The latest refresh failed.
                </Alert>
              ) : null}
              <SignalGrid signals={data.signals} />
              {data.blockerSummary.length > 0 && (
                <BlockerSummarySection summary={data.blockerSummary} />
              )}
              {data.generatedAtUtc && (
                <Typography variant="caption" color="text.secondary">
                  Data as of {new Date(data.generatedAtUtc).toLocaleString()}
                </Typography>
              )}
            </Stack>
          ) : null}
        </Stack>
      </Paper>

      {!isForbidden && data && (
        <Box
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', xl: 'repeat(2, minmax(0, 1fr))' },
            gap: 3,
          }}
        >
          <ClaimPreviewSection
            ariaLabel="High-risk claims"
            claims={data.highRiskClaims}
            eyebrow="Priority attention"
            title="High-risk claims"
            emptyMessage="No claims currently require immediate supervisor attention."
            signalLabel="High-risk claims"
          />
          <ClaimPreviewSection
            ariaLabel="Aging claims"
            claims={data.agingClaims}
            eyebrow="Aging pressure"
            title="Aging claims"
            emptyMessage="No claims currently exceed the aging threshold."
            signalLabel="Aging claims"
          />
        </Box>
      )}

      <IntegrationHealthPanel />
    </Stack>
  );
}
