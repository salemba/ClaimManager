import type { PropsWithChildren } from 'react';
import { Box } from '@mui/material';

interface PageSurfaceProps extends PropsWithChildren {
  centered?: boolean;
  maxWidth?: number | string;
}

export function PageSurface({ children, centered = false, maxWidth = 1440 }: PageSurfaceProps) {
  return (
    <Box
      className="page-surface"
      sx={{
        width: '100%',
        maxWidth,
        mx: 'auto',
        px: { xs: 2, sm: 3, md: 4 },
        py: { xs: 2, md: 4 },
        display: 'grid',
        alignContent: centered ? 'center' : 'start',
        minHeight: centered ? '100vh' : 'auto',
      }}
    >
      {children}
    </Box>
  );
}