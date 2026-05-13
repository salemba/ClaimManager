import { cleanup, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it } from 'vitest';
import { AppProviders } from '../../../../../src/ClaimManager.Frontend/src/app/providers/AppProviders';
import { ClaimStateSummaryPanel } from '../../../../../src/ClaimManager.Frontend/src/features/claims/components/ClaimStateSummaryPanel';
import { WorkflowTimeline } from '../../../../../src/ClaimManager.Frontend/src/features/claims/components/WorkflowTimeline';
import type { Claim, ClaimAuditEntry } from '../../../../../src/ClaimManager.Frontend/src/features/claims/types/Claim';

const baseClaim: Claim = {
  id: 'claim-1',
  claimNumber: 'CLM-0001',
  status: 'new',
  claimantName: 'Jordan Avery',
  claimantEmail: 'jordan.avery@example.com',
  claimantPhone: '555-0100',
  policyNumber: 'POL-2026-0001',
  lossDateUtc: '2026-05-08T00:00:00Z',
  createdAtUtc: '2026-05-11T00:00:00Z',
  updatedAtUtc: null,
  lossType: 'Water damage',
  lossDescription: 'Pipe burst in lower level.',
  createdByUserId: 'adjuster-1',
  updatedByUserId: null,
  blockerType: null,
  blockerReason: null,
  ownedByUserId: 'adjuster-1',
  nextExpectedAction: 'Initial review',
  hasDataIntegrityWarning: false,
  dataIntegrityWarningMessage: null,
  auditHistory: [
    {
      action: 'created',
      summary: 'Claim file created with claimant, claim, and loss information.',
      performedAtUtc: '2026-05-11T00:00:00Z',
      performedByUserId: 'adjuster-1',
    },
  ],
  notes: [],
  documents: [],
};

function renderStateSummaryPanel(claim: Claim) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <ClaimStateSummaryPanel claim={claim} />
      </MemoryRouter>
    </AppProviders>,
  );
}

function renderWorkflowTimeline(auditHistory: ClaimAuditEntry[]) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <WorkflowTimeline auditHistory={auditHistory} />
      </MemoryRouter>
    </AppProviders>,
  );
}

afterEach(() => {
  cleanup();
});

describe('ClaimStateSummaryPanel', () => {
  it('renders claim status badge', () => {
    renderStateSummaryPanel(baseClaim);

    expect(screen.getByText('new')).toBeInTheDocument();
  });

  it('renders owner and next expected action', () => {
    renderStateSummaryPanel(baseClaim);

    expect(screen.getByText('adjuster-1')).toBeInTheDocument();
    expect(screen.getByText('Initial review')).toBeInTheDocument();
  });

  it('renders blocker details when blocker is present', () => {
    const blockedClaim: Claim = {
      ...baseClaim,
      blockerType: 'Pending documentation',
      blockerReason: 'Waiting for police report from claimant.',
    };

    renderStateSummaryPanel(blockedClaim);

    expect(screen.getByText('Pending documentation')).toBeInTheDocument();
    expect(screen.getByText('Waiting for police report from claimant.')).toBeInTheDocument();
  });

  it('shows "No active blocker" calm state when no blocker is set', () => {
    renderStateSummaryPanel(baseClaim);

    expect(screen.queryByText('Blocker detail')).not.toBeInTheDocument();
    expect(screen.getByText('No active blocker')).toBeInTheDocument();
  });

  it('renders data integrity warning when flag is set', () => {
    const warnClaim: Claim = {
      ...baseClaim,
      hasDataIntegrityWarning: true,
      dataIntegrityWarningMessage: 'Policy number format is invalid.',
    };

    renderStateSummaryPanel(warnClaim);

    expect(screen.getByText('Policy number format is invalid.')).toBeInTheDocument();
  });

  it('does not render data integrity warning when flag is false', () => {
    renderStateSummaryPanel(baseClaim);

    expect(screen.queryByText('Policy number format is invalid.')).not.toBeInTheDocument();
  });

  it('renders "Unassigned" when owner is not set', () => {
    const unownedClaim: Claim = { ...baseClaim, ownedByUserId: null };

    renderStateSummaryPanel(unownedClaim);

    expect(screen.getByText('Unassigned')).toBeInTheDocument();
  });

  it('renders "Awaiting workflow progression" when next expected action is not set', () => {
    const pendingClaim: Claim = { ...baseClaim, nextExpectedAction: null };

    renderStateSummaryPanel(pendingClaim);

    expect(screen.getByText('Awaiting workflow progression')).toBeInTheDocument();
  });
});

describe('WorkflowTimeline', () => {
  it('renders audit entries in newest-first order', () => {
    const auditHistory: ClaimAuditEntry[] = [
      {
        action: 'updated',
        summary: 'Claimant email updated.',
        performedAtUtc: '2026-05-12T09:15:00Z',
        performedByUserId: 'adjuster-2',
      },
      {
        action: 'created',
        summary: 'Claim file created.',
        performedAtUtc: '2026-05-11T00:00:00Z',
        performedByUserId: 'adjuster-1',
      },
    ];

    renderWorkflowTimeline(auditHistory);

    const entries = screen.getAllByText(/Claim file created\.|Claimant email updated\./);
    expect(entries[0]).toHaveTextContent('Claimant email updated.');
    expect(entries[1]).toHaveTextContent('Claim file created.');
  });

  it('renders empty state when no audit history', () => {
    renderWorkflowTimeline([]);

    expect(screen.getByText('No recorded changes yet.')).toBeInTheDocument();
  });

  it('renders the section heading', () => {
    renderWorkflowTimeline(baseClaim.auditHistory);

    expect(screen.getByRole('heading', { name: 'Material change history' })).toBeInTheDocument();
  });
});
