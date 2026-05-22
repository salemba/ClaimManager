export interface INotificationService {
  notifyAdjuster(adjusterId: string, message: string): Promise<void>;
}
