import { useEffect, useState } from 'react';
import {
  getVaultProfile,
  runCrossValidation,
  type CrossValidationReport,
  type VaultProfile,
} from '../api/client';

function healthColor(score: number): string {
  if (score >= 80) return 'var(--positive)';
  if (score >= 50) return 'var(--critical)';
  return 'var(--negative)';
}

function tierClass(tier: string): string {
  return tier === 'T1' ? 'badge badge--neutral' : 'badge badge--positive';
}

function severityClass(severity: string, passed: boolean): string {
  if (passed) return 'badge badge--positive';
  if (severity === 'Error') return 'badge badge--negative';
  return 'badge badge--critical';
}

export function VaultPage() {
  const [profile, setProfile] = useState<VaultProfile | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [report, setReport] = useState<CrossValidationReport | null>(null);
  const [validating, setValidating] = useState(false);

  useEffect(() => {
    getVaultProfile()
      .then(setProfile)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load vault'))
      .finally(() => setLoading(false));
  }, []);

  async function validate() {
    setValidating(true);
    setError(null);
    try {
      setReport(await runCrossValidation());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Cross-validation failed');
    } finally {
      setValidating(false);
    }
  }

  if (loading) {
    return (
      <main className="page">
        <p className="muted">Loading…</p>
      </main>
    );
  }

  if (!profile) {
    return (
      <main className="page">
        <div className="error-text">{error ?? 'No vault data.'}</div>
      </main>
    );
  }

  return (
    <main className="page">
      <div className="toolbar">
        <div>
          <h1 className="page__title">Identity Vault</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            One verified profile · {profile.documentCount} document
            {profile.documentCount === 1 ? '' : 's'} · T1 {profile.tiers.t1} · T2 {profile.tiers.t2}
          </p>
        </div>
        <div className="health">
          <div className="health__ring" style={{ borderColor: healthColor(profile.healthScore) }}>
            <span style={{ color: healthColor(profile.healthScore) }}>{profile.healthScore}</span>
          </div>
          <div className="muted">Health</div>
        </div>
      </div>

      <div className="vault-domains">
        {profile.domains.map((d) => (
          <div key={d.domainCode} className="card vault-card">
            <div className="vault-card__head">
              <div className="vault-card__name">{d.domainName}</div>
              <div className="muted">
                {d.documentCount} doc{d.documentCount === 1 ? '' : 's'}
              </div>
            </div>
            <div className="completion">
              <div className="completion__fill" style={{ width: `${d.completionPercent}%` }} />
            </div>
            <div className="muted" style={{ fontSize: 12, marginTop: 4 }}>
              {d.completionPercent}% complete
            </div>

            {d.fields.length > 0 && (
              <div className="vault-fields">
                {d.fields.map((f) => (
                  <div key={f.fieldName} className="vault-field-row">
                    <span className="vault-field-row__name">{f.fieldName}</span>
                    <span className="vault-field-row__val">{f.value ?? '—'}</span>
                    <span
                      className={tierClass(f.tier)}
                      title={`${f.sourceDocumentCount} source document(s)`}
                    >
                      {f.tier}
                    </span>
                  </div>
                ))}
              </div>
            )}
          </div>
        ))}
      </div>

      <div className="card" style={{ marginTop: 20, padding: 20 }}>
        <div className="toolbar" style={{ marginBottom: 12 }}>
          <div>
            <strong>Cross-validation</strong>
            <div className="muted" style={{ fontSize: 13 }}>
              Checks field consistency across all documents.
            </div>
          </div>
          <button className="btn btn--inline" onClick={validate} disabled={validating}>
            {validating ? 'Running…' : 'Run cross-validation'}
          </button>
        </div>

        {report && (
          <>
            <div className="muted" style={{ marginBottom: 12 }}>
              {report.passed}/{report.total} passed · {report.passRatePercent}% pass rate
            </div>
            {report.results.length === 0 && (
              <p className="muted">No applicable checks yet — capture more documents.</p>
            )}
            {report.results.map((r) => (
              <div key={r.ruleCode} className="xv-row">
                <span className={severityClass(r.severity, r.passed)}>
                  {r.passed ? 'Pass' : r.severity}
                </span>
                <span className="xv-row__name">{r.ruleName}</span>
                <span className="xv-row__msg muted">{r.message}</span>
              </div>
            ))}
          </>
        )}
      </div>

      {error && <div className="error-text" style={{ marginTop: 12 }}>{error}</div>}
    </main>
  );
}
