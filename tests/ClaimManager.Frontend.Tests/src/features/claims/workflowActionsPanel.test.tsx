import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppProviders } from '../../../../../src/ClaimManager.Frontend/src/app/providers/AppProviders';
import { WorkflowActionsPanel } from '../../../../../src/ClaimManager.Frontend/src/features/claims/components/WorkflowActionsPanel';
import type { Claim } from '../../../../../src/ClaimManager.Frontend/src/features/claims/types/Claim';

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
  nextExpectedAction: 'Investigate loss details',
  hasDataIntegrityWarning: false,
  dataIntegrityWarningMessage: null,
  auditHistory: [],
  notes: [],
  documents: [],
};

function renderPanel(claim: Claim, overrides?: Partial<React.ComponentProps<typeof WorkflowActionsPanel>>) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <WorkflowActionsPanel
          claim={claim}
          onAdvance={vi.fn().mockResolvedValue(undefined)}
          onRouteForApproval={vi.fn().mockResolvedValue(undefined)}
          {...overrides}
        />
      </MemoryRouter>
    </AppProviders>,
  );
}

afterEach(() => {
  cleanup();
});

describe('WorkflowActionsPanel', () => {
  it('renders "Begin Review" button when status is new', () => {
    renderPanel(baseClaim);

    expect(screen.getByRole('button', { name: /Begin Review/i })).toBeInTheDocument();
  });

  it('renders "Submit for Review" and "Route for Payment Approval" buttons when status is open', () => {
    renderPanel({ ...baseClaim, status: 'open' });

    expect(screen.getByRole('button', { name: /Submit for Review/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Route for Payment Approval/i })).toBeInTheDocument();
  });

  it('renders only "Route for Payment Approval" button when status is in-review', () => {
    renderPanel({ ...baseClaim, status: 'in-review' });

    expect(screen.queryByRole('button', { name: /Submit for Review/i })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Route for Payment Approval/i })).toBeInTheDocument();
  });

  it('renders "Close Claim" button when status is approved', () => {
    renderPanel({ ...baseClaim, status: 'approved' });

    expect(screen.getByRole('button', { name: /Close Claim/i })).toBeInTheDocument();
  });

  it('renders no action buttons and shows explanatory note when status is pending', () => {
    renderPanel({ ...baseClaim, status: 'pending' });

    expect(screen.queryByRole('button', { name: /Begin Review|Submit for Review|Route for Payment Approval|Close Claim/i })).not.toBeInTheDocument();
    expect(screen.getByText(/No workflow actions available from current state/i)).toBeInTheDocument();
  });

  it('renders no action buttons and shows explanatory note when status is closed', () => {
    renderPanel({ ...baseClaim, status: 'closed' });

    expect(screen.getByText(/No workflow actions available from current state/i)).toBeInTheDocument();
  });

  it('expands inline rationale input when "Route for Payment Approval" is clicked', () => {
    renderPanel({ ...baseClaim, status: 'open' });

    fireEvent.click(screen.getByRole('button', { name: /Route for Payment Approval/i }));

    expect(screen.getByLabelText(/Approval rationale/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Confirm Approval Routing/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Cancel/i })).toBeInTheDocument();
  });

  it('"Confirm Approval Routing" is disabled when rationale is empty', () => {
    renderPanel({ ...baseClaim, status: 'open' });

    fireEvent.click(screen.getByRole('button', { name: /Route for Payment Approval/i }));

    expect(screen.getByRole('button', { name: /Confirm Approval Routing/i })).toBeDisabled();
  });

  it('disables all workflow actions while a workflow mutation is in flight', () => {
    renderPanel({ ...baseClaim, status: 'open' }, { advancing: true, routing: false });

    expect(screen.getByRole('button', { name: /Submit for Review/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: /Route for Payment Approval/i })).toBeDisabled();
  });

  it('shows the routed status transition after a successful approval routing', async () => {
    const onRouteForApproval = vi.fn().mockResolvedValue(undefined);
    renderPanel({ ...baseClaim, status: 'open' }, { onRouteForApproval });

    fireEvent.click(screen.getByRole('button', { name: /Route for Payment Approval/i }));
    fireEvent.change(screen.getByLabelText(/Approval rationale/i), {
      target: { value: 'Payment exceeds standard threshold, requires supervisor review.' },
    });
    fireEvent.click(screen.getByRole('button', { name: /Confirm Approval Routing/i }));

    expect(await screen.findByText('Claim advanced from open to pending. Next: Awaiting payment approval decision.')).toBeInTheDocument();
  });
});
