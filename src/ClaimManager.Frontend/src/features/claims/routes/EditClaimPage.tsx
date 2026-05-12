import HistoryRounded from '@mui/icons-material/HistoryRounded';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Alert, CircularProgress, List, ListItem, Paper, Stack, Typography } from '@mui/material';
import { useEffect, useState } from 'react';
import { Navigate, useParams } from 'react-router-dom';
import { addClaimNote, getClaim, updateClaim, uploadClaimDocument } from '../api/claimsApi';
import { ClaimForm } from '../components/ClaimForm';
import { ClaimDocumentsPanel } from '../components/ClaimDocumentsPanel';
import { ClaimNotesPanel } from '../components/ClaimNotesPanel';
import { claimToFormValues } from '../types/Claim';
import type { Claim, ClaimDocument, ClaimFormValues, ClaimNote } from '../types/Claim';
import { ApiError } from '../../../shared/api/client';
import { getProblemFieldErrors } from '../../../shared/api/problemDetails';
import { useClaimFormStore } from '../state/claimFormStore';
import { PageSurface } from '../../../shared/ui/PageSurface';
import { StatusBadge } from '../../../shared/ui/StatusBadge';

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

            <Paper component="section" sx={{ p: { xs: 3, md: 4 } }} aria-label="Claim audit history">
              <Stack spacing={2.5}>
                <div>
                  <Typography variant="overline" color="text.secondary">
                    Auditability
                  </Typography>
                  <Typography variant="h2">Material change history</Typography>
                </div>
                <StatusBadge tone="info" label={`${claimQuery.data.auditHistory.length} audit entries`} icon={<HistoryRounded fontSize="small" />} />
                <Typography color="text.secondary">
                  Every create, note, document, and update action records who changed the claim, when it changed, and what was materially updated.
                </Typography>
                <List disablePadding sx={{ display: 'grid', gap: 1.5 }}>
                  {claimQuery.data.auditHistory.map((entry) => (
                    <ListItem
                      key={`${entry.action}-${entry.performedAtUtc}`}
                      disablePadding
                      sx={{
                        display: 'block',
                        p: 2,
                        borderRadius: 3,
                        bgcolor: 'background.default',
                        border: (theme) => `1px solid ${theme.palette.divider}`,
                      }}
                    >
                      <Stack spacing={0.75}>
                        <Typography variant="subtitle1">{entry.action}</Typography>
                        <Typography variant="body2" color="text.secondary">
                          {new Date(entry.performedAtUtc).toLocaleString()} · {entry.performedByUserId}
                        </Typography>
                        <Typography>{entry.summary}</Typography>
                      </Stack>
                    </ListItem>
                  ))}
                </List>
              </Stack>
            </Paper>
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
