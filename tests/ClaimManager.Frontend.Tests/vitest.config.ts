import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vitest/config';

const rootDir = path.dirname(fileURLToPath(import.meta.url));
const reactDir = path.resolve(rootDir, 'node_modules/react');
const reactDomDir = path.resolve(rootDir, 'node_modules/react-dom');
const muiDir = path.resolve(rootDir, 'node_modules/@mui/material');
const muiIconsDir = path.resolve(rootDir, 'node_modules/@mui/icons-material');
const emotionReactDir = path.resolve(rootDir, 'node_modules/@emotion/react');
const emotionStyledDir = path.resolve(rootDir, 'node_modules/@emotion/styled');
const queryDir = path.resolve(rootDir, 'node_modules/@tanstack/react-query');
const routerDir = path.resolve(rootDir, 'node_modules/react-router-dom');
const zustandDir = path.resolve(rootDir, 'node_modules/zustand');

export default defineConfig({
  resolve: {
    alias: {
      react: reactDir,
      'react-dom': reactDomDir,
      'react/jsx-runtime': path.resolve(reactDir, 'jsx-runtime.js'),
      'react/jsx-dev-runtime': path.resolve(reactDir, 'jsx-dev-runtime.js'),
      '@mui/material': muiDir,
      '@mui/icons-material': muiIconsDir,
      '@emotion/react': emotionReactDir,
      '@emotion/styled': emotionStyledDir,
      '@tanstack/react-query': queryDir,
      'react-router-dom': routerDir,
      zustand: zustandDir,
    },
    dedupe: ['react', 'react-dom', '@mui/material', '@tanstack/react-query', 'react-router-dom'],
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/vitest.setup.ts'],
  },
});