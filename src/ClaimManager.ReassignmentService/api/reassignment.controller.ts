// This is a conceptual implementation of a controller using Express.js syntax.
// A complete setup would require an Express app, middleware configuration, and error handling.

import { NextFunction, Request, Response } from 'express';
import { ReassignmentService } from '../services/reassignment.service';
import { BusinessError, ClaimNotFoundError, ReassignmentConditionsNotMetError } from '../domain/errors';

// Assume a User object is attached to the request by an authentication middleware.
interface AuthenticatedRequest extends Request {
  user?: {
    id: string;
    roles: string[];
  };
}

export const supervisorOnly = (
  req: AuthenticatedRequest,
  res: Response,
  next: NextFunction
) => {
  if (req.user?.roles?.includes('SUPERVISOR')) {
    return next();
  }
  res.status(403).json({ message: 'Insufficient role. Supervisor role required.' });
};

export class ReassignmentController {
  constructor(private readonly reassignmentService: ReassignmentService) {}

  public async forceReassign(req: AuthenticatedRequest, res: Response): Promise<Response> {
    try {
      const { id: claimId } = req.params;
      const { newAdjusterId } = req.body;
      const supervisorId = req.user?.id;

      if (!newAdjusterId) {
        return res.status(400).json({ message: 'newAdjusterId is required.' });
      }
      if (!supervisorId) {
        // This should not happen if the authentication middleware is working correctly.
        return res.status(401).json({ message: 'Unauthorized.' });
      }

      const updatedClaim = await this.reassignmentService.forceReassignClaim(
        claimId,
        newAdjusterId,
        supervisorId
      );

      return res.status(200).json(updatedClaim);
    } catch (error) {
      if (error instanceof ReassignmentConditionsNotMetError) {
        return res.status(422).json({ message: error.message });
      }
      if (error instanceof ClaimNotFoundError) {
        return res.status(404).json({ message: error.message });
      }
      if (error instanceof BusinessError) {
        return res.status(400).json({ message: error.message });
      }
      
      console.error(error);
      return res.status(500).json({ message: 'An internal server error occurred.' });
    }
  }
}

// Example of how to wire this up with Express router
/*
import express from 'express';
import { ReassignmentController, supervisorOnly } from './reassignment.controller';
import { ReassignmentService } from '../services/reassignment.service';
// ... import and setup repositories and other services

const reassignmentService = new ReassignmentService(...)
const reassignmentController = new ReassignmentController(reassignmentService);

const router = express.Router();

router.put(
  '/claims/:id/force-reassign',
  authMiddleware, // some authentication middleware
  supervisorOnly,
  reassignmentController.forceReassign.bind(reassignmentController)
);
*/
