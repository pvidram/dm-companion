# Security

- Never hardcode credentials, tokens or API keys in code.
- Sensitive variables always go in `.env` (and `.env` is never committed).
- Validate ALL user input at the system boundaries (API endpoints, forms).
- Use prepared statements for SQL, never string concatenation.
- Do not expose stack traces or internal errors to the client.
- HTTPS always in production.
- Sanitise data before rendering (XSS prevention).
