import { create } from 'zustand';

interface ShellState {
  navigationOpen: boolean;
  openNavigation: () => void;
  closeNavigation: () => void;
  toggleNavigation: () => void;
}

export const useShellStore = create<ShellState>((set) => ({
  navigationOpen: false,
  openNavigation: () => set({ navigationOpen: true }),
  closeNavigation: () => set({ navigationOpen: false }),
  toggleNavigation: () => set((state) => ({ navigationOpen: !state.navigationOpen })),
}));