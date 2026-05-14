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

const ACTION_DEFINITIONS: Record<string, Omit<WorkflowAction, 'type'>> = {
  advance: { label: 'Advance Workflow' },
  'route-for-approval': { label: 'Route for Payment Approval' },
};

const STATUS_LABELS: Record<string, string> = {
  new: 'Begin Review',
  open: 'Submit for Review',
  approved: 'Close Claim',
};

interface WorkflowActionsPanelProps {
  claim: Claim;
  onAdvance: (rowVersion: string) => Promise<void>;
  onRouteForApproval: (rationale: string, rowVersion: string) => Promise<void>;
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
          </Stack>
        )}
      </Stack>
    </Paper>
  );
}
