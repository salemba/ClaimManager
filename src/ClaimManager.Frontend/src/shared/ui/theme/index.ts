import { alpha, createTheme } from '@mui/material/styles';

export const colorTokens = {
  ink: '#10233a',
  inkMuted: '#4e6175',
  surface: '#f4f7fb',
  surfaceRaised: '#ffffff',
  surfaceTint: '#e9f0f8',
  border: '#d5deea',
  accent: '#135d66',
  accentStrong: '#0f4b53',
  accentSoft: '#d7eef0',
  highlight: '#2f6fed',
  highlightSoft: '#dce7ff',
} as const;

export const spacingTokens = {
  xxs: 0.5,
  xs: 1,
  sm: 1.5,
  md: 2,
  lg: 3,
  xl: 4,
  xxl: 6,
} as const;

export const radiusTokens = {
  sm: 10,
  md: 16,
  lg: 24,
  pill: 999,
} as const;

export const elevationTokens = {
  shell: '0 18px 60px rgba(16, 35, 58, 0.10)',
  panel: '0 10px 28px rgba(16, 35, 58, 0.08)',
  inset: 'inset 0 1px 0 rgba(255, 255, 255, 0.75)',
} as const;

export const breakpointTokens = {
  mobile: 0,
  tablet: 768,
  desktop: 1024,
  wide: 1440,
} as const;

export const statusTokens = {
  neutral: {
    fill: '#eef3f8',
    text: '#34485e',
    border: '#c5d2df',
    icon: '#4e6175',
  },
  info: {
    fill: '#e5efff',
    text: '#1f4db5',
    border: '#b9cbf4',
    icon: '#2f6fed',
  },
  warning: {
    fill: '#fff3df',
    text: '#8a5b00',
    border: '#f1d296',
    icon: '#c88400',
  },
  success: {
    fill: '#e4f5ec',
    text: '#1c6a3b',
    border: '#a8d8b9',
    icon: '#2f8f55',
  },
  critical: {
    fill: '#fdeceb',
    text: '#a3332d',
    border: '#f2b9b5',
    icon: '#d65045',
  },
} as const;

export const focusTokens = {
  outlineWidth: 3,
  outlineColor: '#2f6fed',
  outlineOffset: 2,
} as const;

declare module '@mui/material/styles' {
  interface Theme {
    claimManagerTokens: {
      color: typeof colorTokens;
      spacing: typeof spacingTokens;
      radius: typeof radiusTokens;
      elevation: typeof elevationTokens;
      breakpoint: typeof breakpointTokens;
      status: typeof statusTokens;
      focus: typeof focusTokens;
    };
  }

  interface ThemeOptions {
    claimManagerTokens?: Theme['claimManagerTokens'];
  }
}

const baseTheme = createTheme({
  breakpoints: {
    values: {
      xs: breakpointTokens.mobile,
      sm: breakpointTokens.tablet,
      md: breakpointTokens.desktop,
      lg: breakpointTokens.wide,
      xl: 1920,
    },
  },
  shape: {
    borderRadius: radiusTokens.md,
  },
  spacing: 8,
  typography: {
    fontFamily: '"Segoe UI Variable", "Segoe UI", "Source Sans 3", sans-serif',
    h1: {
      fontSize: '2.25rem',
      lineHeight: 1.1,
      fontWeight: 700,
      letterSpacing: '-0.03em',
    },
    h2: {
      fontSize: '1.75rem',
      lineHeight: 1.15,
      fontWeight: 700,
      letterSpacing: '-0.025em',
    },
    h3: {
      fontSize: '1.25rem',
      lineHeight: 1.2,
      fontWeight: 700,
    },
    subtitle1: {
      fontSize: '1rem',
      lineHeight: 1.5,
      fontWeight: 600,
    },
    body1: {
      fontSize: '1rem',
      lineHeight: 1.6,
    },
    body2: {
      fontSize: '0.95rem',
      lineHeight: 1.55,
    },
    button: {
      fontWeight: 600,
      letterSpacing: '0.01em',
      textTransform: 'none',
    },
    overline: {
      fontWeight: 700,
      fontSize: '0.78rem',
      letterSpacing: '0.12em',
      textTransform: 'uppercase',
    },
  },
});

export const appTheme = createTheme(baseTheme, {
  palette: {
    mode: 'light',
    primary: {
      main: colorTokens.accent,
      dark: colorTokens.accentStrong,
      light: colorTokens.accentSoft,
      contrastText: '#ffffff',
    },
    secondary: {
      main: colorTokens.highlight,
      light: colorTokens.highlightSoft,
      contrastText: '#ffffff',
    },
    background: {
      default: colorTokens.surface,
      paper: colorTokens.surfaceRaised,
    },
    text: {
      primary: colorTokens.ink,
      secondary: colorTokens.inkMuted,
    },
    divider: colorTokens.border,
    success: {
      main: statusTokens.success.icon,
      light: statusTokens.success.fill,
      dark: statusTokens.success.text,
      contrastText: '#ffffff',
    },
    warning: {
      main: statusTokens.warning.icon,
      light: statusTokens.warning.fill,
      dark: statusTokens.warning.text,
      contrastText: '#ffffff',
    },
    info: {
      main: statusTokens.info.icon,
      light: statusTokens.info.fill,
      dark: statusTokens.info.text,
      contrastText: '#ffffff',
    },
    error: {
      main: statusTokens.critical.icon,
      light: statusTokens.critical.fill,
      dark: statusTokens.critical.text,
      contrastText: '#ffffff',
    },
  },
  claimManagerTokens: {
    color: colorTokens,
    spacing: spacingTokens,
    radius: radiusTokens,
    elevation: elevationTokens,
    breakpoint: breakpointTokens,
    status: statusTokens,
    focus: focusTokens,
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        ':root': {
          colorScheme: 'light',
        },
        '*, *::before, *::after': {
          boxSizing: 'border-box',
        },
        body: {
          margin: 0,
          minWidth: 320,
          minHeight: '100vh',
          backgroundColor: colorTokens.surface,
          backgroundImage: [
            'radial-gradient(circle at top right, rgba(47, 111, 237, 0.12), transparent 30%)',
            'linear-gradient(180deg, #f7fafc 0%, #edf2f7 100%)',
          ].join(', '),
        },
        '#root': {
          minHeight: '100vh',
        },
        ':focus-visible': {
          outline: `${focusTokens.outlineWidth}px solid ${focusTokens.outlineColor}`,
          outlineOffset: `${focusTokens.outlineOffset}px`,
        },
      },
    },
    MuiButtonBase: {
      defaultProps: {
        disableRipple: false,
      },
      styleOverrides: {
        root: {
          borderRadius: radiusTokens.pill,
        },
      },
    },
    MuiButton: {
      styleOverrides: {
        root: {
          minHeight: 44,
          paddingInline: baseTheme.spacing(spacingTokens.md),
          boxShadow: 'none',
          '&:hover': {
            boxShadow: 'none',
          },
        },
      },
    },
    MuiCard: {
      styleOverrides: {
        root: {
          borderRadius: radiusTokens.lg,
          boxShadow: elevationTokens.panel,
          border: `1px solid ${alpha(colorTokens.ink, 0.08)}`,
        },
      },
    },
    MuiChip: {
      styleOverrides: {
        root: {
          fontWeight: 600,
          borderRadius: radiusTokens.pill,
        },
      },
    },
    MuiPaper: {
      styleOverrides: {
        root: {
          backgroundImage: 'none',
        },
        rounded: {
          borderRadius: radiusTokens.lg,
        },
      },
    },
    MuiTextField: {
      defaultProps: {
        fullWidth: true,
      },
    },
  },
});

export type AppTheme = typeof appTheme;