import { Claim } from '../domain/claim';

export interface IClaimRepository {
  findById(id: string): Promise<Claim | null>;
  update(claim: Claim): Promise<Claim>;
}
