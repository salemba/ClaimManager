import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, CircularProgress, Paper, Stack, Typography } from '@mui/material';
import { useEffect, useState } from 'react';
import { Navigate, useParams } from 'react-router-dom';
import { addClaimNote, advanceClaimWorkflow, getClaim, routeClaimForApproval, updateClaim, uploadClaimDocument } from '../api/claimsApi';
import { ClaimForm } from '../components/ClaimForm';
import { ClaimDocumentsPanel } from '../components/ClaimDocumentsPanel';
import { ClaimNotesPanel } from '../components/ClaimNotesPanel';
import { ClaimStateSummaryPanel } from '../components/ClaimStateSummaryPanel';
import { WorkflowActionsPanel } from '../components/WorkflowActionsPanel';
import { WorkflowTimeline } from '../components/WorkflowTimeline';
import { claimToFormValues } from '../types/Claim';
import type { Claim, ClaimDocument, ClaimFormValues, ClaimNote } from '../types/Claim';
import { ApiError } from '../../../shared/api/client';
import { getProblemFieldErrors } from '../../../shared/api/problemDetails';
import { useClaimFormStore } from '../state/claimFormStore';
import { PageSurface } from '../../../shared/ui/PageSurface';

export function EditClaimPage() {
  const { claimId } = useParams<{ claimId: string }>();
  const queryClient = useQueryClient();
  const setErrors = useClaimFormStore((state) => state.setErrors);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const [noteError, setNoteError] = useState<string | null>(null);
  const [documentError, setDocumentError] = useState<string | null>(null);
  const [pendingNotes, setPendingNotes] = useState<ClaimNote[]>([]);
  const [pendingDocuments, setPendingDocuments] = useState<ClaimDocument[]>([]);

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
    mutationFn: (values: ClaimFormValues) => updateClaim(claimId!, values),
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

  const [advanceError, setAdvanceError] = useState<string | null>(null);
  const [routeError, setRouteError] = useState<string | null>(null);

  const advanceClaimMutation = useMutation({
    mutationFn: () => advanceClaimWorkflow(claimId!),
    onSuccess: async () => {
      setAdvanceError(null);
      await invalidateClaimQueries(queryClient, claimId);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        setAdvanceError(error.message);
        return;
      }
      setAdvanceError('Unable to advance the workflow right now.');
    },
  });

  const routeForApprovalMutation = useMutation({
    mutationFn: (rationale: string) => routeClaimForApproval(claimId!, rationale),
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
        {successMessage ? <Alert severity="success">{successMessage}</Alert> : null}

        <ClaimStateSummaryPanel claim={claimQuery.data} />

        <WorkflowActionsPanel
          claim={claimQuery.data}
          onAdvance={() => advanceClaimMutation.mutateAsync()}
          onRouteForApproval={(rationale) => routeForApprovalMutation.mutateAsync(rationale)}
          advancing={advanceClaimMutation.isPending}
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
              onSubmit={async (file) => {
                return uploadDocumentMutation.mutateAsync(file);
              }}
            />

            <WorkflowTimeline auditHistory={claimQuery.data.auditHistory} />
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

async function invalidateClaimShellQueries(queryClient: ReturnType<typeof useQueryClient>) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: ['claims'] }),
    queryClient.invalidateQueries({ queryKey: ['workspace'] }),
  ]);
}
