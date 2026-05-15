import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, Button, CircularProgress, Paper, Stack, Typography } from '@mui/material';
import { useEffect, useState } from 'react';
import { Link as RouterLink, Navigate, useLocation, useParams } from 'react-router-dom';
import { addClaimNote, advanceClaimWorkflow, getClaim, reconcileClaimState, routeClaimForApproval, syncClaimDocumentData, syncClaimPaymentData, syncClaimPolicyData, updateClaim, uploadClaimDocument } from '../api/claimsApi';
import { sendClaimNotification, retryClaimNotification } from '../../claimant-communication/api';
import { ClaimCommunicationsPanel } from '../../claimant-communication/ClaimCommunicationsPanel';
import type { SendNotificationRequest } from '../../claimant-communication/types';
import { ClaimForm } from '../components/ClaimForm';
import { ClaimDocumentsPanel } from '../components/ClaimDocumentsPanel';
import { ClaimNotesPanel } from '../components/ClaimNotesPanel';
import { ClaimStateSummaryPanel } from '../components/ClaimStateSummaryPanel';
import { WorkflowActionsPanel } from '../components/WorkflowActionsPanel';
import { WorkflowTimeline } from '../components/WorkflowTimeline';
import { claimToFormValues } from '../types/Claim';
import type { ClaimDocument, ClaimFormValues, ClaimNote } from '../types/Claim';
import { ApiError } from '../../../shared/api/client';
import { getProblemFieldErrors } from '../../../shared/api/problemDetails';
import { useClaimFormStore } from '../state/claimFormStore';
import { PageSurface } from '../../../shared/ui/PageSurface';

type DashboardOrigin = {
  label: string;
  backTo?: string;
};

export function EditClaimPage() {
  const { claimId } = useParams<{ claimId: string }>();
  const location = useLocation();
  const dashboardOrigin = (location.state as { dashboardOrigin?: DashboardOrigin } | null)?.dashboardOrigin;
  const dashboardBackLink = dashboardOrigin?.backTo ?? '/';
  const queryClient = useQueryClient();
  const setErrors = useClaimFormStore((state) => state.setErrors);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [noteError, setNoteError] = useState<string | null>(null);
  const [documentError, setDocumentError] = useState<string | null>(null);
  const [pendingNotes, setPendingNotes] = useState<ClaimNote[]>([]);
  const [pendingDocuments, setPendingDocuments] = useState<ClaimDocument[]>([]);
  const [policySyncError, setPolicySyncError] = useState<string | null>(null);
  const [paymentSyncError, setPaymentSyncError] = useState<string | null>(null);
  const [documentSyncError, setDocumentSyncError] = useState<string | null>(null);
  const [reconciliationError, setReconciliationError] = useState<string | null>(null);
  const [advanceError, setAdvanceError] = useState<string | null>(null);
  const [routeError, setRouteError] = useState<string | null>(null);
  const [sendNotificationError, setSendNotificationError] = useState<string | null>(null);
  const [retryNotificationError, setRetryNotificationError] = useState<string | null>(null);
  const [retryingNotificationId, setRetryingNotificationId] = useState<string | null>(null);

  const claimQuery = useQuery({
    queryKey: ['claims', claimId],
    queryFn: () => getClaim(claimId!),
    enabled: Boolean(claimId),
  });

  useEffect(() => {
    if (!claimQuery.data) {
      return;
    }

    setPendingNotes((currentNotes) => currentNotes.filter((note) => !claimQuery.data.notes.some((serverNote) => serverNote.id === note.id)));
    setPendingDocuments((currentDocuments) => currentDocuments.filter((document) => !claimQuery.data.documents.some((serverDocument) => serverDocument.id === document.id)));
  }, [claimQuery.data]);

  const updateClaimMutation = useMutation({
    mutationFn: (values: ClaimFormValues) => updateClaim(claimId!, values, claimQuery.data!.rowVersion),
    onSuccess: async () => {
      setSubmitError(null);
      setSuccessMessage('Claim file updated and audit history refreshed.');
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setErrors(getProblemFieldErrors(error.problemDetails));
        setSubmitError(error.message);
        return;
      }

      setSubmitError('Unable to save the claim file right now.');
    },
  });

  const addNoteMutation = useMutation({
    mutationFn: (content: string) => addClaimNote(claimId!, content),
    onSuccess: async (note) => {
      setNoteError(null);
      setPendingNotes((currentNotes) => [note, ...currentNotes.filter((currentNote) => currentNote.id !== note.id)]);
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setNoteError(getProblemFieldErrors(error.problemDetails).content?.[0] ?? error.message);
        return;
      }

      setNoteError('Unable to add the note right now.');
    },
  });

  const uploadDocumentMutation = useMutation({
    mutationFn: (file: File) => uploadClaimDocument(claimId!, file),
    onSuccess: async (document) => {
      setDocumentError(null);
      setPendingDocuments((currentDocuments) => [document, ...currentDocuments.filter((currentDocument) => currentDocument.id !== document.id)]);
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setDocumentError(getProblemFieldErrors(error.problemDetails).file?.[0] ?? error.message);
        return;
      }

      setDocumentError('Unable to upload the document right now.');
    },
  });

  const advanceWorkflowMutation = useMutation({
    mutationFn: (rowVersion: string) => advanceClaimWorkflow(claimId!, rowVersion),
    onSuccess: async () => {
      setAdvanceError(null);
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setAdvanceError(error.message);
        return;
      }
      setAdvanceError('Unable to advance the claim workflow right now.');
    },
  });

  const routeForApprovalMutation = useMutation({
    mutationFn: ({ rationale, rowVersion }: { rationale: string; rowVersion: string }) =>
      routeClaimForApproval(claimId!, rationale, rowVersion),
    onSuccess: async () => {
      setRouteError(null);
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setRouteError(error.message);
        return;
      }
      setRouteError('Unable to route the claim for approval right now.');
    },
  });

  const syncPolicyMutation = useMutation({
    mutationFn: () => syncClaimPolicyData(claimId!),
    onSuccess: async () => {
      setPolicySyncError(null);
      setSuccessMessage('Policy data synchronized and claim context refreshed.');
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setPolicySyncError(error.message);
        return;
      }

      setPolicySyncError('Unable to synchronize policy data right now.');
    },
  });

  const syncPaymentMutation = useMutation({
    mutationFn: () => syncClaimPaymentData(claimId!),
    onSuccess: async () => {
      setPaymentSyncError(null);
      setSuccessMessage('Payment data synchronized and claim context refreshed.');
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setPaymentSyncError(error.message);
        return;
      }

      setPaymentSyncError('Unable to synchronize payment data right now.');
    },
  });

  const sendNotificationMutation = useMutation({
    mutationFn: (request: SendNotificationRequest) =>
      sendClaimNotification(claimId!, request),
    onSuccess: async () => {
      setSendNotificationError(null);
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setSendNotificationError(error.message);
        return;
      }
      setSendNotificationError('Unable to send the notification right now.');
    },
  });

  const retryNotificationMutation = useMutation({
    mutationFn: (notificationId: string) => retryClaimNotification(claimId!, notificationId),
    onSuccess: async () => {
      setRetryNotificationError(null);
      setRetryingNotificationId(null);
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      setRetryingNotificationId(null);
      if (error instanceof ApiError) {
        setRetryNotificationError(error.message);
        return;
      }
      setRetryNotificationError('Unable to retry the notification right now.');
    },
  });

  const syncDocumentsMutation = useMutation({
    mutationFn: () => syncClaimDocumentData(claimId!),
    onSuccess: async () => {
      setDocumentSyncError(null);
      setSuccessMessage('Document repository synchronized and claim context refreshed.');
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setDocumentSyncError(error.message);
        return;
      }

      setDocumentSyncError('Unable to synchronize document repository right now.');
    },
  });

  const reconcileClaimMutation = useMutation({
    mutationFn: () => reconcileClaimState(claimId!),
    onSuccess: async () => {
      setReconciliationError(null);
      setSuccessMessage('Claim reconciliation completed and claim context refreshed.');
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setReconciliationError(error.message);
        return;
      }

      setReconciliationError('Unable to reconcile claim state right now.');
    },
  });

  if (!claimId) {
    return <Navigate to="/claims" replace />;
  }

  if (claimQuery.isLoading || !claimQuery.data) {
    return (
      <PageSurface centered maxWidth={920}>
        <Paper sx={{ p: 4 }}>
          <Stack spacing={2} sx={{ alignItems: 'center' }}>
            <CircularProgress aria-label="Loading claim file" />
            <Typography variant="h3">Loading claim file...</Typography>
          </Stack>
        </Paper>
      </PageSurface>
    );
  }

  const visibleNotes = [...pendingNotes, ...claimQuery.data.notes.filter((note) => !pendingNotes.some((pendingNote) => pendingNote.id === note.id))];
  const visibleDocuments = [
    ...pendingDocuments,
    ...claimQuery.data.documents.filter((document) => !pendingDocuments.some((pendingDocument) => pendingDocument.id === document.id)),
  ];

  return (
    <PageSurface>
      <Stack spacing={3}>
        {dashboardOrigin ? (
          <Alert
            severity="info"
            action={
              <Button component={RouterLink} to={dashboardBackLink} size="small" color="inherit">
                Back to dashboard
              </Button>
            }
          >
            Opened from Supervisor dashboard: {dashboardOrigin.label}
          </Alert>
        ) : null}

        {successMessage ? <Alert severity="success">{successMessage}</Alert> : null}

        <ClaimStateSummaryPanel claim={claimQuery.data} />

        <Paper
          component="section"
          variant="outlined"
          sx={{ p: { xs: 3, md: 4 }, borderColor: 'divider', bgcolor: 'background.default' }}
          aria-label="Claim recovery"
        >
          <Stack spacing={2}>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
              <div>
                <Typography variant="overline" color="text.secondary">
                  Recovery
                </Typography>
                <Typography variant="h3">Claim Reconciliation</Typography>
                <Typography variant="body2" color="text.secondary">
                  Rerun the current policy, payment, and document sync paths in one coordinated recovery attempt.
                </Typography>
              </div>

              <Button
                variant="contained"
                onClick={() => reconcileClaimMutation.mutate()}
                disabled={reconcileClaimMutation.isPending}
              >
                Run Reconciliation
              </Button>
            </Stack>

            {reconciliationError ? <Alert severity="error">{reconciliationError}</Alert> : null}

            {claimQuery.data.reconciliation ? (
              <Alert severity={claimQuery.data.reconciliation.isFullyReconciled ? 'success' : 'warning'}>
                <Stack spacing={0.75}>
                  <span>{claimQuery.data.reconciliation.summary}</span>
                  <Typography variant="caption" color="inherit">
                    Last attempted {new Date(claimQuery.data.reconciliation.attemptedAtUtc).toLocaleString()}
                  </Typography>
                  <Typography variant="body2" color="inherit">
                    Retried: {formatDependencyList(claimQuery.data.reconciliation.retriedDependencies)}
                  </Typography>
                  <Typography variant="body2" color="inherit">
                    Recovered: {formatDependencyList(claimQuery.data.reconciliation.recoveredDependencies, 'None in the last attempt')}
                  </Typography>
                  <Typography variant="body2" color="inherit">
                    Unresolved: {formatDependencyList(claimQuery.data.reconciliation.unresolvedDependencies, 'None')}
                  </Typography>
                </Stack>
              </Alert>
            ) : (
              <Typography variant="body2" color="text.secondary">
                No claim reconciliation attempt has been recorded yet.
              </Typography>
            )}
          </Stack>
        </Paper>

        <WorkflowActionsPanel
          claim={claimQuery.data}
          onAdvance={async (rowVersion) => {
            await advanceWorkflowMutation.mutateAsync(rowVersion);
          }}
          onRouteForApproval={async (rationale, rowVersion) => {
            await routeForApprovalMutation.mutateAsync({ rationale, rowVersion });
          }}
          advancing={advanceWorkflowMutation.isPending}
          routing={routeForApprovalMutation.isPending}
          advanceError={advanceError}
          routeError={routeError}
        />

        <Stack
          sx={{
            display: 'grid',
            gridTemplateColumns: { xs: '1fr', xl: '1.05fr 0.95fr' },
            gap: 3,
          }}
        >
          <Stack spacing={3}>
            <ClaimForm
              mode="edit"
              initialValues={claimToFormValues(claimQuery.data)}
              submitLabel="Save claim updates"
              busy={updateClaimMutation.isPending}
              submitError={submitError}
              onSubmit={async (values) => {
                setSuccessMessage(null);
                await updateClaimMutation.mutateAsync(values);
              }}
            />

            <Paper
              component="section"
              variant="outlined"
              sx={{ p: { xs: 3, md: 4 }, borderColor: 'divider', bgcolor: 'background.default' }}
              aria-label="Policy System Data"
            >
              <Stack spacing={2}>
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
                  <div>
                    <Typography variant="overline" color="text.secondary">
                      External policy source
                    </Typography>
                    <Typography variant="h3">Policy System Data</Typography>
                  </div>

                  <Button
                    variant="outlined"
                    onClick={() => syncPolicyMutation.mutate()}
                    disabled={syncPolicyMutation.isPending}
                  >
                    Sync Policy Data
                  </Button>
                </Stack>

                {policySyncError ? <Alert severity="error">{policySyncError}</Alert> : null}

                <Stack spacing={1}>
                  <Typography variant="body2"><strong>Policy holder:</strong> {claimQuery.data.policyHolder ?? 'Not yet synchronized'}</Typography>
                  <Typography variant="body2"><strong>Coverage type:</strong> {claimQuery.data.coverageType ?? '—'}</Typography>
                  <Typography variant="body2"><strong>Effective date:</strong> {claimQuery.data.policyEffectiveDate ?? '—'}</Typography>
                  <Typography variant="body2"><strong>Expiration date:</strong> {claimQuery.data.policyExpirationDate ?? '—'}</Typography>
                  <Typography variant="body2">
                    <strong>Last synced:</strong> {claimQuery.data.policySyncedAtUtc ? new Date(claimQuery.data.policySyncedAtUtc).toLocaleString() : 'Never'}
                  </Typography>
                </Stack>
              </Stack>
            </Paper>

            <Paper
              component="section"
              variant="outlined"
              sx={{ p: { xs: 3, md: 4 }, borderColor: 'divider', bgcolor: 'background.default' }}
              aria-label="Payment System Data"
            >
              <Stack spacing={2}>
                <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
                  <div>
                    <Typography variant="overline" color="text.secondary">
                      External payment source
                    </Typography>
                    <Typography variant="h3">Payment System Data</Typography>
                  </div>

                  <Button
                    variant="outlined"
                    onClick={() => syncPaymentMutation.mutate()}
                    disabled={syncPaymentMutation.isPending}
                  >
                    Sync Payment Data
                  </Button>
                </Stack>

                {paymentSyncError ? <Alert severity="error">{paymentSyncError}</Alert> : null}

                <Stack spacing={1}>
                  <Typography variant="body2"><strong>Payment reference:</strong> {claimQuery.data.paymentReference ?? 'Not yet synchronized'}</Typography>
                  <Typography variant="body2"><strong>Status:</strong> {claimQuery.data.paymentStatus ?? '—'}</Typography>
                  <Typography variant="body2">
                    <strong>Amount:</strong> {claimQuery.data.paymentAmount != null ? `${claimQuery.data.paymentAmount.toFixed(2)} ${claimQuery.data.paymentCurrency ?? ''}`.trim() : '—'}
                  </Typography>
                  <Typography variant="body2"><strong>Settled at:</strong> {claimQuery.data.paymentSettledAt ? new Date(claimQuery.data.paymentSettledAt).toLocaleString() : '—'}</Typography>
                  <Typography variant="body2">
                    <strong>Last synced:</strong> {claimQuery.data.paymentSyncedAtUtc ? new Date(claimQuery.data.paymentSyncedAtUtc).toLocaleString() : 'Never'}
                  </Typography>
                </Stack>
              </Stack>
            </Paper>
          </Stack>

          <Stack spacing={3}>
            <ClaimNotesPanel
              notes={visibleNotes}
              busy={addNoteMutation.isPending}
              errorMessage={noteError}
              onSubmit={async (content) => {
                await addNoteMutation.mutateAsync(content);
              }}
            />

            <ClaimDocumentsPanel
              documents={visibleDocuments}
              busy={uploadDocumentMutation.isPending}
              errorMessage={documentError}
              onSync={async () => {
                await syncDocumentsMutation.mutateAsync();
              }}
              syncing={syncDocumentsMutation.isPending}
              syncError={documentSyncError}
              documentSyncedAtUtc={claimQuery.data.documentSyncedAtUtc}
              onSubmit={async (file) => {
                return uploadDocumentMutation.mutateAsync(file);
              }}
            />

            <WorkflowTimeline auditHistory={claimQuery.data.auditHistory} />

            <ClaimCommunicationsPanel
              claimId={claimId!}
              communications={claimQuery.data.communications ?? []}
              onSend={async (request) => {
                await sendNotificationMutation.mutateAsync(request);
              }}
              onRetry={async (notificationId) => {
                setRetryingNotificationId(notificationId);
                await retryNotificationMutation.mutateAsync(notificationId);
              }}
              sending={sendNotificationMutation.isPending}
              retryingId={retryingNotificationId}
              sendError={sendNotificationError}
              retryError={retryNotificationError}
            />
          </Stack>
        </Stack>
      </Stack>
    </PageSurface>
  );
}

async function invalidateClaimQueries(queryClient: ReturnType<typeof useQueryClient>, claimId: string | undefined) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: ['claims'] }),
    queryClient.refetchQueries({ queryKey: ['claims', claimId], exact: true }),
    queryClient.invalidateQueries({ queryKey: ['workspace'] }),
  ]);
}

function formatDependencyList(dependencies: string[], emptyLabel = 'None'): string {
  if (dependencies.length === 0) {
    return emptyLabel;
  }

  return dependencies
    .map((dependency) => {
      if (dependency === 'policy') return 'Policy';
      if (dependency === 'payment') return 'Payment';
      if (dependency === 'documents') return 'Documents';
      return dependency;
    })
    .join(', ');
}

