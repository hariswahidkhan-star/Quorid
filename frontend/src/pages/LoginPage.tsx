import { useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

type Mode = 'login' | 'register';

export function LoginPage() {
  const { login, register } = useAuth();
  const navigate = useNavigate();

  const [mode, setMode] = useState<Mode>('login');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [tenantName, setTenantName] = useState('');
  const [entityName, setEntityName] = useState('');
  const [fullName, setFullName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      if (mode === 'login') {
        await login(email, password);
      } else {
        await register({ tenantName, entityName, fullName, email, password });
      }
      navigate('/');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Something went wrong.');
    } finally {
      setBusy(false);
    }
  }

  function switchMode(next: Mode) {
    setMode(next);
    setError(null);
  }

  return (
    <div className="auth">
      <form className="auth__card" onSubmit={handleSubmit}>
        <div className="auth__logo">
          QUOR<span>ID</span>
        </div>
        <div className="auth__tagline">Verified once. Trusted everywhere.</div>

        {error && <div className="error-text">{error}</div>}

        {mode === 'register' && (
          <>
            <div className="field">
              <label>Company / Tenant name</label>
              <input value={tenantName} onChange={(e) => setTenantName(e.target.value)} required />
            </div>
            <div className="field">
              <label>Primary entity name</label>
              <input value={entityName} onChange={(e) => setEntityName(e.target.value)} required />
            </div>
            <div className="field">
              <label>Your full name</label>
              <input value={fullName} onChange={(e) => setFullName(e.target.value)} required />
            </div>
          </>
        )}

        <div className="field">
          <label>Email</label>
          <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} required />
        </div>
        <div className="field">
          <label>Password</label>
          <input
            type="password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </div>

        <button className="btn" type="submit" disabled={busy}>
          {busy ? 'Please wait…' : mode === 'login' ? 'Sign in' : 'Create account'}
        </button>

        <p className="muted" style={{ marginTop: 16, textAlign: 'center' }}>
          {mode === 'login' ? (
            <>
              No account?{' '}
              <a
                href="#"
                onClick={(e) => {
                  e.preventDefault();
                  switchMode('register');
                }}
              >
                Register a tenant
              </a>
            </>
          ) : (
            <>
              Already have an account?{' '}
              <a
                href="#"
                onClick={(e) => {
                  e.preventDefault();
                  switchMode('login');
                }}
              >
                Sign in
              </a>
            </>
          )}
        </p>
      </form>
    </div>
  );
}
