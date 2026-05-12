import WarningAmberRounded from '@mui/icons-material/WarningAmberRounded';
import { Alert, Typography } from '@mui/material';

interface ValidationErrorProps {
  message?: string;
}

export function ValidationError({ message }: ValidationErrorProps) {
  if (!message) {
    return null;
  }

  return (
    <Alert icon={<WarningAmberRounded fontSize="inherit" />} severity="error" sx={{ mt: 1 }}>
      <Typography variant="body2">{message}</Typography>
    </Alert>
  );
}