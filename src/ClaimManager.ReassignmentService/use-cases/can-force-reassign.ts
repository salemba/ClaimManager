import { Claim } from '../domain/claim';

const FORTY_EIGHT_HOURS_IN_MS = 48 * 60 * 60 * 1000;
const MINIMUM_AMOUNT_FOR_REASSIGNMENT = 10000;

export function canForceReassign(
  claim: Claim,
  now: Date
): { allowed: boolean; reasons: string[] } {
  const reasons: string[] = [];

  if (claim.blockedSince) {
    const blockedDuration = now.getTime() - claim.blockedSince.getTime();
    if (blockedDuration > FORTY_EIGHT_HOURS_IN_MS) {
      reasons.push(`Claim has been blocked for more than 48 hours.`);
    }
  }

  if (claim.amount > MINIMUM_AMOUNT_FOR_REASSIGNMENT) {
    reasons.push(`Claim amount is over €${MINIMUM_AMOUNT_FOR_REASSIGNMENT}.`);
  }

  return {
    allowed: reasons.length > 0,
    reasons,
  };
}
