import { ForceReassignAudit } from '../domain/audit';

export interface IAuditRepository {
  save(audit: ForceReassignAudit): Promise<void>;
}
