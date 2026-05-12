import type { PropsWithChildren, ReactNode } from 'react';
import { Stack, Typography } from '@mui/material';

interface FormFieldProps extends PropsWithChildren {
  label: string;
  description?: string;
  action?: ReactNode;
}

export function FormField({ label, description, action, children }: FormFieldProps) {
  return (
    <Stack spacing={1}>
      <Stack direction="row" spacing={1.5} sx={{ justifyContent: 'space-between', alignItems: 'baseline' }}>
        <Typography component="div" variant="subtitle1">
          {label}
        </Typography>
        {action}
      </Stack>
      {description ? (
        <Typography variant="body2" color="text.secondary">
          {description}
        </Typography>
      ) : null}
      {children}
    </Stack>
  );
}