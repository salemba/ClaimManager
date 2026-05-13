import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AppProviders } from '../../../../../src/ClaimManager.Frontend/src/app/providers/AppProviders';
import { CreateClaimPage } from '../../../../../src/ClaimManager.Frontend/src/features/claims/routes/CreateClaimPage';
import { EditClaimPage } from '../../../../../src/ClaimManager.Frontend/src/features/claims/routes/EditClaimPage';
import { addClaimNote, createClaim, getClaim, getClaims, updateClaim, uploadClaimDocument } from '../../../../../src/ClaimManager.Frontend/src/features/claims/api/claimsApi';
import { ApiError } from '../../../../../src/ClaimManager.Frontend/src/shared/api/client';

vi.mock('../../../../../src/ClaimManager.Frontend/src/features/claims/api/claimsApi', () => ({
  getClaims: vi.fn(),
  getClaim: vi.fn(),
  createClaim: vi.fn(),
  updateClaim: vi.fn(),
  addClaimNote: vi.fn(),
  uploadClaimDocument: vi.fn(),
  advanceClaimWorkflow: vi.fn(),
  routeClaimForApproval: vi.fn(),
}));

const mockedGetClaims = vi.mocked(getClaims);
const mockedGetClaim = vi.mocked(getClaim);
const mockedCreateClaim = vi.mocked(createClaim);
const mockedUpdateClaim = vi.mocked(updateClaim);
const mockedAddClaimNote = vi.mocked(addClaimNote);
const mockedUploadClaimDocument = vi.mocked(uploadClaimDocument);

const claimFixture = {
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
  notes: [],
  documents: [],
  auditHistory: [
    {
      action: 'created',
      summary: 'Claim file created with claimant, claim, and loss information.',
      performedAtUtc: '2026-05-11T00:00:00Z',
      performedByUserId: 'adjuster-1',
    },
  ],
};

const claimSummaryFixture = {
  id: claimFixture.id,
  claimNumber: claimFixture.claimNumber,
  status: claimFixture.status,
  claimantName: claimFixture.claimantName,
  policyNumber: claimFixture.policyNumber,
  lossDateUtc: claimFixture.lossDateUtc,
  createdAtUtc: claimFixture.createdAtUtc,
  updatedAtUtc: claimFixture.updatedAtUtc,
  blockerType: claimFixture.blockerType,
  blockerReason: claimFixture.blockerReason,
  ownedByUserId: claimFixture.ownedByUserId,
  hasDataIntegrityWarning: claimFixture.hasDataIntegrityWarning,
};

function renderCreateClaimPage() {
  return render(
    <AppProviders>
      <MemoryRouter>
        <CreateClaimPage />
      </MemoryRouter>
    </AppProviders>,
  );
}

function renderEditClaimPage(claimId = 'claim-1') {
  return render(
    <AppProviders>
      <MemoryRouter initialEntries={[`/claims/${claimId}/edit`]}>
        <Routes>
          <Route path="/claims/:claimId/edit" element={<EditClaimPage />} />
        </Routes>
      </MemoryRouter>
    </AppProviders>,
  );
}

describe('Claim form', () => {
  beforeEach(() => {
    mockedGetClaims.mockResolvedValue({
      items: [claimSummaryFixture],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });
    mockedGetClaim.mockResolvedValue(claimFixture);
    mockedUpdateClaim.mockResolvedValue({
      ...claimFixture,
      claimantEmail: 'jordan.updated@example.com',
      updatedAtUtc: '2026-05-12T09:15:00Z',
      updatedByUserId: 'adjuster-2',
      notes: [],
      documents: [],
      auditHistory: [
        {
          action: 'updated',
          summary: "Claimant email updated from 'jordan.avery@example.com' to 'jordan.updated@example.com'.",
          performedAtUtc: '2026-05-12T09:15:00Z',
          performedByUserId: 'adjuster-2',
        },
        ...claimFixture.auditHistory,
      ],
    });
  });

  afterEach(() => {
    cleanup();
    vi.clearAllMocks();
  });

  it('shows field-level validation errors before submit', async () => {
    const user = userEvent.setup();

    renderCreateClaimPage();

    await user.click(await screen.findByRole('button', { name: 'Create claim file' }));

    expect(await screen.findByText('Claimant name is required.')).toBeInTheDocument();
    expect(screen.getByText('Claimant email is required.')).toBeInTheDocument();
    expect(screen.getByText('Loss description is required.')).toBeInTheDocument();
    expect(mockedCreateClaim).not.toHaveBeenCalled();
  });

  it('creates a claim and shows confirmation', async () => {
    const user = userEvent.setup();

    mockedCreateClaim.mockResolvedValue({
      id: 'claim-2',
      claimNumber: 'CLM-0002',
      status: 'new',
      claimantName: 'Morgan Lee',
      claimantEmail: 'morgan.lee@example.com',
      claimantPhone: '555-0135',
      policyNumber: 'POL-0200',
      lossDateUtc: '2026-05-10T00:00:00Z',
      createdAtUtc: '2026-05-12T08:00:00Z',
      updatedAtUtc: null,
      lossType: 'Collision',
      lossDescription: 'Rear-end collision during evening commute.',
      createdByUserId: 'adjuster-1',
      updatedByUserId: null,
      blockerType: null,
      blockerReason: null,
      ownedByUserId: 'adjuster-1',
      nextExpectedAction: 'Initial review',
      hasDataIntegrityWarning: false,
      dataIntegrityWarningMessage: null,
      notes: [],
      documents: [],
      auditHistory: [],
    });

    mockedGetClaims
      .mockResolvedValueOnce({
        items: [claimSummaryFixture],
        page: 1,
        pageSize: 20,
        totalCount: 1,
      })
      .mockResolvedValueOnce({
        items: [
          claimSummaryFixture,
          {
            id: 'claim-2',
            claimNumber: 'CLM-0002',
            status: 'new',
            claimantName: 'Morgan Lee',
            policyNumber: 'POL-0200',
            lossDateUtc: '2026-05-10T00:00:00Z',
            createdAtUtc: '2026-05-12T08:00:00Z',
            updatedAtUtc: null,
            blockerType: null,
            blockerReason: null,
            ownedByUserId: 'adjuster-1',
            hasDataIntegrityWarning: false,
          },
        ],
        page: 1,
        pageSize: 20,
        totalCount: 2,
      });

    renderCreateClaimPage();

    fireEvent.change((await screen.findAllByRole('textbox', { name: /Claimant name/i }))[0], { target: { value: 'Morgan Lee' } });
    fireEvent.change(screen.getAllByRole('textbox', { name: /Claimant email/i })[0], { target: { value: 'morgan.lee@example.com' } });
    fireEvent.change(screen.getAllByRole('textbox', { name: /Claimant phone/i })[0], { target: { value: '555-0135' } });
    fireEvent.change(screen.getAllByRole('textbox', { name: /Policy number/i })[0], { target: { value: 'POL-0200' } });
    fireEvent.change(screen.getAllByLabelText('Loss date')[0], { target: { value: '2026-05-10' } });
    fireEvent.change(screen.getAllByRole('textbox', { name: /Loss type/i })[0], { target: { value: 'Collision' } });
    fireEvent.change(screen.getAllByRole('textbox', { name: /Loss description/i })[0], { target: { value: 'Rear-end collision during evening commute.' } });
    await user.click(screen.getAllByRole('button', { name: 'Create claim file' })[0]);

    await waitFor(() => {
      expect(mockedCreateClaim).toHaveBeenCalledTimes(1);
    });

    expect(await screen.findByText('Claim CLM-0002 created and added to the working queue.')).toBeInTheDocument();
  }, 15000);

  it('prefills edit values, renders audit history, and supports keyboard submission flow', async () => {
    const user = userEvent.setup({ delay: null });

    renderEditClaimPage();

    const claimantNameInput = await screen.findByRole('textbox', { name: 'Claimant name' });

    expect(claimantNameInput).toHaveValue('Jordan Avery');
    expect(await screen.findByRole('heading', { name: 'Material change history' })).toBeInTheDocument();
    expect(screen.getByText('Claim file created with claimant, claim, and loss information.')).toBeInTheDocument();

    // Tab past the WorkflowActionsPanel "Begin Review" button, then land on the first form field
    await user.tab();
    await user.tab();
    expect(claimantNameInput).toHaveFocus();

    fireEvent.change(screen.getByRole('textbox', { name: 'Claimant email' }), { target: { value: 'jordan.updated@example.com' } });
    await user.click(screen.getByRole('button', { name: 'Save claim updates' }));

    await waitFor(() => {
      expect(mockedUpdateClaim).toHaveBeenCalledTimes(1);
    });

    expect(await screen.findByText('Claim file updated and audit history refreshed.')).toBeInTheDocument();
  }, 15000);

  it('adds a note and refreshes the claim context panels', async () => {
    const user = userEvent.setup();

    mockedGetClaim
      .mockResolvedValueOnce(claimFixture)
      .mockResolvedValueOnce({
        ...claimFixture,
        notes: [
          {
            id: 'note-1',
            content: 'Customer called with vendor ETA.',
            createdAtUtc: '2026-05-12T10:00:00Z',
            createdByUserId: 'adjuster-2',
          },
        ],
        auditHistory: [
          {
            action: 'note-added',
            summary: "Claim note added: 'Customer called with vendor ETA.'.",
            performedAtUtc: '2026-05-12T10:00:00Z',
            performedByUserId: 'adjuster-2',
          },
          ...claimFixture.auditHistory,
        ],
      });
    mockedAddClaimNote.mockResolvedValue({
      id: 'note-1',
      content: 'Customer called with vendor ETA.',
      createdAtUtc: '2026-05-12T10:00:00Z',
      createdByUserId: 'adjuster-2',
    });

    renderEditClaimPage();

    await user.type(await screen.findByRole('textbox', { name: 'Claim note' }), 'Customer called with vendor ETA.');
    await user.click(screen.getByRole('button', { name: 'Add note' }));

    await waitFor(() => {
      expect(mockedAddClaimNote).toHaveBeenCalledWith('claim-1', 'Customer called with vendor ETA.');
    });

    expect(await screen.findByText('Customer called with vendor ETA.')).toBeInTheDocument();
  }, 10000);

  it('keeps specific note validation feedback adjacent to the note input', async () => {
    const user = userEvent.setup();

    mockedAddClaimNote.mockRejectedValue(
      new ApiError('Note content must be 4000 characters or fewer.', 400, {
        title: 'One or more validation errors occurred.',
        errors: {
          content: ['Note content must be 4000 characters or fewer.'],
        },
      }),
    );

    renderEditClaimPage();

    await user.type(await screen.findByRole('textbox', { name: 'Claim note' }), 'Customer called with vendor ETA.');
    await user.click(screen.getByRole('button', { name: 'Add note' }));

    await waitFor(() => {
      expect(mockedAddClaimNote).toHaveBeenCalledWith('claim-1', 'Customer called with vendor ETA.');
    });

    expect(await screen.findByRole('alert')).toHaveTextContent('Note content must be 4000 characters or fewer.');
  }, 10000);

  it('uploads a document and renders refreshed metadata', async () => {
    const user = userEvent.setup();
    const file = new File(['estimate'], 'estimate.pdf', { type: 'application/pdf' });

    mockedGetClaim
      .mockResolvedValueOnce(claimFixture)
      .mockResolvedValueOnce({
        ...claimFixture,
        documents: [
          {
            id: 'doc-1',
            fileName: 'estimate.pdf',
            fileType: '.pdf',
            contentType: 'application/pdf',
            fileSizeBytes: 8,
            uploadedAtUtc: '2026-05-12T10:15:00Z',
            uploadedByUserId: 'adjuster-2',
          },
        ],
        auditHistory: [
          {
            action: 'document-uploaded',
            summary: 'Document uploaded: estimate.pdf (.pdf).',
            performedAtUtc: '2026-05-12T10:15:00Z',
            performedByUserId: 'adjuster-2',
          },
          ...claimFixture.auditHistory,
        ],
      });
    mockedUploadClaimDocument.mockResolvedValue({
      id: 'doc-1',
      fileName: 'estimate.pdf',
      fileType: '.pdf',
      contentType: 'application/pdf',
      fileSizeBytes: 8,
      uploadedAtUtc: '2026-05-12T10:15:00Z',
      uploadedByUserId: 'adjuster-2',
    });

    renderEditClaimPage();

    await user.upload(await screen.findByLabelText('Claim document'), file);
    await user.click(screen.getByRole('button', { name: 'Upload document' }));

    await waitFor(() => {
      expect(mockedUploadClaimDocument).toHaveBeenCalledWith('claim-1', file);
    });

    expect(await screen.findByText('estimate.pdf')).toBeInTheDocument();
    expect(screen.getByText(/Uploaded by adjuster-2/)).toBeInTheDocument();
  }, 10000);

  it('shows upload validation errors next to the document control', async () => {
    const user = userEvent.setup();
    const file = new File(['malware'], 'payload.exe', { type: 'application/octet-stream' });

    mockedUploadClaimDocument.mockRejectedValue(
      new ApiError('Supported file types are PDF, JPG, JPEG, and PNG.', 400, {
        title: 'One or more validation errors occurred.',
        errors: {
          file: ['Supported file types are PDF, JPG, JPEG, and PNG.'],
        },
      }),
    );

    renderEditClaimPage();

    await user.upload(await screen.findByLabelText('Claim document'), file);
    await user.click(screen.getByRole('button', { name: 'Upload document' }));

    await waitFor(() => {
      expect(mockedUploadClaimDocument).toHaveBeenCalledWith('claim-1', file);
    });

    expect(await screen.findByRole('alert')).toHaveTextContent('Supported file types are PDF, JPG, JPEG, and PNG.');
  }, 10000);
});