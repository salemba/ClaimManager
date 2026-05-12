import UploadFileRounded from '@mui/icons-material/UploadFileRounded';
import { Button, List, ListItem, Paper, Stack, Typography } from '@mui/material';
import { useRef, useState } from 'react';
import { ValidationError } from '../../../shared/ui/ValidationError';
import type { ClaimDocument } from '../types/Claim';

interface ClaimDocumentsPanelProps {
  documents: ClaimDocument[];
  busy?: boolean;
  errorMessage?: string | null;
  onSubmit: (file: File) => Promise<ClaimDocument>;
}

export function ClaimDocumentsPanel({ documents, busy = false, errorMessage, onSubmit }: ClaimDocumentsPanelProps) {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [localError, setLocalError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement | null>(null);

  const handleUpload = async () => {
    if (!selectedFile) {
      setLocalError('Choose a document before uploading.');
      return;
    }

    setLocalError(null);
    try {
      await onSubmit(selectedFile);
      setSelectedFile(null);
      if (inputRef.current) {
        inputRef.current.value = '';
      }
    } catch (error) {
      setLocalError(error instanceof Error ? error.message : 'Unable to upload the document right now.');
    }
  };

  return (
    <Paper component="section" sx={{ p: { xs: 3, md: 4 } }} aria-label="Claim documents">
      <Stack spacing={2.5}>
        <div>
          <Typography variant="overline" color="text.secondary">
            Evidence handling
          </Typography>
          <Typography variant="h2">Documents</Typography>
          <Typography color="text.secondary">
            Upload supported claim evidence and keep trusted metadata available from the working claim file.
          </Typography>
        </div>

        <ValidationError message={localError ?? errorMessage ?? undefined} />

        <Stack spacing={1.5}>
          <label htmlFor="claim-document-upload">
            <Typography variant="subtitle1">Claim document</Typography>
          </label>
          <input
            ref={inputRef}
            id="claim-document-upload"
            aria-label="Claim document"
            type="file"
            onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)}
          />
          <Typography variant="body2" color="text.secondary">
            Supported file types: PDF, JPG, JPEG, PNG. Maximum size: 10 MB.
          </Typography>
          {selectedFile ? (
            <Typography variant="body2">Selected: {selectedFile.name}</Typography>
          ) : null}
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
            <Typography variant="body2" color="text.secondary">
              {documents.length} document{documents.length === 1 ? '' : 's'} linked to this claim.
            </Typography>
            <Button variant="contained" startIcon={<UploadFileRounded />} disabled={busy} onClick={handleUpload}>
              Upload document
            </Button>
          </Stack>
        </Stack>

        {documents.length === 0 ? (
          <Typography color="text.secondary">No documents uploaded yet. Supported evidence will appear here after a confirmed upload.</Typography>
        ) : (
          <List disablePadding sx={{ display: 'grid', gap: 1.5 }}>
            {documents.map((document) => (
              <ListItem
                key={document.id}
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
                  <Typography variant="subtitle1">{document.fileName}</Typography>
                  <Typography variant="body2" color="text.secondary">
                    {document.fileType.toUpperCase()} · {(document.fileSizeBytes / 1024).toFixed(1)} KB · {new Date(document.uploadedAtUtc).toLocaleString()}
                  </Typography>
                  <Typography variant="body2" color="text.secondary">
                    Uploaded by {document.uploadedByUserId}
                  </Typography>
                </Stack>
              </ListItem>
            ))}
          </List>
        )}
      </Stack>
    </Paper>
  );
}