# Testing

- Write tests for all critical business logic.
- Prefer integration tests over unit tests with excessive mocking.
- Do not mock the database unless strictly necessary.
- Tests must be able to run independently (in any order).
- Each test should have a single logical assert.
- Naming: describe what is tested, not how: `it('should return 0 for empty invoice')`.
