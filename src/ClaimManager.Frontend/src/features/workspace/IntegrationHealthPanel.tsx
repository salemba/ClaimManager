import { useQuery } from '@tanstack/react-query';
import { Alert, Chip, Paper, Stack, Typography } from '@mui/material';
import { getIntegrationHealth } from '../auth/api/authApi';
import type { IntegrationHealthEntry } from '../auth/api/authApi';

const CLAIM_DATA_BOUNDARIES = new Set(['policy-system', 'payment-system', 'document-repository']);

function statusChipColor(status: string): 'success' | 'warning' | 'error' | 'default' {
  if (status === 'healthy') return 'success';
  if (status === 'degraded') return 'warning';
  if (status === 'unhealthy') return 'error';
  return 'default';
}

function boundaryLabel(name: string): string {
  const labels: Record<string, string> = {
    'policy-system': 'Policy System',
    'payment-system': 'Payment System',
    'document-repository': 'Document Repository',
    messaging: 'Messaging',
  };
  return labels[name] ?? name;
}

function formatTimestamp(utcString: string): string {
  const date = new Date(utcString);
  return Number.isNaN(date.getTime()) ? utcString : date.toLocaleString();
}

function buildStatusSummary(unhealthyCount: number, degradedCount: number): string {
  const parts: string[] = [];

  if (unhealthyCount > 0) {
    parts.push(`${unhealthyCount} integration ${unhealthyCount === 1 ? 'boundary is' : 'boundaries are'} unhealthy`);
  }

  if (degradedCount > 0) {
    parts.push(`${degradedCount} integration ${degradedCount === 1 ? 'boundary is' : 'boundaries are'} degraded`);
  }

  return `${parts.join(' and ')}.`;
}

function buildImpactSummary(entries: IntegrationHealthEntry[]): string {
  const claimDataBoundariesAffected = entries.some((entry) => entry.status !== 'healthy' && CLAIM_DATA_BOUNDARIES.has(entry.name));

  if (claimDataBoundariesAffected) {
    return 'Claim data freshness may be affected until policy, payment, or document boundaries recover.';
  }

  return 'Messaging-only disruption may affect outbound updates, but not stored claim data freshness.';
}

function IntegrationHealthRow({ entry }: { entry: IntegrationHealthEntry }) {
  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ alignItems: { sm: 'flex-start' } }}>
      <Stack spacing={0.25} sx={{ minWidth: 160 }}>
        <Typography variant="body2" sx={{ fontWeight: 'medium' }}>
          {boundaryLabel(entry.name)}
        </Typography>
        <Chip
          label={entry.status}
          color={statusChipColor(entry.status)}
          size="small"
          sx={{ width: 'fit-content' }}
        />
      </Stack>
      <Stack spacing={0.5} sx={{ pt: { sm: 0.25 } }}>
        <Typography variant="body2" color="text.secondary">
          {entry.description}
        </Typography>
        {entry.activeIncidentStartedAtUtc && (
          <Typography variant="caption" color="text.secondary">
            Incident active since {formatTimestamp(entry.activeIncidentStartedAtUtc)}
          </Typography>
        )}
        {!entry.activeIncidentStartedAtUtc && entry.lastResolvedIncidentAtUtc && (
          <Typography variant="caption" color="text.secondary">
            Last incident recovered at {formatTimestamp(entry.lastResolvedIncidentAtUtc)}
          </Typography>
        )}
      </Stack>
    </Stack>
  );
}

export function IntegrationHealthPanel() {
  const { data, isError, isLoading, isRefetchError } = useQuery({
    queryKey: ['integration-health'],
    queryFn: getIntegrationHealth,
    refetchInterval: 60_000,
    staleTime: 30_000,
  });

  const hasLoadedData = data !== undefined;

  if (isError && !hasLoadedData) {
    return (
      <Alert severity="error">
        Unable to load integration health at this time.
      </Alert>
    );
  }

  const entries = data?.entries ?? [];
  const unhealthyCount = entries.filter((entry) => entry.status === 'unhealthy').length;
  const degradedCount = entries.filter((entry) => entry.status === 'degraded').length;
  const hasNonHealthyEntries = unhealthyCount > 0 || degradedCount > 0;
  const showLoadingState = isLoading && !hasLoadedData;
  const bannerSeverity = unhealthyCount > 0 ? 'error' : 'warning';

  return (
    <Paper
      component="section"
      variant="outlined"
      sx={{ p: { xs: 3, md: 4 }, borderColor: 'divider', bgcolor: 'background.default' }}
      aria-label="Integration health"
    >
      <Stack spacing={2}>
        <div>
          <Typography variant="overline" color="text.secondary">
            Operational status
          </Typography>
          <Typography variant="h3">Integration Health</Typography>
        </div>

        {isRefetchError && hasLoadedData && (
          <Alert severity="warning">
            Showing the last known integration health. The latest refresh failed.
          </Alert>
        )}

        {hasNonHealthyEntries && (
          <Alert severity={bannerSeverity}>
            {buildStatusSummary(unhealthyCount, degradedCount)} {buildImpactSummary(entries)}
          </Alert>
        )}

        {showLoadingState ? (
          <Typography variant="body2" color="text.secondary">
            Loading integration status…
          </Typography>
        ) : entries.length === 0 ? (
          <Typography variant="body2" color="text.secondary">
            No integration health boundaries are currently configured.
          </Typography>
        ) : (
          <Stack spacing={2} divider={<div style={{ borderBottom: '1px solid var(--mui-palette-divider)' }} />}>
            {entries.map((entry) => (
              <IntegrationHealthRow key={entry.name} entry={entry} />
            ))}
          </Stack>
        )}

        {data?.reportedAtUtc && (
          <Typography variant="caption" color="text.secondary">
            Status as of {new Date(data.reportedAtUtc).toLocaleString()}
          </Typography>
        )}
      </Stack>
    </Paper>
  );
}
