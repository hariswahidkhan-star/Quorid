import { useCallback, useEffect, useState } from 'react';
import {
  getComplianceCalendar,
  getComplianceOverview,
  getFrameworkStatus,
  type ComplianceCalendarItem,
  type ComplianceOverview,
  type FrameworkStatus,
} from '../api/client';

function scoreColor(s: number): string {
  if (s >= 80) return 'var(--positive)';
  if (s >= 50) return 'var(--critical)';
  return 'var(--negative)';
}

function requirementBadge(status: string): string {
  if (status === 'present') return 'badge badge--positive';
  if (status === 'expired') return 'badge badge--critical';
  return 'badge badge--negative';
}

export function CompliancePage() {
  const [overview, setOverview] = useState<ComplianceOverview | null>(null);
  const [calendar, setCalendar] = useState<ComplianceCalendarItem[]>([]);
  const [selected, setSelected] = useState<FrameworkStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getComplianceOverview()
      .then(setOverview)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load compliance'))
      .finally(() => setLoading(false));
    getComplianceCalendar()
      .then(setCalendar)
      .catch(() => undefined);
  }, []);

  const openFramework = useCallback((id: string) => {
    getFrameworkStatus(id)
      .then(setSelected)
      .catch(() => undefined);
  }, []);

  if (loading) {
    return (
      <main className="page">
        <p className="muted">Loading…</p>
      </main>
    );
  }

  if (!overview) {
    return (
      <main className="page">
        <div className="error-text">{error ?? 'No compliance data.'}</div>
      </main>
    );
  }

  return (
    <main className="page">
      <div className="toolbar">
        <div>
          <h1 className="page__title">Compliance</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            Framework readiness, gap analysis, and renewal deadlines.
          </p>
        </div>
        <div className="health">
          <div className="health__ring" style={{ borderColor: scoreColor(overview.overallScore) }}>
            <span style={{ color: scoreColor(overview.overallScore) }}>{overview.overallScore}</span>
          </div>
          <div className="muted">Overall</div>
        </div>
      </div>

      <div className="tile-grid">
        {overview.frameworks.map((f) => (
          <div key={f.frameworkId} className="tile" onClick={() => openFramework(f.frameworkId)}>
            <div className="tile__title">{f.name}</div>
            <div className="completion">
              <div className="completion__fill" style={{ width: `${f.score}%`, background: scoreColor(f.score) }} />
            </div>
            <div className="tile__desc">
              {f.present}/{f.total} requirements · {f.score}%
            </div>
          </div>
        ))}
      </div>

      {selected && (
        <div className="card" style={{ marginTop: 20, padding: 20 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
            <h2 style={{ margin: 0, fontSize: 18 }}>{selected.name} — gap analysis</h2>
            <span style={{ color: scoreColor(selected.score), fontWeight: 700 }}>{selected.score}%</span>
          </div>
          <table className="table" style={{ marginTop: 12 }}>
            <thead>
              <tr>
                <th>Requirement</th>
                <th>Status</th>
                <th>Evidence</th>
              </tr>
            </thead>
            <tbody>
              {selected.requirements.map((r) => (
                <tr key={r.requirementId}>
                  <td>{r.name}</td>
                  <td>
                    <span className={requirementBadge(r.status)}>{r.status}</span>
                  </td>
                  <td className="muted">{r.documentTitle ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <div className="card" style={{ marginTop: 20, padding: 20 }}>
        <strong>Compliance calendar</strong>
        <div className="muted" style={{ fontSize: 13, marginBottom: 8 }}>
          Documents satisfying requirements, by renewal date.
        </div>
        {calendar.length === 0 && <p className="muted">No dated compliance documents yet.</p>}
        {calendar.map((c, i) => (
          <div key={`${c.documentId}-${i}`} className="activity-row">
            <span>
              {c.documentTitle}{' '}
              <span className="muted">
                — {c.frameworkName}: {c.requirementName}
              </span>
            </span>
            <span className={c.daysUntilExpiry < 30 ? 'badge badge--critical' : 'badge badge--neutral'}>
              {c.expiryDate} ({c.daysUntilExpiry}d)
            </span>
          </div>
        ))}
      </div>
    </main>
  );
}
