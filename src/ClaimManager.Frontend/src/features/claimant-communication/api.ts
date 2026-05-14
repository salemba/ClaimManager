import { apiFetch } from '../../shared/api/client';
import type { ClaimCommunication, SendNotificationRequest } from './types';

export async function sendClaimNotification(claimId: string, request: SendNotificationRequest) {
  return apiFetch<ClaimCommunication>(`/api/claims/${claimId}/notifications`, {
    method: 'POST',
    body: JSON.stringify(request),
  });
}

export async function retryClaimNotification(claimId: string, notificationId: string) {
  return apiFetch<ClaimCommunication>(
    `/api/claims/${claimId}/notifications/${notificationId}/retry`,
    {
      method: 'POST',
      body: '{}',
    },
  );
}
