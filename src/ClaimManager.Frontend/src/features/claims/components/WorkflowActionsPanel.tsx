import ArrowForwardRounded from '@mui/icons-material/ArrowForwardRounded';
import CheckCircleRounded from '@mui/icons-material/CheckCircleRounded';
import PlayArrowRounded from '@mui/icons-material/PlayArrowRounded';
import { Alert, Button, Collapse, Paper, Stack, TextField, Typography } from '@mui/material';
import { useState } from 'react';
import type { Claim } from '../types/Claim';

interface WorkflowAction {
  label: string;
  type: 'advance' | 'route-for-approval';
}

const STATUS_ACTIONS: Record<string, WorkflowAction[]> = {
  new: [{ label: 'Begin Review', type: 'advance' }],
  open: [
    { label: 'Submit for Review', type: 'advance' },
    { label: 'Route for Payment Approval', type: 'route-for-approval' },
  ],
  'in-review': [{ label: 'Route for Payment Approval', type: 'route-for-approval' }],
  approved: [{ label: 'Close Claim', type: 'advance' }],
};

interface WorkflowActionsPanelProps {
  claim: Claim;
  onAdvance: () => Promise<void>;
  onRouteForApproval: (rationale: string) => Promise<void>;
  advancing?: boolean;
  routing?: boolean;
  advanceError?: string | null;
  routeError?: string | null;
}

export function WorkflowActionsPanel({
  claim,
  onAdvance,
  onRouteForApproval,
  advancing = false,
  routing = false,
  advanceError,
  routeError,
}: WorkflowActionsPanelProps) {
  const [showRationaleInput, setShowRationaleInput] = useState(false);
  const [rationale, setRationale] = useState('');
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const isBusy = advancing || routing;

  const actions = STATUS_ACTIONS[claim.status] ?? [];

  const handleAdvance = async () => {
    const previousStatus = claim.status;
    setSuccessMessage(null);
    try {
      await onAdvance();
      const nextStatus = getNextStatus(previousStatus);
      const nextAction = getNextExpectedAction(previousStatus);
      const msg = nextAction
        ? `Claim advanced from ${previousStatus} to ${nextStatus}. Next: ${nextAction}.`
        : `Claim advanced from ${previousStatus} to ${nextStatus}.`;
      setSuccessMessage(msg);
    } catch {
      // error handled by parent via advanceError prop
    }
  };

  const handleConfirmRouting = async () => {
    const previousStatus = claim.status;
    const normalizedRationale = rationale.trim();
    setSuccessMessage(null);
    try {
      await onRouteForApproval(normalizedRationale);
      setShowRationaleInput(false);
      setRationale('');
      setSuccessMessage(`Claim advanced from ${previousStatus} to pending. Next: Awaiting payment approval decision.`);
    } catch {
      // error handled by parent via routeError prop
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

        {actions.length === 0 ? (
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
                        inputProps={{ 'aria-label': 'Approval rationale' }}
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
          </Stack>
        )}
      </Stack>
    </Paper>
  );
}

function getNextStatus(currentStatus: string): string {
  const transitions: Record<string, string> = {
    new: 'open',
    open: 'in-review',
    approved: 'closed',
  };
  return transitions[currentStatus] ?? currentStatus;
}

function getNextExpectedAction(currentStatus: string): string | null {
  const actions: Record<string, string | null> = {
    new: 'Investigate loss details',
    open: 'Review and document findings',
    approved: null,
  };
  return actions[currentStatus] ?? null;
}
