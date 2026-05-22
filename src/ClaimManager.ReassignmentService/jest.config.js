/** @type {import('ts-jest').JestConfigWithTsJest} */
module.exports = {
  preset: 'ts-jest',
  testEnvironment: 'node',
  testMatch: ['**/tests/**/*.spec.ts'],
  moduleNameMapper: {
    '^../domain/(.*)$': '<rootDir>/src/domain/$1',
    '^../use-cases/(.*)$': '<rootDir>/src/use-cases/$1',
  },
};
