import { Claim, ClaimStatus } from '../../src/ClaimManager.ReassignmentService/domain/claim';
import { canForceReassign } from '../../src/ClaimManager.ReassignmentService/use-cases/can-force-reassign';

describe('canForceReassign', () => {
  const now = new Date('2023-01-03T12:00:00.000Z');

  const baseClaim: Claim = {
    id: '1',
    adjusterId: 'adjuster-1',
    status: ClaimStatus.UNDER_REVIEW,
    amount: 5000,
    blockedSince: null,
    createdAt: new Date('2023-01-01T00:00:00.000Z'),
  };

  it('should return allowed: true if claim has been blocked for more than 48 hours', () => {
    const blockedSince = new Date('2023-01-01T11:59:59.000Z'); // 48h and 1 second ago
    const claim: Claim = { ...baseClaim, blockedSince };

    const result = canForceReassign(claim, now);

    expect(result.allowed).toBe(true);
    expect(result.reasons).toHaveLength(1);
    expect(result.reasons[0]).toBe('Claim has been blocked for more than 48 hours.');
  });

  it('should return allowed: true if claim amount is over 10,000', () => {
    const claim: Claim = { ...baseClaim, amount: 10001 };

    const result = canForceReassign(claim, now);

    expect(result.allowed).toBe(true);
    expect(result.reasons).toHaveLength(1);
    expect(result.reasons[0]).toBe('Claim amount is over €10000.');
  });

  it('should return allowed: true with both reasons if both conditions are met', () => {
    const blockedSince = new Date('2023-01-01T11:00:00.000Z');
    const claim: Claim = { ...baseClaim, amount: 15000, blockedSince };

    const result = canForceReassign(claim, now);

    expect(result.allowed).toBe(true);
    expect(result.reasons).toHaveLength(2);
    expect(result.reasons).toContain('Claim has been blocked for more than 48 hours.');
    expect(result.reasons).toContain('Claim amount is over €10000.');
  });

  it('should return allowed: false if no conditions are met', () => {
    const blockedSince = new Date('2023-01-02T12:00:00.000Z'); // 24h ago
    const claim: Claim = { ...baseClaim, blockedSince, amount: 9000 };

    const result = canForceReassign(claim, now);

    expect(result.allowed).toBe(false);
    expect(result.reasons).toHaveLength(0);
  });

  it('should return allowed: false if claim is blocked for exactly 48 hours', () => {
    const blockedSince = new Date('2023-01-01T12:00:00.000Z');
    const claim: Claim = { ...baseClaim, blockedSince };

    const result = canForceReassign(claim, now);

    expect(result.allowed).toBe(false);
    expect(result.reasons).toHaveLength(0);
  });

  it('should return allowed: false if claim amount is exactly 10,000', () => {
    const claim: Claim = { ...baseClaim, amount: 10000 };

    const result = canForceReassign(claim, now);

    expect(result.allowed).toBe(false);
    expect(result.reasons).toHaveLength(0);
  });

  it('should return allowed: false if blockedSince is null', () => {
    const claim: Claim = { ...baseClaim, blockedSince: null, amount: 5000 };
    const result = canForceReassign(claim, now);
    expect(result.allowed).toBe(false);
  });
});
