import { apiFetch } from '../../../shared/api/client';
import type { Claim, ClaimDocument, ClaimFormValues, ClaimNote, ClaimSummary, ClaimsPage, ClaimsQueryParams } from '../types/Claim';

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

export async function getClaims(params?: ClaimsQueryParams) {
  const qs = new URLSearchParams();
  if (params?.search)                qs.set('search',        params.search);
  if (params?.status)                qs.set('status',         params.status);
  if (params?.blockerType)           qs.set('blockerType',    params.blockerType);
  if (params?.hasBlocker != null)    qs.set('hasBlocker',     String(params.hasBlocker));
  if (params?.ownedByUserId)         qs.set('ownedByUserId',  params.ownedByUserId);
  if (params?.page != null)          qs.set('page',           String(params.page));
  if (params?.pageSize != null)      qs.set('pageSize',       String(params.pageSize));
  const suffix = qs.toString() ? `?${qs.toString()}` : '';
  return apiFetch<ClaimsPage<ClaimSummary>>(`/api/claims${suffix}`);
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

export async function updateClaim(id: string, values: ClaimFormValues, rowVersion: string) {
  return apiFetch<Claim>(`/api/claims/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ ...toRequest(values), rowVersion }),
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

export async function advanceClaimWorkflow(id: string, rowVersion: string) {
  return apiFetch<Claim>(`/api/claims/${id}/advance`, {
    method: 'POST',
    body: JSON.stringify({ rowVersion }),
  });
}

export async function routeClaimForApproval(id: string, rationale: string, rowVersion: string) {
  return apiFetch<Claim>(`/api/claims/${id}/route-for-approval`, {
    method: 'POST',
    body: JSON.stringify({ rationale, rowVersion }),
  });
}

export async function interveneOnClaim(id: string, newOwnerId: string, targetStatus: string, rowVersion: string) {
  return apiFetch<Claim>(`/api/claims/${id}/intervene`, {
    method: 'POST',
    body: JSON.stringify({ newOwnerId, targetStatus, rowVersion }),
  });
}

export async function syncClaimPolicyData(id: string) {
  return apiFetch<Claim>(`/api/claims/${id}/sync-policy`, {
    method: 'POST',
    body: '{}',
  });
}

export async function syncClaimPaymentData(id: string) {
  return apiFetch<Claim>(`/api/claims/${id}/sync-payment`, {
    method: 'POST',
    body: '{}',
  });
}

export async function syncClaimDocumentData(id: string) {
  return apiFetch<Claim>(`/api/claims/${id}/sync-documents`, {
    method: 'POST',
    body: '{}',
  });
}

export async function reconcileClaimState(id: string) {
  return apiFetch<Claim>(`/api/claims/${id}/reconcile`, {
    method: 'POST',
    body: '{}',
  });
}