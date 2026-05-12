import { useEffect, useMemo } from 'react';
import SaveRounded from '@mui/icons-material/SaveRounded';
import { Button, Paper, Stack, TextField, Typography } from '@mui/material';
import { FormField } from '../../../shared/ui/FormField';
import { ValidationError } from '../../../shared/ui/ValidationError';
import { useClaimFormStore } from '../state/claimFormStore';
import type { ClaimFormErrors, ClaimFormValues } from '../types/Claim';

interface ClaimFormProps {
  mode: 'create' | 'edit';
  initialValues: ClaimFormValues;
  submitLabel: string;
  busy?: boolean;
  submitError?: string | null;
  onSubmit: (values: ClaimFormValues) => Promise<void> | void;
}

function validateForm(values: ClaimFormValues): ClaimFormErrors {
  const errors: ClaimFormErrors = {};

  if (!values.claimantName.trim()) {
    errors.claimantName = 'Claimant name is required.';
  }

  if (!values.claimantEmail.trim()) {
    errors.claimantEmail = 'Claimant email is required.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(values.claimantEmail.trim())) {
    errors.claimantEmail = 'Enter a valid email address.';
  }

  if (!values.claimantPhone.trim()) {
    errors.claimantPhone = 'Claimant phone is required.';
  }

  if (!values.policyNumber.trim()) {
    errors.policyNumber = 'Policy number is required.';
  }

  if (!values.lossDateUtc) {
    errors.lossDateUtc = 'Loss date is required.';
  }

  if (!values.lossType.trim()) {
    errors.lossType = 'Loss type is required.';
  }

  if (!values.lossDescription.trim()) {
    errors.lossDescription = 'Loss description is required.';
  }

  return errors;
}

export function ClaimForm({ mode, initialValues, submitLabel, busy = false, submitError, onSubmit }: ClaimFormProps) {
  const values = useClaimFormStore((state) => state.values);
  const errors = useClaimFormStore((state) => state.errors);
  const setFieldValue = useClaimFormStore((state) => state.setFieldValue);
  const setErrors = useClaimFormStore((state) => state.setErrors);
  const reset = useClaimFormStore((state) => state.reset);

  useEffect(() => {
    reset(initialValues);
  }, [initialValues, reset]);

  const sectionTitle = useMemo(() => (mode === 'create' ? 'Create claim file' : 'Update claim file'), [mode]);

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const nextErrors = validateForm(values);
    if (Object.keys(nextErrors).length > 0) {
      setErrors(nextErrors);
      return;
    }

    await onSubmit(values);
  };

  return (
    <Paper component="form" onSubmit={handleSubmit} sx={{ p: { xs: 3, md: 4 } }} noValidate>
      <Stack spacing={3}>
        <div>
          <Typography variant="overline" color="text.secondary">
            {mode === 'create' ? 'Claim intake' : 'Claim maintenance'}
          </Typography>
          <Typography variant="h2">{sectionTitle}</Typography>
          <Typography color="text.secondary" sx={{ maxWidth: 720 }}>
            Capture claimant, claim, and loss information with accessible field-level validation and keyboard-friendly layout.
          </Typography>
        </div>

        <ValidationError message={submitError ?? undefined} />

        <Stack component="section" spacing={2.5} aria-label="Claimant information">
          <Typography variant="h3">Claimant information</Typography>
          <Stack spacing={2}>
            <FormField label="Claimant name">
              <TextField
                label="Claimant name"
                value={values.claimantName}
                onChange={(event) => setFieldValue('claimantName', event.target.value)}
                error={Boolean(errors.claimantName)}
                helperText={errors.claimantName}
                name="claimantName"
                fullWidth
                autoComplete="name"
              />
            </FormField>

            <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
              <FormField label="Claimant email">
                <TextField
                  label="Claimant email"
                  value={values.claimantEmail}
                  onChange={(event) => setFieldValue('claimantEmail', event.target.value)}
                  error={Boolean(errors.claimantEmail)}
                  helperText={errors.claimantEmail}
                  name="claimantEmail"
                  type="email"
                  fullWidth
                  autoComplete="email"
                />
              </FormField>

              <FormField label="Claimant phone">
                <TextField
                  label="Claimant phone"
                  value={values.claimantPhone}
                  onChange={(event) => setFieldValue('claimantPhone', event.target.value)}
                  error={Boolean(errors.claimantPhone)}
                  helperText={errors.claimantPhone}
                  name="claimantPhone"
                  fullWidth
                  autoComplete="tel"
                />
              </FormField>
            </Stack>
          </Stack>
        </Stack>

        <Stack component="section" spacing={2.5} aria-label="Claim details">
          <Typography variant="h3">Claim details</Typography>
          <FormField label="Policy number">
            <TextField
              label="Policy number"
              value={values.policyNumber}
              onChange={(event) => setFieldValue('policyNumber', event.target.value)}
              error={Boolean(errors.policyNumber)}
              helperText={errors.policyNumber}
              name="policyNumber"
              fullWidth
            />
          </FormField>
        </Stack>

        <Stack component="section" spacing={2.5} aria-label="Loss information">
          <Typography variant="h3">Loss information</Typography>
          <Stack spacing={2}>
            <Stack direction={{ xs: 'column', md: 'row' }} spacing={2}>
              <FormField label="Loss date">
                <TextField
                  label="Loss date"
                  value={values.lossDateUtc}
                  onChange={(event) => setFieldValue('lossDateUtc', event.target.value)}
                  error={Boolean(errors.lossDateUtc)}
                  helperText={errors.lossDateUtc}
                  name="lossDateUtc"
                  type="date"
                  fullWidth
                  slotProps={{ inputLabel: { shrink: true } }}
                />
              </FormField>

              <FormField label="Loss type">
                <TextField
                  label="Loss type"
                  value={values.lossType}
                  onChange={(event) => setFieldValue('lossType', event.target.value)}
                  error={Boolean(errors.lossType)}
                  helperText={errors.lossType}
                  name="lossType"
                  fullWidth
                />
              </FormField>
            </Stack>

            <FormField label="Loss description">
              <TextField
                label="Loss description"
                value={values.lossDescription}
                onChange={(event) => setFieldValue('lossDescription', event.target.value)}
                error={Boolean(errors.lossDescription)}
                helperText={errors.lossDescription}
                name="lossDescription"
                fullWidth
                multiline
                minRows={4}
              />
            </FormField>
          </Stack>
        </Stack>

        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ justifyContent: 'flex-end' }}>
          <Button type="submit" variant="contained" startIcon={<SaveRounded />} disabled={busy}>
            {submitLabel}
          </Button>
        </Stack>
      </Stack>
    </Paper>
  );
}