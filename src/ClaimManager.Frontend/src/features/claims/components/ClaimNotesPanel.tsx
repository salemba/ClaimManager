import NoteAddRounded from '@mui/icons-material/NoteAddRounded';
import { Button, List, ListItem, Paper, Stack, TextField, Typography } from '@mui/material';
import { useState } from 'react';
import { ValidationError } from '../../../shared/ui/ValidationError';
import type { ClaimNote } from '../types/Claim';

interface ClaimNotesPanelProps {
  notes: ClaimNote[];
  busy?: boolean;
  errorMessage?: string | null;
  onSubmit: (content: string) => Promise<void>;
}

export function ClaimNotesPanel({ notes, busy = false, errorMessage, onSubmit }: ClaimNotesPanelProps) {
  const [content, setContent] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const normalizedContent = content.trim();
    if (!normalizedContent) {
      setLocalError('Note content is required.');
      return;
    }

    setLocalError(null);
    try {
      await onSubmit(normalizedContent);
      setContent('');
    } catch {
    }
  };

  return (
    <Paper component="section" sx={{ p: { xs: 3, md: 4 } }} aria-label="Claim notes">
      <Stack spacing={2.5}>
        <div>
          <Typography variant="overline" color="text.secondary">
            Claim context
          </Typography>
          <Typography variant="h2">Notes</Typography>
          <Typography color="text.secondary">
            Capture operational context directly on the claim so the next adjuster sees the same working narrative.
          </Typography>
        </div>

        <ValidationError message={localError ?? errorMessage ?? undefined} />

        <Stack component="form" spacing={2} onSubmit={handleSubmit}>
          <TextField
            label="Claim note"
            value={content}
            onChange={(event) => setContent(event.target.value)}
            multiline
            minRows={3}
            fullWidth
          />
          <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
            <Typography variant="body2" color="text.secondary">
              {notes.length} note{notes.length === 1 ? '' : 's'} on this claim.
            </Typography>
            <Button type="submit" variant="contained" startIcon={<NoteAddRounded />} disabled={busy}>
              Add note
            </Button>
          </Stack>
        </Stack>

        {notes.length === 0 ? (
          <Typography color="text.secondary">No notes yet. Add the first operational update for this claim.</Typography>
        ) : (
          <List disablePadding sx={{ display: 'grid', gap: 1.5 }}>
            {notes.map((note) => (
              <ListItem
                key={note.id}
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
                  <Typography variant="body2" color="text.secondary">
                    {new Date(note.createdAtUtc).toLocaleString()} · {note.createdByUserId}
                  </Typography>
                  <Typography>{note.content}</Typography>
                </Stack>
              </ListItem>
            ))}
          </List>
        )}
      </Stack>
    </Paper>
  );
}