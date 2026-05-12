import { create } from 'zustand';
import type { ClaimFormErrors, ClaimFormValues } from '../types/Claim';
import { emptyClaimFormValues } from '../types/Claim';

interface ClaimFormState {
  values: ClaimFormValues;
  errors: ClaimFormErrors;
  setFieldValue: <TField extends keyof ClaimFormValues>(field: TField, value: ClaimFormValues[TField]) => void;
  setErrors: (errors: ClaimFormErrors) => void;
  reset: (values?: ClaimFormValues) => void;
}

export const useClaimFormStore = create<ClaimFormState>((set) => ({
  values: emptyClaimFormValues,
  errors: {},
  setFieldValue: (field, value) =>
    set((state) => ({
      values: {
        ...state.values,
        [field]: value,
      },
      errors: {
        ...state.errors,
        [field]: undefined,
      },
    })),
  setErrors: (errors) => set({ errors }),
  reset: (values = emptyClaimFormValues) => set({ values, errors: {} }),
}));