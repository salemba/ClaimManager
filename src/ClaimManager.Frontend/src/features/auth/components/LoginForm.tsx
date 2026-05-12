import { useState } from 'react';
import type { FormEvent } from 'react';
import LockOutlined from '@mui/icons-material/LockOutlined';
import TipsAndUpdatesOutlined from '@mui/icons-material/TipsAndUpdatesOutlined';
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  List,
  ListItem,
  ListItemIcon,
  ListItemText,
  Stack,
  TextField,
  Typography,
} from '@mui/material';
import { useNavigate } from 'react-router-dom';
import { ApiError } from '../../../shared/api/client';
import { PageSurface } from '../../../shared/ui/PageSurface';
import { login } from '../api/authApi';

export function LoginForm() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('adjuster@claimmanager.local');
  const [password, setPassword] = useState('Adjuster!2345');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      await login(email, password);
      navigate('/');
    } catch (caughtError) {
      if (caughtError instanceof ApiError) {
        setError(caughtError.message);
      } else {
        setError('Sign-in failed.');
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <PageSurface centered maxWidth={560}>
      <Card className="auth-card" component="section">
        <CardContent sx={{ p: { xs: 3, md: 4 } }}>
          <Box component="form" onSubmit={handleSubmit} noValidate>
            <Stack spacing={3}>
              <Stack spacing={1.5}>
                <Typography variant="overline" color="text.secondary">
                  Carrier claims access
                </Typography>
                <Typography variant="h1">Sign in to ClaimManager</Typography>
                <Typography color="text.secondary">
                  Use the seeded internal accounts to verify the authenticated shell and policy-protected APIs.
                </Typography>
              </Stack>

              <TextField
                label="Email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                autoComplete="username"
              />

              <TextField
                label="Password"
                type="password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                autoComplete="current-password"
              />

              {error ? <Alert severity="error">{error}</Alert> : null}

              <Button type="submit" variant="contained" size="large" disabled={submitting} startIcon={<LockOutlined />}>
                {submitting ? 'Signing in...' : 'Sign in'}
              </Button>

              <Box component="section" aria-label="Seeded accounts" sx={{ p: 2.5, borderRadius: 3, bgcolor: 'background.default' }}>
                <Typography variant="subtitle1">Seeded accounts</Typography>
                <List dense disablePadding sx={{ mt: 1 }}>
                  <ListItem disableGutters>
                    <ListItemIcon sx={{ minWidth: 36 }}>
                      <TipsAndUpdatesOutlined color="primary" fontSize="small" />
                    </ListItemIcon>
                    <ListItemText primary="Adjuster" secondary="adjuster@claimmanager.local / Adjuster!2345" />
                  </ListItem>
                  <ListItem disableGutters>
                    <ListItemIcon sx={{ minWidth: 36 }}>
                      <TipsAndUpdatesOutlined color="primary" fontSize="small" />
                    </ListItemIcon>
                    <ListItemText primary="Admin" secondary="admin@claimmanager.local / Admin!234567" />
                  </ListItem>
                </List>
              </Box>
            </Stack>
          </Box>
        </CardContent>
      </Card>
    </PageSurface>
  );
}