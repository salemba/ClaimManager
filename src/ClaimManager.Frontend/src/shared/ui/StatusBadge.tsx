import type { ReactElement } from 'react';
import { Chip, useTheme } from '@mui/material';
import type { statusTokens } from './theme';

type StatusTone = keyof typeof statusTokens;

interface StatusBadgeProps {
  label: string;
  tone: StatusTone;
  icon?: ReactElement;
}

export function StatusBadge({ label, tone, icon }: StatusBadgeProps) {
  const theme = useTheme();
  const token = theme.claimManagerTokens.status[tone];

  return (
    <Chip
      icon={icon}
      label={label}
      size="small"
      sx={{
        height: 30,
        border: `1px solid ${token.border}`,
        bgcolor: token.fill,
        color: token.text,
        '& .MuiChip-icon': {
          color: token.icon,
        },
      }}
    />
  );
}