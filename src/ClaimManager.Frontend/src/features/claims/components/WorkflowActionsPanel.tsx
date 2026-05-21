import ArrowForwardRounded from '@mui/icons-material/ArrowForwardRounded';
import CheckCircleRounded from '@mui/icons-material/CheckCircleRounded';
import PlayArrowRounded from '@mui/icons-material/PlayArrowRounded';
import SupervisorAccountRounded from '@mui/icons-material/SupervisorAccountRounded';
import { Alert, Button, Collapse, MenuItem, Paper, Stack, TextField, Typography } from '@mui/material';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { getWorkspace } from '../../auth/api/authApi';
import type { Claim } from '../types/Claim';

interface WorkflowAction {
  label: string;
  type: 'advance' | 'route-for-approval';
}

const ACTION_DEFINITIONS: Record<string, Omit<WorkflowAction, 'type'>> = {
  advance: { label: 'Advance Workflow' },
  'route-for-approval': { label: 'Route for Payment Approval' },
};

const STATUS_LABELS: Record<string, string> = {
  new: 'Begin Review',
  open: 'Submit for Review',
  approved: 'Close Claim',
};

const WORKFLOW_STATES = ['new', 'open', 'in-review', 'pending', 'approved', 'closed'];

interface WorkflowActionsPanelProps {
  claim: Claim;
  onAdvance: (rowVersion: string) => Promise<void>;
  onRouteForApproval: (rationale: string, rowVersion: string) => Promise<void>;
  onIntervene?: (newOwnerId: string, targetStatus: string, rowVersion: string) => Promise<void>;
  advancing?: boolean;
  routing?: boolean;
  intervening?: boolean;
  advanceError?: string | null;
  routeError?: string | null;
  interveneError?: string | null;
}

export function WorkflowActionsPanel({
  claim,
  onAdvance,
  onRouteForApproval,
  onIntervene,
  advancing = false,
  routing = false,
  intervening = false,
  advanceError,
  routeError,
  interveneError,
}: WorkflowActionsPanelProps) {
  const [showRationaleInput, setShowRationaleInput] = useState(false);
  const [showInterventionInput, setShowInterventionInput] = useState(false);
  const [rationale, setRationale] = useState('');
  const [targetOwnerId, setTargetOwnerId] = useState('');
  const [targetStatus, setTargetStatus] = useState('');
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  const workspaceQuery = useQuery({
    queryKey: ['workspace'],
    queryFn: getWorkspace,
    staleTime: 300_000,
  });

  const isBusy = advancing || routing || intervening;
  const isSupervisor = workspaceQuery.data?.user.roles.includes('supervisor') || workspaceQuery.data?.user.roles.includes('admin');

  // Intervention criteria: blocked > 48h OR amount > 10k
  const isBlockedLongEnough = !!claim.blockedAtUtc && (Date.now() - new Date(claim.blockedAtUtc).getTime()) > 48 * 60 * 60 * 1000;
  const isHighAmount = (claim.paymentAmount ?? 0) > 10000;
  const canIntervene = isBlockedLongEnough || isHighAmount;

  const actions = (claim.availableActions || [])
    .map((actionType) => {
      const definition = ACTION_DEFINITIONS[actionType as keyof typeof ACTION_DEFINITIONS];
      if (!definition) {
        return null;
      }
      const action: WorkflowAction = {
        type: actionType as 'advance' | 'route-for-approval',
        label: definition.label,
      };
      if (action.type === 'advance' && STATUS_LABELS[claim.status]) {
        action.label = STATUS_LABELS[claim.status];
      }
      return action;
    })
    .filter((a): a is WorkflowAction => a !== null);

  const handleAdvance = async () => {
    setSuccessMessage(null);
    try {
      await onAdvance(claim.rowVersion);
      setSuccessMessage(`Claim workflow advanced. The claim has been updated.`);
    } catch {
      // error handled by parent via advanceError prop
    }
  };

  const handleConfirmRouting = async () => {
    const normalizedRationale = rationale.trim();
    setSuccessMessage(null);
    try {
      await onRouteForApproval(normalizedRationale, claim.rowVersion);
      setShowRationaleInput(false);
      setRationale('');
      setSuccessMessage(`Claim routed for approval. The claim has been updated.`);
    } catch {
      // error handled by parent via routeError prop
    }
  };

  const handleConfirmIntervention = async () => {
    if (!onIntervene || !targetOwnerId || !targetStatus) return;
    setSuccessMessage(null);
    try {
      await onIntervene(targetOwnerId, targetStatus, claim.rowVersion);
      setShowInterventionInput(false);
      setTargetOwnerId('');
      setTargetStatus('');
      setSuccessMessage(`Supervisor intervention completed. The claim has been reassigned and progressed.`);
    } catch {
      // error handled by parent via interveneError prop
    }
  };

  return (
    <Paper component="section" sx={{ p: { xs: 3, md: 4 } }} aria-label="Workflow actions">
      <Stack spacing={2.5}>
        <div>
          <Typography variant="overline" color="text.secondary">
            Workflow
          </Typography>
          <Typography variant="h2">Next action</Typography>
          <Typography color="text.secondary">
            Move this claim forward or route it for approval.
          </Typography>
        </div>

        {successMessage ? (
          <Alert severity="success" icon={<CheckCircleRounded />} onClose={() => setSuccessMessage(null)}>
            {successMessage}
          </Alert>
        ) : null}

        {advanceError ? <Alert severity="error">{advanceError}</Alert> : null}
        {routeError ? <Alert severity="error">{routeError}</Alert> : null}
        {interveneError ? <Alert severity="error">{interveneError}</Alert> : null}

        {actions.length === 0 && !isSupervisor ? (
          <Typography variant="body2" color="text.secondary">
            No workflow actions available from current state.
          </Typography>
        ) : (
          <Stack spacing={2}>
            {actions.map((action) => {
              if (action.type === 'advance') {
                return (
                  <div key={action.type}>
                    <Button
                      variant="contained"
                      startIcon={<PlayArrowRounded />}
                      disabled={isBusy}
                      onClick={handleAdvance}
                    >
                      {action.label}
                    </Button>
                  </div>
                );
              }

              return (
                <Stack key={action.type} spacing={1.5}>
                  {!showRationaleInput ? (
                    <div>
                      <Button
                        variant="outlined"
                        startIcon={<ArrowForwardRounded />}
                        disabled={isBusy}
                        onClick={() => setShowRationaleInput(true)}
                      >
                        {action.label}
                      </Button>
                    </div>
                  ) : null}

                  <Collapse in={showRationaleInput}>
                    <Stack spacing={1.5}>
                      <TextField
                        label="Approval rationale"
                        value={rationale}
                        onChange={(e) => setRationale(e.target.value)}
                        multiline
                        minRows={3}
                        fullWidth
                        helperText="Explain why this claim requires payment approval (10–500 characters)."
                        slotProps={{ htmlInput: { 'aria-label': 'Approval rationale' } }}
                      />
                      <Stack direction="row" spacing={1}>
                        <Button
                          variant="contained"
                          disabled={isBusy || rationale.trim().length < 10}
                          onClick={handleConfirmRouting}
                          aria-label="Confirm Approval Routing"
                        >
                          Confirm Approval Routing
                        </Button>
                        <Button
                          variant="text"
                          disabled={isBusy}
                          onClick={() => {
                            setShowRationaleInput(false);
                            setRationale('');
                          }}
                        >
                          Cancel
                        </Button>
                      </Stack>
                    </Stack>
                  </Collapse>
                </Stack>
              );
            })}

            {isSupervisor ? (
              <Stack spacing={1.5} sx={{ mt: 1, pt: 2, borderTop: 1, borderColor: 'divider' }}>
                <Typography variant="overline" color="warning.main" sx={{ fontWeight: 'bold' }}>
                  Supervisor Intervention
                </Typography>
                
                {!showInterventionInput ? (
                  <div>
                    <Button
                      variant="outlined"
                      color="warning"
                      startIcon={<SupervisorAccountRounded />}
                      disabled={isBusy || !canIntervene}
                      onClick={() => setShowInterventionInput(true)}
                    >
                      Intervene & Reassign
                    </Button>
                    {!canIntervene && (
                      <Typography variant="caption" display="block" color="text.secondary" sx={{ mt: 0.5 }}>
                        Requires blocker &gt; 48h or amount &gt; €10,000.
                      </Typography>
                    )}
                  </div>
                ) : null}

                <Collapse in={showInterventionInput}>
                  <Stack spacing={2}>
                    <Typography variant="body2">
                      Manually reassign the claim and force a workflow transition.
                    </Typography>
                    <TextField
                      label="Target Adjuster ID"
                      value={targetOwnerId}
                      onChange={(e) => setTargetOwnerId(e.target.value)}
                      fullWidth
                      size="small"
                      placeholder="e.g. adjuster@claimmanager.local"
                      slotProps={{ htmlInput: { 'aria-label': 'Target Adjuster ID' } }}
                    />
                    <TextField
                      select
                      label="Target Workflow Status"
                      value={targetStatus}
                      onChange={(e) => setTargetStatus(e.target.value)}
                      fullWidth
                      size="small"
                      slotProps={{ htmlInput: { 'aria-label': 'Target Workflow Status' } }}
                    >
                      {WORKFLOW_STATES.map((status) => (
                        <MenuItem key={status} value={status}>
                          {status.toUpperCase()}
                        </MenuItem>
                      ))}
                    </TextField>
                    <Stack direction="row" spacing={1}>
                      <Button
                        variant="contained"
                        color="warning"
                        disabled={isBusy || !targetOwnerId || !targetStatus}
                        onClick={handleConfirmIntervention}
                      >
                        Execute Intervention
                      </Button>
                      <Button
                        variant="text"
                        disabled={isBusy}
                        onClick={() => {
                          setShowInterventionInput(false);
                          setTargetOwnerId('');
                          setTargetStatus('');
                        }}
                      >
                        Cancel
                      </Button>
                    </Stack>
                  </Stack>
                </Collapse>
              </Stack>
            ) : null}
          </Stack>
        )}
      </Stack>
    </Paper>
  );
}
