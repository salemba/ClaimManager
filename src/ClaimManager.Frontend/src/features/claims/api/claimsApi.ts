import { apiFetch } from '../../../shared/api/client';
import type { Claim, ClaimDocument, ClaimFormValues, ClaimNote, ClaimSummary } from '../types/Claim';

function toRequest(values: ClaimFormValues) {
  return {
    claimantName: values.claimantName.trim(),
    claimantEmail: values.claimantEmail.trim(),
    claimantPhone: values.claimantPhone.trim(),
    policyNumber: values.policyNumber.trim(),
    lossDateUtc: new Date(values.lossDateUtc).toISOString(),
    lossType: values.lossType.trim(),
    lossDescription: values.lossDescription.trim(),
  };
}

export async function getClaims() {
  return apiFetch<ClaimSummary[]>('/api/claims');
}

export async function getClaim(id: string) {
  return apiFetch<Claim>(`/api/claims/${id}`);
}

export async function createClaim(values: ClaimFormValues) {
  return apiFetch<Claim>('/api/claims', {
    method: 'POST',
    body: JSON.stringify(toRequest(values)),
  });
}

export async function updateClaim(id: string, values: ClaimFormValues) {
  return apiFetch<Claim>(`/api/claims/${id}`, {
    method: 'PUT',
    body: JSON.stringify(toRequest(values)),
  });
}

export async function addClaimNote(id: string, content: string) {
  return apiFetch<ClaimNote>(`/api/claims/${id}/notes`, {
    method: 'POST',
    body: JSON.stringify({ content: content.trim() }),
  });
}

export async function uploadClaimDocument(id: string, file: File) {
  const formData = new FormData();
  formData.append('file', file);

  return apiFetch<ClaimDocument>(`/api/claims/${id}/documents`, {
    method: 'POST',
    body: formData,
  });
}

export async function advanceClaimWorkflow(id: string) {
  return apiFetch<Claim>(`/api/claims/${id}/advance`, {
    method: 'POST',
    body: '{}',
  });
}

export async function routeClaimForApproval(id: string, rationale: string) {
  return apiFetch<Claim>(`/api/claims/${id}/route-for-approval`, {
    method: 'POST',
    body: JSON.stringify({ rationale }),
  });
}