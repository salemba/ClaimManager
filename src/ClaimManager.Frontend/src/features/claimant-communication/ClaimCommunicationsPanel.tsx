import {
  Alert,
  Button,
  Chip,
  Divider,
  FormControl,
  InputLabel,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useState } from 'react';
import type { ClaimCommunication, SendNotificationRequest } from './types';

interface ClaimCommunicationsPanelProps {
  claimId: string;
  communications: ClaimCommunication[];
  onSend: (request: SendNotificationRequest) => Promise<void>;
  onRetry: (notificationId: string) => Promise<void>;
  sending?: boolean;
  retryingId?: string | null;
  sendError?: string | null;
  retryError?: string | null;
}

function statusChipColor(status: string): 'success' | 'error' | 'default' {
  if (status === 'sent') return 'success';
  if (status === 'failed') return 'error';
  return 'default';
}

export function ClaimCommunicationsPanel({
  claimId: _claimId,
  communications,
  onSend,
  onRetry,
  sending = false,
  retryingId = null,
  sendError = null,
  retryError = null,
}: ClaimCommunicationsPanelProps) {
  const [communicationType, setCommunicationType] = useState('operational');
  const [recipient, setRecipient] = useState('');
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');

  async function handleSend() {
    await onSend({
      communicationType,
      channel: 'email',
      recipient: recipient.trim(),
      subject: subject.trim(),
      body: body.trim(),
    });
    setRecipient('');
    setSubject('');
    setBody('');
  }

  return (
    <Paper
      component="section"
      variant="outlined"
      sx={{ p: { xs: 3, md: 4 }, borderColor: 'divider', bgcolor: 'background.default' }}
      aria-label="Outbound Communications"
    >
      <Stack spacing={3}>
        <div>
          <Typography variant="overline" color="text.secondary">
            Messaging
          </Typography>
          <Typography variant="h3">Outbound Communications</Typography>
        </div>

        <Stack spacing={2}>
          <FormControl size="small" fullWidth>
            <InputLabel id="comm-type-label">Communication type</InputLabel>
            <Select
              labelId="comm-type-label"
              label="Communication type"
              value={communicationType}
              onChange={(e) => setCommunicationType(e.target.value)}
            >
              <MenuItem value="operational">Operational</MenuItem>
              <MenuItem value="claimant-safe">Claimant-safe</MenuItem>
            </Select>
          </FormControl>

          <TextField
            label="Recipient email"
            size="small"
            fullWidth
            value={recipient}
            onChange={(e) => setRecipient(e.target.value)}
          />

          <TextField
            label="Subject"
            size="small"
            fullWidth
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
          />

          <TextField
            label="Message body"
            size="small"
            fullWidth
            multiline
            rows={3}
            value={body}
            onChange={(e) => setBody(e.target.value)}
          />

          {sendError ? <Alert severity="error">{sendError}</Alert> : null}

          <Button
            variant="contained"
            onClick={handleSend}
            disabled={sending || !recipient || !subject || !body}
          >
            Send notification
          </Button>
        </Stack>

        {communications.length > 0 ? (
          <>
            <Divider />
            <Stack spacing={2}>
              <Typography variant="subtitle2">Communication history</Typography>
              {communications.map((comm) => (
                <Paper key={comm.id} variant="outlined" sx={{ p: 2 }}>
                  <Stack spacing={1}>
                    <Stack direction="row" spacing={1} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                      <Chip
                        label={comm.status}
                        color={statusChipColor(comm.status)}
                        size="small"
                      />
                      <Chip label={comm.communicationType} size="small" variant="outlined" />
                      <Typography variant="body2" color="text.secondary">
                        {comm.channel} → {comm.recipient}
                      </Typography>
                    </Stack>

                    <Typography variant="body2">
                      <strong>Subject:</strong> {comm.subject}
                    </Typography>

                    {comm.lastAttemptAtUtc ? (
                      <Typography variant="body2" color="text.secondary">
                        Last attempt: {new Date(comm.lastAttemptAtUtc).toLocaleString()} (
                        {comm.attemptCount} attempt{comm.attemptCount !== 1 ? 's' : ''})
                      </Typography>
                    ) : null}

                    {comm.deliveryId ? (
                      <Typography variant="body2" color="text.secondary">
                        Delivery ID: {comm.deliveryId}
                      </Typography>
                    ) : null}

                    {comm.failureReason ? (
                      <Alert severity="error" sx={{ py: 0 }}>
                        {comm.failureReason}
                      </Alert>
                    ) : null}

                    {comm.status === 'failed' ? (
                      <div>
                        {retryError && retryingId === comm.id ? (
                          <Alert severity="error" sx={{ mb: 1 }}>
                            {retryError}
                          </Alert>
                        ) : null}
                        <Button
                          size="small"
                          variant="outlined"
                          onClick={() => onRetry(comm.id)}
                          disabled={retryingId === comm.id}
                        >
                          Retry send
                        </Button>
                      </div>
                    ) : null}
                  </Stack>
                </Paper>
              ))}
            </Stack>
          </>
        ) : (
          <Typography variant="body2" color="text.secondary">
            No outbound communications recorded yet.
          </Typography>
        )}
      </Stack>
    </Paper>
  );
}
