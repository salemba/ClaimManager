import AddIcon from '@mui/icons-material/AddCircleOutlineRounded';
import BlockIcon from '@mui/icons-material/BlockRounded';
import { useEffect, useState } from 'react';
import { Link as RouterLink, useNavigate, useSearchParams } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import {
  Alert,
  Box,
  Button,
  Checkbox,
  Chip,
  CircularProgress,
  FormControl,
  FormControlLabel,
  InputLabel,
  List,
  ListItem,
  ListItemButton,
  ListItemText,
  MenuItem,
  Paper,
  Select,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { getClaims } from '../api/claimsApi';
import type { ClaimsQueryParams } from '../types/Claim';
import { StatusBadge } from '../../../shared/ui/StatusBadge';
import { PageSurface } from '../../../shared/ui/PageSurface';

const KNOWN_STATUSES = ['new', 'open', 'in-review', 'pending', 'approved', 'closed'] as const;
const DEFAULT_PAGE_SIZE = 20;

function normalizePage(value: number | undefined) {
  return Number.isFinite(value) && value && value > 0 ? Math.floor(value) : 1;
}

function buildParams(searchParams: URLSearchParams): ClaimsQueryParams {
  return {
    search: searchParams.get('search') ?? undefined,
    status: searchParams.get('status') ?? undefined,
    blockerType: searchParams.get('blockerType') ?? undefined,
    hasBlocker:
      searchParams.get('hasBlocker') === 'true'
        ? true
        : searchParams.get('hasBlocker') === 'false'
          ? false
          : undefined,
    page: searchParams.get('page') ? Number(searchParams.get('page')) : 1,
    pageSize: DEFAULT_PAGE_SIZE,
  };
}

function hasActiveFilters(searchParams: URLSearchParams) {
  return (
    !!searchParams.get('search') ||
    !!searchParams.get('status') ||
    !!searchParams.get('blockerType') ||
    searchParams.has('hasBlocker')
  );
}

export function ClaimsQueuePage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const navigate = useNavigate();

  const params = buildParams(searchParams);
  const activeFilters = hasActiveFilters(searchParams);
  const searchFromUrl = searchParams.get('search') ?? '';

  const [searchInput, setSearchInput] = useState(searchFromUrl);

  useEffect(() => {
    setSearchInput(searchFromUrl);
  }, [searchFromUrl]);

  useEffect(() => {
    const timer = setTimeout(() => {
      if (searchInput === searchFromUrl) {
        return;
      }

      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        if (searchInput) {
          next.set('search', searchInput);
        } else {
          next.delete('search');
        }
        next.delete('page');
        return next;
      });
    }, 300);
    return () => clearTimeout(timer);
  }, [searchFromUrl, searchInput, setSearchParams]);

  const claimsQuery = useQuery({
    queryKey: ['claims', 'list', params],
    queryFn: () => getClaims(params),
    staleTime: 30_000,
  });

  const currentPage = normalizePage(claimsQuery.data?.page ?? params.page);
  const totalCount = claimsQuery.data?.totalCount ?? 0;
  const pageSize = claimsQuery.data?.pageSize ?? DEFAULT_PAGE_SIZE;
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const pageStart = totalCount > 0 ? (currentPage - 1) * pageSize + 1 : 0;
  const pageEnd = totalCount > 0 ? Math.min(pageStart + pageSize - 1, totalCount) : 0;

  function setPage(page: number) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.set('page', String(page));
      return next;
    });
  }

  function setStatus(value: string) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      if (value) {
        next.set('status', value);
      } else {
        next.delete('status');
      }
      next.delete('page');
      return next;
    });
  }

  function setHasBlocker(checked: boolean) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      if (checked) {
        next.set('hasBlocker', 'true');
      } else {
        next.delete('hasBlocker');
      }
      next.delete('page');
      return next;
    });
  }

  function clearFilters() {
    setSearchInput('');
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      next.delete('search');
      next.delete('status');
      next.delete('blockerType');
      next.delete('hasBlocker');
      next.delete('page');
      return next;
    });
  }

  return (
    <PageSurface>
      <Stack spacing={3}>
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ justifyContent: 'space-between', alignItems: { sm: 'center' } }}>
          <div>
            <Typography variant="overline" color="text.secondary">
              Claims queue
            </Typography>
            <Typography variant="h2">Claims work queue</Typography>
          </div>
          <Button
            component={RouterLink}
            to="/claims/new"
            variant="contained"
            startIcon={<AddIcon />}
          >
            New Claim
          </Button>
        </Stack>

        <Paper component="section" sx={{ p: { xs: 2, md: 3 } }} aria-label="Claims filters">
          <Stack spacing={2}>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ alignItems: { sm: 'center' } }}>
              <TextField
                label="Search"
                placeholder="Claim number, claimant, or policy"
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                size="small"
                sx={{ minWidth: 260 }}
                slotProps={{ htmlInput: { 'aria-label': 'Search claims' } }}
              />

              <FormControl size="small" sx={{ minWidth: 160 }}>
                <InputLabel id="status-filter-label">Status</InputLabel>
                <Select
                  labelId="status-filter-label"
                  label="Status"
                  value={searchParams.get('status') ?? ''}
                  onChange={(e) => setStatus(e.target.value)}
                >
                  <MenuItem value="">All statuses</MenuItem>
                  {KNOWN_STATUSES.map((s) => (
                    <MenuItem key={s} value={s}>
                      {s}
                    </MenuItem>
                  ))}
                </Select>
              </FormControl>

              <FormControlLabel
                control={
                  <Checkbox
                    checked={searchParams.get('hasBlocker') === 'true'}
                    onChange={(e) => setHasBlocker(e.target.checked)}
                    inputProps={{ 'aria-label': 'Show only claims with an active blocker' }}
                  />
                }
                label="Has blocker"
              />

              {activeFilters ? (
                <Button variant="text" size="small" onClick={clearFilters} aria-label="Clear all filters">
                  Clear filters
                </Button>
              ) : null}
            </Stack>
          </Stack>
        </Paper>

        <Paper component="section" aria-label="Claims list">
          <Stack>
            {claimsQuery.isLoading ? (
              <Stack spacing={1.5} sx={{ alignItems: 'center', py: 6 }}>
                <CircularProgress aria-label="Loading claims" />
                <Typography color="text.secondary">Loading claims...</Typography>
              </Stack>
            ) : claimsQuery.isError ? (
              <Box sx={{ p: 3 }}>
                <Alert severity="error">Unable to load claims. Please try again.</Alert>
              </Box>
            ) : totalCount === 0 ? (
              <Stack spacing={1.5} sx={{ alignItems: 'center', py: 6, px: 3, textAlign: 'center' }}>
                <Typography color="text.secondary">
                  {activeFilters
                    ? 'No claims match the current filters.'
                    : 'No claims have been filed yet.'}
                </Typography>
                {activeFilters ? (
                  <Button variant="text" size="small" onClick={clearFilters}>
                    Clear filters
                  </Button>
                ) : null}
              </Stack>
            ) : (
              <>
                <Stack
                  direction="row"
                  spacing={1}
                  sx={{ px: 3, pt: 2, pb: 1, justifyContent: 'space-between', alignItems: 'center' }}
                >
                  <Typography variant="body2" color="text.secondary">
                    {`Showing ${pageStart}–${pageEnd} of ${totalCount} claims`}
                  </Typography>
                </Stack>

                <List disablePadding sx={{ display: 'grid', gap: 0 }}>
                  {(claimsQuery.data?.items ?? []).map((claim) => (
                    <ListItem key={claim.id} disablePadding sx={{ borderBottom: (theme) => `1px solid ${theme.palette.divider}` }}>
                      <ListItemButton
                        onClick={() => navigate(`/claims/${claim.id}/edit`)}
                        sx={{ px: 3, py: 2 }}
                        aria-label={`Open claim ${claim.claimNumber}`}
                      >
                        <ListItemText
                          primary={
                            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
                              <Typography variant="subtitle2">{claim.claimNumber}</Typography>
                              <Typography variant="body2">{claim.claimantName}</Typography>
                              <StatusBadge tone={getStatusTone(claim.status)} label={claim.status} />
                              {claim.blockerType ? (
                                <Chip
                                  icon={<BlockIcon fontSize="small" />}
                                  label={claim.blockerType}
                                  size="small"
                                  color="warning"
                                  variant="outlined"
                                />
                              ) : null}
                            </Stack>
                          }
                          secondary={
                            <Typography variant="caption" color="text.secondary">
                              Policy {claim.policyNumber} · Loss {new Date(claim.lossDateUtc).toLocaleDateString()}
                            </Typography>
                          }
                        />
                      </ListItemButton>
                    </ListItem>
                  ))}
                </List>

                {totalCount > pageSize ? (
                  <Stack
                    direction="row"
                    spacing={1}
                    sx={{ px: 3, py: 2, justifyContent: 'flex-end', alignItems: 'center' }}
                  >
                    <Button
                      size="small"
                      variant="outlined"
                      disabled={currentPage <= 1}
                      onClick={() => setPage(currentPage - 1)}
                      aria-label="Previous page"
                    >
                      Previous
                    </Button>
                    <Typography variant="body2" color="text.secondary">
                      Page {currentPage} of {totalPages}
                    </Typography>
                    <Button
                      size="small"
                      variant="outlined"
                      disabled={currentPage >= totalPages}
                      onClick={() => setPage(currentPage + 1)}
                      aria-label="Next page"
                    >
                      Next
                    </Button>
                  </Stack>
                ) : null}
              </>
            )}
          </Stack>
        </Paper>
      </Stack>
    </PageSurface>
  );
}

function getStatusTone(status: string): 'neutral' | 'info' | 'warning' | 'success' | 'error' {
  switch (status) {
    case 'new': return 'neutral';
    case 'open': return 'info';
    case 'in-review': return 'warning';
    case 'pending': return 'warning';
    case 'approved': return 'success';
    case 'closed': return 'neutral';
    default: return 'neutral';
  }
}
