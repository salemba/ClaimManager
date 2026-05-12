import ConstructionRounded from '@mui/icons-material/ConstructionRounded';
import InfoOutlined from '@mui/icons-material/InfoOutlined';
import { Box, List, ListItem, ListItemIcon, ListItemText, Paper, Stack, Typography } from '@mui/material';
import { PageSurface } from '../../shared/ui/PageSurface';
import { StatusBadge } from '../../shared/ui/StatusBadge';
import type { WorkbenchRouteMeta } from '../router/workbench';

interface WorkbenchPlaceholderPageProps {
  meta: WorkbenchRouteMeta;
}

export function WorkbenchPlaceholderPage({ meta }: WorkbenchPlaceholderPageProps) {
  return (
    <PageSurface>
      <Stack spacing={3}>
        <Paper component="section" sx={{ p: { xs: 3, md: 4 } }}>
          <Stack spacing={2}>
            <StatusBadge tone="info" label="Route scaffold" icon={<ConstructionRounded fontSize="small" />} />
            <div>
              <Typography variant="overline" color="text.secondary">
                {meta.eyebrow}
              </Typography>
              <Typography variant="h2">{meta.title}</Typography>
            </div>
            <Typography color="text.secondary">{meta.description}</Typography>
            <Typography>{meta.placeholderSummary}</Typography>
          </Stack>
        </Paper>

        <Paper component="section" variant="outlined" sx={{ p: { xs: 3, md: 4 } }}>
          <Typography variant="h3" gutterBottom>
            What this scaffold guarantees
          </Typography>
          <List disablePadding>
            {[
              'Predictable route-level navigation in the shared workbench shell.',
              'Stable page landmarks and headings for keyboard and screen-reader users.',
              'A clear boundary between shipped foundations and future workflow-specific UI.',
            ].map((item) => (
              <ListItem key={item} disableGutters>
                <ListItemIcon sx={{ minWidth: 36 }}>
                  <InfoOutlined color="primary" fontSize="small" />
                </ListItemIcon>
                <ListItemText primary={item} />
              </ListItem>
            ))}
          </List>
          <Box
            sx={{
              mt: 3,
              p: 2,
              borderRadius: 3,
              bgcolor: 'grey.50',
              border: (theme) => `1px dashed ${theme.palette.divider}`,
            }}
          >
            <Typography variant="body2" color="text.secondary">
              Placeholder route only. Workflow implementation remains intentionally out of scope for Story 1.2.
            </Typography>
          </Box>
        </Paper>
      </Stack>
    </PageSurface>
  );
}