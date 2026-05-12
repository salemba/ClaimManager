export interface ProblemDetails {
  title?: string;
  detail?: string;
  status?: number;
  errors?: Record<string, string[]>;
}

export function getProblemMessage(problem: ProblemDetails | null | undefined) {
  return problem?.detail ?? problem?.title ?? 'Request failed.';
}

export function getProblemFieldErrors(problem: ProblemDetails | null | undefined) {
  return problem?.errors ?? {};
}