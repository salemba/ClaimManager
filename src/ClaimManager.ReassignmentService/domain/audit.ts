import { Claim } from './claim';

export interface ForceReassignAudit {
  readonly supervisorId: string;
  readonly timestamp: Date;
  readonly reason: string;
  readonly beforeState: Claim;
  readonly afterState: Claim;
  readonly beforeAdjusterId: string;
  readonly afterAdjusterId: string;
}
