import { apiFetch } from '../../../shared/api/client';

export interface AuthSession {
  userId: string;
  email: string;
  roles: string[];
}

export interface ClaimSummary {
  claimNumber: string;
  status: string;
  createdAtUtc: string;
}

export interface WorkspacePayload {
  user: AuthSession;
  databaseAvailable: boolean;
  claims: ClaimSummary[];
}

export async function login(email: string, password: string) {
  return apiFetch<AuthSession>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  });
}

export async function logout() {
  return apiFetch<void>('/api/auth/logout', {
    method: 'POST',
  });
}

export async function getCurrentSession() {
  return apiFetch<AuthSession>('/api/auth/me');
}

export async function getWorkspace() {
  return apiFetch<WorkspacePayload>('/api/workspace');
}