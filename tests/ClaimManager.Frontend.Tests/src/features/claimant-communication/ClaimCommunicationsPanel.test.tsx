import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { AppProviders } from '../../../../../src/ClaimManager.Frontend/src/app/providers/AppProviders';
import { ClaimCommunicationsPanel } from '../../../../../src/ClaimManager.Frontend/src/features/claimant-communication/ClaimCommunicationsPanel';
import type { ClaimCommunication } from '../../../../../src/ClaimManager.Frontend/src/features/claimant-communication/types';

const noopSend = vi.fn().mockResolvedValue(undefined);
const noopRetry = vi.fn().mockResolvedValue(undefined);

const sentCommunication: ClaimCommunication = {
  id: 'comm-1',
  communicationType: 'operational',
  channel: 'email',
  recipient: 'ops@claimmanager.local',
  subject: 'Claim flagged',
  status: 'sent',
  attemptCount: 1,
  lastAttemptAtUtc: '2026-05-14T12:00:00Z',
  deliveryId: 'DEL-ABC123',
  failureReason: null,
  createdAtUtc: '2026-05-14T11:59:00Z',
  createdByUserId: 'adjuster-1',
};

const failedCommunication: ClaimCommunication = {
  ...sentCommunication,
  id: 'comm-2',
  status: 'failed',
  deliveryId: null,
  failureReason: 'Mailbox not found.',
};

function renderPanel(
  communications: ClaimCommunication[],
  overrides?: Partial<React.ComponentProps<typeof ClaimCommunicationsPanel>>,
) {
  return render(
    <AppProviders>
      <MemoryRouter>
        <ClaimCommunicationsPanel
          claimId="claim-1"
          communications={communications}
          onSend={noopSend}
          onRetry={noopRetry}
          {...overrides}
        />
      </MemoryRouter>
    </AppProviders>,
  );
}

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe('ClaimCommunicationsPanel', () => {
  it('renders the panel heading', () => {
    renderPanel([]);
    expect(screen.getByRole('heading', { name: 'Outbound Communications' })).toBeInTheDocument();
  });

  it('shows empty state message when no communications', () => {
    renderPanel([]);
    expect(screen.getByText('No outbound communications recorded yet.')).toBeInTheDocument();
  });

  it('renders sent communication with success chip and delivery ID', () => {
    renderPanel([sentCommunication]);

    expect(screen.getByText('sent')).toBeInTheDocument();
    expect(screen.getByText('Claim flagged')).toBeInTheDocument();
    expect(screen.getByText(/DEL-ABC123/)).toBeInTheDocument();
  });

  it('renders failed communication with error chip and failure reason', () => {
    renderPanel([failedCommunication]);

    expect(screen.getByText('failed')).toBeInTheDocument();
    expect(screen.getByText('Mailbox not found.')).toBeInTheDocument();
  });

  it('shows retry button only for failed communications', () => {
    renderPanel([sentCommunication, failedCommunication]);

    const retryButtons = screen.getAllByRole('button', { name: 'Retry send' });
    expect(retryButtons).toHaveLength(1);
  });

  it('calls onRetry with the correct notification id when retry is clicked', async () => {
    const mockRetry = vi.fn().mockResolvedValue(undefined);
    renderPanel([failedCommunication], { onRetry: mockRetry });

    fireEvent.click(screen.getByRole('button', { name: 'Retry send' }));

    await waitFor(() => {
      expect(mockRetry).toHaveBeenCalledWith('comm-2');
    });
  });

  it('disables send button when required fields are empty', () => {
    renderPanel([]);

    const sendButton = screen.getByRole('button', { name: 'Send notification' });
    expect(sendButton).toBeDisabled();
  });

  it('enables send button when recipient, subject, and body are filled', async () => {
    renderPanel([]);

    fireEvent.change(screen.getByLabelText('Recipient email'), {
      target: { value: 'test@example.com' },
    });
    fireEvent.change(screen.getByLabelText('Subject'), {
      target: { value: 'Status update' },
    });
    fireEvent.change(screen.getByLabelText('Message body'), {
      target: { value: 'Your claim is being reviewed.' },
    });

    expect(screen.getByRole('button', { name: 'Send notification' })).not.toBeDisabled();
  });

  it('calls onSend with correct request when send button is clicked', async () => {
    const mockSend = vi.fn().mockResolvedValue(undefined);
    renderPanel([], { onSend: mockSend });

    fireEvent.change(screen.getByLabelText('Recipient email'), {
      target: { value: 'a@b.com' },
    });
    fireEvent.change(screen.getByLabelText('Subject'), {
      target: { value: 'Test subject' },
    });
    fireEvent.change(screen.getByLabelText('Message body'), {
      target: { value: 'Test body' },
    });
    fireEvent.click(screen.getByRole('button', { name: 'Send notification' }));

    await waitFor(() => {
      expect(mockSend).toHaveBeenCalledWith({
        communicationType: 'operational',
        channel: 'email',
        recipient: 'a@b.com',
        subject: 'Test subject',
        body: 'Test body',
      });
    });
  });

  it('shows send error alert when sendError is provided', () => {
    renderPanel([], { sendError: 'Unable to send the notification right now.' });

    expect(screen.getByText('Unable to send the notification right now.')).toBeInTheDocument();
  });

  it('shows attempt count for sent communication', () => {
    renderPanel([sentCommunication]);

    expect(screen.getByText(/1 attempt/)).toBeInTheDocument();
  });

  it('disables retry button when retryingId matches', () => {
    renderPanel([failedCommunication], { retryingId: 'comm-2' });

    expect(screen.getByRole('button', { name: 'Retry send' })).toBeDisabled();
  });
});
