export interface ClaimCommunication {
  id: string;
  communicationType: string;
  channel: string;
  recipient: string;
  subject: string;
  status: string;
  attemptCount: number;
  lastAttemptAtUtc: string | null;
  deliveryId: string | null;
  failureReason: string | null;
  createdAtUtc: string;
  createdByUserId: string;
}

export interface SendNotificationRequest {
  communicationType: string;
  channel: string;
  recipient: string;
  subject: string;
  body: string;
}
