import AddCircleOutlineRounded from '@mui/icons-material/AddCircleOutlineRounded';
import EditRounded from '@mui/icons-material/EditRounded';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, CircularProgress, List, ListItem, ListItemButton, ListItemText, Paper, Stack, Typography } from '@mui/material';
import { useState } from 'react';
import { Link as RouterLink } from 'react-router-dom';
import { createClaim, getClaims } from '../api/claimsApi';
import { ClaimForm } from '../components/ClaimForm';
import { emptyClaimFormValues } from '../types/Claim';
import type { ClaimFormValues } from '../types/Claim';
import { ApiError } from '../../../shared/api/client';
import { getProblemFieldErrors } from '../../../shared/api/problemDetails';
import { useClaimFormStore } from '../state/claimFormStore';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { PageSurface } from '../../../shared/ui/PageSurface';

export function CreateClaimPage() {
  const queryClient = useQueryClient();
  const setErrors = useClaimFormStore((state) => state.setErrors);
  const reset = useClaimFormStore((state) => state.reset);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);

  const claimsQuery = useQuery({
    queryKey: ['claims'],
    queryFn: getClaims,
  });

  const createClaimMutation = useMutation({
    mutationFn: createClaim,
    onSuccess: async (claim) => {
      setSubmitError(null);
      setSuccessMessage(`Claim ${claim.claimNumber} created and added to the working queue.`);
      reset(emptyClaimFormValues);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['claims'] }),
        queryClient.invalidateQueries({ queryKey: ['workspace'] }),
      ]);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setErrors(getProblemFieldErrors(error.problemDetails));
        setSubmitError(error.message);
        return;
      }

      setSubmitError('Unable to create the claim file right now.');
    },
  });

  const handleSubmit = async (values: ClaimFormValues) => {
    setSuccessMessage(null);
    await createClaimMutation.mutateAsync(values);
  };

  return (
    <PageSurface>
      <Stack spacing={3}>
        {successMessage ? <Alert severity="success">{successMessage}</Alert> : null}

        <Stack
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', xl: '1.05fr 0.95fr' },
            gap: 3,
          }}
        >
          <Paper component="section" sx={{ p: { xs: 3, md: 4 } }} aria-label="Claims work queue">
            <Stack spacing={2.5}>
              <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
                <div>
                  <Typography variant="overline" color="text.secondary">
                    Working queue
                  </Typography>
                  <Typography variant="h2">Claims ready for handling</Typography>
                </div>
                <StatusBadge tone="info" label={`${claimsQuery.data?.length ?? 0} claims in queue`} icon={<AddCircleOutlineRounded fontSize="small" />} />
              </Stack>

              <Typography color="text.secondary" sx={{ maxWidth: 720 }}>
                New claim files land here immediately after intake so adjusters can move from creation into maintenance without leaving the workbench.
              </Typography>

              {claimsQuery.isLoading ? (
                <Stack spacing={1.5} sx={{ alignItems: 'center', py: 6 }}>
                  <CircularProgress aria-label="Loading claims queue" />
                  <Typography color="text.secondary">Loading current claims queue...</Typography>
                </Stack>
              ) : (
                <List disablePadding sx={{ display: 'grid', gap: 1.5 }}>
                  {(claimsQuery.data ?? []).map((claim) => (
                    <ListItem key={claim.id} disablePadding>
                      <ListItemButton
                        component={RouterLink}
                        to={`/claims/${claim.id}/edit`}
                        sx={{
                          p: 2,
                          borderRadius: 3,
                          border: (theme) => `1px solid ${theme.palette.divider}`,
                          alignItems: 'flex-start',
                        }}
                      >
                        <ListItemText
                          primary={`${claim.claimNumber} · ${claim.claimantName}`}
                          secondary={`Policy ${claim.policyNumber} · Loss ${new Date(claim.lossDateUtc).toLocaleDateString()} · ${claim.status}`}
                        />
                        <Button component="span" variant="text" startIcon={<EditRounded />}>
                          Edit
                        </Button>
                      </ListItemButton>
                    </ListItem>
                  ))}
                </List>
              )}
            </Stack>
          </Paper>

          <ClaimForm
            mode="create"
            initialValues={emptyClaimFormValues}
            submitLabel="Create claim file"
            busy={createClaimMutation.isPending}
            submitError={submitError}
            onSubmit={handleSubmit}
          />
        </Stack>
      </Stack>
    </PageSurface>
  );
}