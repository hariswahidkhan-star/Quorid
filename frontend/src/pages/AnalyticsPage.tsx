import { useCallback, useEffect, useState } from 'react';
import {
  askQuorid,
  getDashboard,
  listReports,
  runReport,
  type AskResponse,
  type Breakdown,
  type Dashboard,
  type ReportResult,
  type ReportSummary,
} from '../api/client';

type Tab = 'dashboard' | 'ask' | 'reports';
const TABS: { key: Tab; label: string }[] = [
  { key: 'dashboard', label: 'Dashboard' },
  { key: 'ask', label: 'Ask Quorid' },
  { key: 'reports', label: 'Reports' },
];

export function AnalyticsPage() {
  const [tab, setTab] = useState<Tab>('dashboard');

  return (
    <main className="page" style={{ maxWidth: 1200 }}>
      <div className="toolbar">
        <div>
          <h1 className="page__title">Analytics</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            Live metrics, natural-language answers, and 18 pre-built reports.
          </p>
        </div>
      </div>

      <div className="tabs">
        {TABS.map((t) => (
          <button
            key={t.key}
            className={`tab${tab === t.key ? ' tab--active' : ''}`}
            onClick={() => setTab(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'dashboard' && <DashboardTab />}
      {tab === 'ask' && <AskTab />}
      {tab === 'reports' && <ReportsTab />}
    </main>
  );
}

// ---- Dashboard ----

function DashboardTab() {
  const [data, setData] = useState<Dashboard | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getDashboard()
      .then(setData)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'));
  }, []);

  if (error) return <div className="error-text">{error}</div>;
  if (!data) return <p className="muted">Loading…</p>;

  return (
    <>
      <div className="kpi-grid">
        {data.kpis.map((k) => (
          <div key={k.key} className="kpi">
            <div className="kpi__label">{k.label}</div>
            <div className={`kpi__value kpi__value--${k.tone}`}>{k.value}</div>
            {k.sublabel && <div className="kpi__sub">{k.sublabel}</div>}
          </div>
        ))}
      </div>

      <div className="analytics-cols">
        {data.breakdowns.map((b) => (
          <BreakdownCard key={b.key} breakdown={b} />
        ))}
      </div>

      <div className="card" style={{ padding: 20, marginTop: 16 }}>
        <strong>Recent activity</strong>
        {data.recentActivity.length === 0 && <p className="muted">No activity yet.</p>}
        {data.recentActivity.map((a, i) => (
          <div key={i} className="activity-row">
            <span>
              <span className="badge badge--neutral">{a.resourceType}</span> {humanize(a.action)}
            </span>
            <span className="muted" style={{ fontSize: 12 }}>{formatDate(a.at)}</span>
          </div>
        ))}
      </div>
    </>
  );
}

function BreakdownCard({ breakdown }: { breakdown: Breakdown }) {
  const max = Math.max(1, ...breakdown.slices.map((s) => s.count));
  return (
    <div className="card" style={{ padding: 20 }}>
      <strong>{breakdown.title}</strong>
      <div style={{ marginTop: 12 }}>
        {breakdown.slices.every((s) => s.count === 0) && <p className="muted">No data yet.</p>}
        {breakdown.slices.map((s) => (
          <div key={s.label} className="bar-row">
            <div className="bar-row__label">{s.label}</div>
            <div className="bar-row__track">
              <div className="bar-row__fill" style={{ width: `${(s.count / max) * 100}%` }} />
            </div>
            <div className="bar-row__count">{s.count}</div>
          </div>
        ))}
      </div>
    </div>
  );
}

// ---- Ask Quorid ----

const STARTERS = [
  'How many documents are expiring soon?',
  'What is our proposal win rate?',
  'How verified is our vault?',
  'What is our compliance readiness?',
];

function AskTab() {
  const [question, setQuestion] = useState('');
  const [answer, setAnswer] = useState<AskResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const ask = useCallback(async (q: string) => {
    if (!q.trim()) return;
    setBusy(true);
    setError(null);
    try {
      setAnswer(await askQuorid(q));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Ask failed');
    } finally {
      setBusy(false);
    }
  }, []);

  return (
    <div style={{ maxWidth: 760 }}>
      <div className="card" style={{ padding: 20 }}>
        <div className="ask-bar">
          <span className="ask-bar__spark">✨</span>
          <input
            className="ask-bar__input"
            placeholder="Ask about your documents, pipeline, rooms, compliance…"
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && ask(question)}
          />
          <button className="btn btn--inline" onClick={() => ask(question)} disabled={busy || !question.trim()}>
            {busy ? 'Thinking…' : 'Ask'}
          </button>
        </div>

        <div className="ask-starters">
          {STARTERS.map((s) => (
            <button
              key={s}
              className="ask-chip"
              onClick={() => {
                setQuestion(s);
                ask(s);
              }}
            >
              {s}
            </button>
          ))}
        </div>
      </div>

      {error && <div className="error-text" style={{ marginTop: 16 }}>{error}</div>}

      {answer && (
        <div className="card ask-answer" style={{ padding: 20, marginTop: 16 }}>
          {answer.metric && (
            <div className="ask-answer__metric">
              <span className="ask-answer__value">{answer.value ?? '—'}</span>
              <span className="ask-answer__label">{answer.metric}</span>
            </div>
          )}
          <p className="ask-answer__text">{answer.answer}</p>
          {answer.breakdown.length > 0 && (
            <BreakdownCard
              breakdown={{ key: 'ask', title: 'Breakdown', slices: answer.breakdown }}
            />
          )}
        </div>
      )}
    </div>
  );
}

// ---- Reports ----

function ReportsTab() {
  const [reports, setReports] = useState<ReportSummary[]>([]);
  const [active, setActive] = useState<ReportResult | null>(null);
  const [loadingKey, setLoadingKey] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    listReports()
      .then(setReports)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'));
  }, []);

  async function open(key: string) {
    setLoadingKey(key);
    setError(null);
    try {
      setActive(await runReport(key));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Report failed');
    } finally {
      setLoadingKey(null);
    }
  }

  const categories = [...new Set(reports.map((r) => r.category))];

  if (active) {
    return (
      <div>
        <button className="btn btn--ghost btn--inline" onClick={() => setActive(null)}>
          ← All reports
        </button>
        <div className="toolbar" style={{ marginTop: 12 }}>
          <div>
            <h2 style={{ margin: 0 }}>{active.title}</h2>
            <p className="muted" style={{ margin: 0 }}>
              {active.category} · {active.totalRows} row{active.totalRows === 1 ? '' : 's'}
            </p>
          </div>
        </div>
        <div className="card" style={{ padding: 0, marginTop: 12, overflowX: 'auto' }}>
          <table className="table">
            <thead>
              <tr>
                {active.columns.map((c) => (
                  <th key={c}>{c}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {active.rows.length === 0 && (
                <tr>
                  <td colSpan={active.columns.length} className="muted" style={{ padding: 20 }}>
                    No data for this report yet.
                  </td>
                </tr>
              )}
              {active.rows.map((row, i) => (
                <tr key={i}>
                  {row.map((cell, j) => (
                    <td key={j}>{cell}</td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    );
  }

  return (
    <div>
      {error && <div className="error-text">{error}</div>}
      {categories.map((cat) => (
        <div key={cat} style={{ marginBottom: 24 }}>
          <div className="report-cat">{cat}</div>
          <div className="report-grid">
            {reports
              .filter((r) => r.category === cat)
              .map((r) => (
                <button
                  key={r.key}
                  className="report-card"
                  onClick={() => open(r.key)}
                  disabled={loadingKey === r.key}
                >
                  <div className="report-card__title">{r.title}</div>
                  <div className="report-card__desc">{r.description}</div>
                  <div className="report-card__cta">
                    {loadingKey === r.key ? 'Loading…' : 'Run report →'}
                  </div>
                </button>
              ))}
          </div>
        </div>
      ))}
    </div>
  );
}

// ---- helpers ----

function humanize(action: string): string {
  return action
    .toLowerCase()
    .replace(/_/g, ' ')
    .replace(/^\w/, (c) => c.toUpperCase());
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
}
