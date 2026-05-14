import BlockRounded from '@mui/icons-material/BlockRounded';
import PersonRounded from '@mui/icons-material/PersonRounded';
import WarningAmberRounded from '@mui/icons-material/WarningAmberRounded';
import { Alert, Divider, Paper, Stack, Typography } from '@mui/material';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import type { Claim } from '../types/Claim';

interface ClaimStateSummaryPanelProps {
  claim: Claim;
}

type StatusTone = 'critical' | 'warning' | 'success' | 'info' | 'neutral';

const STATUS_TONE_MAP: Record<string, StatusTone> = {
  blocked: 'critical',
  'in-review': 'warning',
  pending: 'warning',
  approved: 'success',
  closed: 'success',
  resolved: 'success',
  complete: 'success',
  new: 'info',
  open: 'info',
  active: 'info',
  'in-progress': 'info',
};

function getStatusTone(status: string): StatusTone {
  return STATUS_TONE_MAP[status.toLowerCase()] ?? 'neutral';
}

interface SyncFreshnessContextProps {
  policySyncedAtUtc: string | null;
  paymentSyncedAtUtc: string | null;
  documentSyncedAtUtc: string | null;
}

function SyncFreshnessContext({ policySyncedAtUtc, paymentSyncedAtUtc, documentSyncedAtUtc }: SyncFreshnessContextProps) {
  const lines: string[] = [];

  if (policySyncedAtUtc) {
    lines.push(`Policy: last synced ${new Date(policySyncedAtUtc).toLocaleString()}`);
  } else {
    lines.push('Policy: never synced');
  }

  if (paymentSyncedAtUtc) {
    lines.push(`Payment: last synced ${new Date(paymentSyncedAtUtc).toLocaleString()}`);
  } else {
    lines.push('Payment: never synced');
  }

  if (documentSyncedAtUtc) {
    lines.push(`Documents: last synced ${new Date(documentSyncedAtUtc).toLocaleString()}`);
  } else {
    lines.push('Documents: never synced');
  }

  return (
    <Typography variant="caption" component="div" color="inherit">
      {lines.map((line) => (
        <div key={line}>{line}</div>
      ))}
    </Typography>
  );
}

export function ClaimStateSummaryPanel({ claim }: ClaimStateSummaryPanelProps) {
  const { status, blockerType, blockerReason, ownedByUserId, nextExpectedAction, hasDataIntegrityWarning, dataIntegrityWarningMessage } = claim;
  const normalizedBlockerType = blockerType?.trim() || null;

  return (
    <Paper component="section" sx={{ p: { xs: 3, md: 4 } }} aria-label="Claim state summary">
      <Stack spacing={2}>
        <div>
          <Typography variant="overline" color="text.secondary">
            Workflow state
          </Typography>
          <Typography variant="h2">Claim status</Typography>
        </div>

        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} useFlexGap sx={{ flexWrap: 'wrap' }}>
          <Stack spacing={0.5}>
            <Typography variant="caption" color="text.secondary">
              Status
            </Typography>
            <StatusBadge label={status} tone={getStatusTone(status)} />
          </Stack>

          <Stack spacing={0.5}>
            <Typography variant="caption" color="text.secondary">
              Blocker
            </Typography>
            {normalizedBlockerType ? (
              <StatusBadge label={normalizedBlockerType} tone="critical" icon={<BlockRounded />} />
            ) : (
              <Typography variant="body2" color="text.secondary">
                No active blocker
              </Typography>
            )}
          </Stack>

          <Stack spacing={0.5}>
            <Typography variant="caption" color="text.secondary">
              Owner
            </Typography>
            <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
              <PersonRounded fontSize="small" color="action" />
              <Typography variant="body2">{ownedByUserId ?? 'Unassigned'}</Typography>
            </Stack>
          </Stack>
        </Stack>

        {normalizedBlockerType && blockerReason && (
          <>
            <Divider />
            <Stack spacing={0.5}>
              <Typography variant="caption" color="text.secondary">
                Blocker detail
              </Typography>
              <Typography variant="body2">{blockerReason}</Typography>
            </Stack>
          </>
        )}

        <Divider />
        <Stack spacing={0.5}>
          <Typography variant="caption" color="text.secondary">
            Next expected action
          </Typography>
          <Typography variant="body2">
            {nextExpectedAction ?? 'Awaiting workflow progression'}
          </Typography>
        </Stack>

        {hasDataIntegrityWarning && (
          <Alert severity="warning" icon={<WarningAmberRounded />}>
            <Stack spacing={0.5}>
              <span>{dataIntegrityWarningMessage ?? 'This claim has a data integrity issue that may require attention.'}</span>
              <SyncFreshnessContext
                policySyncedAtUtc={claim.policySyncedAtUtc}
                paymentSyncedAtUtc={claim.paymentSyncedAtUtc}
                documentSyncedAtUtc={claim.documentSyncedAtUtc}
              />
            </Stack>
          </Alert>
        )}
      </Stack>
    </Paper>
  );
}
