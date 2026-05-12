import type { ProblemDetails } from './problemDetails';
import { getProblemMessage } from './problemDetails';

export class ApiError extends Error {
  public readonly status: number;
  public readonly problemDetails: ProblemDetails | null;

  constructor(message: string, status: number, problemDetails: ProblemDetails | null = null) {
    super(message);
    this.status = status;
    this.problemDetails = problemDetails;
  }
}

export async function apiFetch<T>(input: RequestInfo | URL, init?: RequestInit): Promise<T> {
  const isFormDataBody = init?.body instanceof FormData;
  const headers = new Headers(init?.headers);

  if (!isFormDataBody) {
    headers.set('Content-Type', 'application/json');
  }

  const csrfToken = getCookieValue('claimmanager.csrf');
  if (csrfToken && isUnsafeMethod(init?.method)) {
    headers.set('X-CSRF-TOKEN', csrfToken);
  }

  const response = await fetch(input, {
    ...init,
    credentials: 'include',
    headers,
  });

  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as ProblemDetails | null;
    throw new ApiError(getProblemMessage(problem), response.status, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

function isUnsafeMethod(method?: string) {
  const normalizedMethod = method?.toUpperCase() ?? 'GET';
  return normalizedMethod !== 'GET' && normalizedMethod !== 'HEAD' && normalizedMethod !== 'OPTIONS';
}

function getCookieValue(cookieName: string) {
  if (typeof document === 'undefined') {
    return null;
  }

  const encodedName = `${encodeURIComponent(cookieName)}=`;
  for (const cookie of document.cookie.split(';')) {
    const normalizedCookie = cookie.trim();
    if (normalizedCookie.startsWith(encodedName)) {
      return decodeURIComponent(normalizedCookie.slice(encodedName.length));
    }
  }

  return null;
}