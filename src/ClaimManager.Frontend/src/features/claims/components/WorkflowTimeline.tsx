import { List, ListItem, Paper, Stack, Typography } from '@mui/material';
import type { ClaimAuditEntry } from '../types/Claim';

interface WorkflowTimelineProps {
  auditHistory: ClaimAuditEntry[];
}

function formatDate(utcString: string): string {
  const d = new Date(utcString);
  return isNaN(d.getTime()) ? utcString : d.toLocaleString();
}

export function WorkflowTimeline({ auditHistory }: WorkflowTimelineProps) {
  return (
    <Paper component="section" sx={{ p: { xs: 3, md: 4 } }} aria-label="Workflow timeline">
      <Stack spacing={2.5}>
        <div>
          <Typography variant="overline" color="text.secondary">
            Claim history
          </Typography>
          <Typography variant="h2">Material change history</Typography>
          <Typography color="text.secondary">
            A chronological record of material changes made to this claim.
          </Typography>
        </div>

        {auditHistory.length === 0 ? (
          <Typography color="text.secondary">No recorded changes yet.</Typography>
        ) : (
          <List disablePadding sx={{ display: 'grid', gap: 1.5 }}>
            {auditHistory.map((entry) => (
              <ListItem
                key={`${entry.performedAtUtc}-${entry.performedByUserId}`}
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
                    {formatDate(entry.performedAtUtc)} · {entry.performedByUserId}
                  </Typography>
                  <Typography>{entry.summary}</Typography>
                </Stack>
              </ListItem>
            ))}
          </List>
        )}
      </Stack>
    </Paper>
  );
}
