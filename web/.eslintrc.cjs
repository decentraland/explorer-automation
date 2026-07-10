/** @type {import("eslint").Linter.Config} */
module.exports = {
  root: true,
  parser: '@typescript-eslint/parser',
  parserOptions: {
    ecmaVersion: 2022,
    sourceType: 'module',
    tsconfigRootDir: __dirname,
    project: './tsconfig.json'
  },
  plugins: ['@typescript-eslint', 'playwright'],
  extends: ['eslint:recommended', 'plugin:@typescript-eslint/recommended', 'plugin:playwright/recommended', 'prettier'],
  rules: {
    '@typescript-eslint/no-floating-promises': 'error',
    '@typescript-eslint/no-misused-promises': 'error',
    'playwright/no-skipped-test': 'warn',
    'playwright/no-conditional-in-test': 'warn'
  },
  overrides: [
    {
      // Specs must take `test` from shared/fixtures/base-test.ts (or a wallet
      // fixture) so the CF Access route is installed — a spec on plain
      // `@playwright/test` silently loses the headers on `.zone` / `.today`
      // and times out on blank pages. Types and `expect` alone are still fine
      // to import, but base-test re-exports `expect` so one import covers both.
      files: ['tests/**/*.spec.ts'],
      rules: {
        'no-restricted-imports': [
          'error',
          {
            paths: [
              {
                name: '@playwright/test',
                importNames: ['test'],
                message:
                  'Import `test` from shared/fixtures/base-test.js (or a wallet fixture) so the CF Access route is installed for .zone/.today runs.'
              }
            ]
          }
        ]
      }
    }
  ],
  ignorePatterns: ['node_modules/', 'dist/', 'playwright-report/', 'test-results/', 'allure-results/', '.eslintrc.cjs']
}
