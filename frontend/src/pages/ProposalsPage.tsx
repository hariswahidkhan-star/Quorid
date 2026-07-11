import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  createProposal,
  getProposalAnalytics,
  listProposals,
  updateProposal,
  type ProposalAnalytics,
  type ProposalListItem,
} from '../api/client';

const STAGES = ['Lead', 'Qualified', 'Proposal', 'Negotiation', 'Won', 'Lost'];

export function ProposalsPage() {
  const navigate = useNavigate();
  const [proposals, setProposals] = useState<ProposalListItem[]>([]);
  const [analytics, setAnalytics] = useState<ProposalAnalytics | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);
  const [dragId, setDragId] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    listProposals()
      .then(setProposals)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
    getProposalAnalytics().then(setAnalytics).catch(() => undefined);
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function moveTo(id: string, stage: string) {
    setProposals((prev) => prev.map((p) => (p.id === id ? { ...p, stage } : p)));
    await updateProposal(id, { stage });
    getProposalAnalytics().then(setAnalytics).catch(() => undefined);
  }

  return (
    <main className="page" style={{ maxWidth: 1400 }}>
      <div className="toolbar">
        <div>
          <h1 className="page__title">Proposals</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>Opportunity pipeline and proposal builder.</p>
        </div>
        <button className="btn btn--inline" onClick={() => setShowCreate(true)}>
          + New Proposal
        </button>
      </div>

      {analytics && (
        <div className="stat-row">
          <div className="stat">
            <div className="stat__value">{analytics.winRatePercent}%</div>
            <div className="stat__label">Win rate</div>
          </div>
          <div className="stat">
            <div className="stat__value">${analytics.pipelineValue.toLocaleString()}</div>
            <div className="stat__label">Pipeline</div>
          </div>
          <div className="stat">
            <div className="stat__value">${analytics.wonValue.toLocaleString()}</div>
            <div className="stat__label">Won value</div>
          </div>
          <div className="stat">
            <div className="stat__value">{analytics.open}</div>
            <div className="stat__label">Open</div>
          </div>
        </div>
      )}

      {loading && <p className="muted">Loading…</p>}
      {error && <div className="error-text">{error}</div>}

      <div className="kanban">
        {STAGES.map((stage) => (
          <div
            key={stage}
            className="kanban__col"
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => {
              e.preventDefault();
              if (dragId) {
                moveTo(dragId, stage);
                setDragId(null);
              }
            }}
          >
            <div className="kanban__head">
              {stage} <span className="muted">({proposals.filter((p) => p.stage === stage).length})</span>
            </div>
            {proposals
              .filter((p) => p.stage === stage)
              .map((p) => (
                <div
                  key={p.id}
                  className="kanban__card"
                  draggable
                  onDragStart={() => setDragId(p.id)}
                  onClick={() => navigate(`/proposals/${p.id}`)}
                >
                  <div style={{ fontWeight: 600, fontSize: 14 }}>{p.title}</div>
                  {p.recipientName && <div className="muted" style={{ fontSize: 12 }}>{p.recipientName}</div>}
                  {p.value != null && (
                    <div style={{ fontSize: 13, color: 'var(--positive)' }}>${p.value.toLocaleString()}</div>
                  )}
                  <div className="muted" style={{ fontSize: 12 }}>{p.documentCount} docs</div>
                </div>
              ))}
          </div>
        ))}
      </div>

      {showCreate && (
        <CreateProposalModal onClose={() => setShowCreate(false)} onCreated={(id) => navigate(`/proposals/${id}`)} />
      )}
    </main>
  );
}

function CreateProposalModal({ onClose, onCreated }: { onClose: () => void; onCreated: (id: string) => void }) {
  const [title, setTitle] = useState('');
  const [recipient, setRecipient] = useState('');
  const [value, setValue] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function create() {
    if (!title) return;
    setBusy(true);
    setError(null);
    try {
      const numeric = value ? Number(value) : undefined;
      const p = await createProposal({
        title,
        recipientName: recipient || undefined,
        value: Number.isFinite(numeric) ? numeric : undefined,
        dueDate: dueDate || undefined,
      });
      onCreated(p.id);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Create failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal__header">
          <h2>New Proposal</h2>
          <button className="modal__close" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>
        <div className="field">
          <label>Title</label>
          <input value={title} onChange={(e) => setTitle(e.target.value)} />
        </div>
        <div className="field">
          <label>Recipient</label>
          <input value={recipient} onChange={(e) => setRecipient(e.target.value)} />
        </div>
        <div className="form-grid">
          <div className="field">
            <label>Value ($)</label>
            <input type="number" value={value} onChange={(e) => setValue(e.target.value)} />
          </div>
          <div className="field">
            <label>Due date</label>
            <input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
          </div>
        </div>
        {error && <div className="error-text">{error}</div>}
        <div className="modal__footer">
          <button className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancel
          </button>
          <button className="btn" onClick={create} disabled={busy || !title}>
            {busy ? 'Creating…' : 'Create'}
          </button>
        </div>
      </div>
    </div>
  );
}
