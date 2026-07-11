import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { createEngagement, listEngagements, type EngagementListItem } from '../api/client';

interface Props {
  type: string;
  title: string;
  subtitle: string;
}

export function EngagementsPage({ type, title, subtitle }: Props) {
  const navigate = useNavigate();
  const [items, setItems] = useState<EngagementListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreate, setShowCreate] = useState(false);

  const load = useCallback(() => {
    setLoading(true);
    listEngagements(type)
      .then(setItems)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [type]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <main className="page">
      <div className="toolbar">
        <div>
          <h1 className="page__title">{title}</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>{subtitle}</p>
        </div>
        <button className="btn btn--inline" onClick={() => setShowCreate(true)}>
          + New {type}
        </button>
      </div>

      {loading && <p className="muted">Loading…</p>}
      {error && <div className="error-text">{error}</div>}

      {!loading && items.length === 0 && (
        <div className="card" style={{ padding: 32, textAlign: 'center' }}>
          <p className="muted">
            None yet. Click <strong>+ New {type}</strong> to add one.
          </p>
        </div>
      )}

      {items.length > 0 && (
        <div className="card">
          <table className="table">
            <thead>
              <tr>
                <th>Name</th>
                {type !== 'Project' && <th>Contact</th>}
                <th>Status</th>
                <th>Progress</th>
                <th>Due</th>
              </tr>
            </thead>
            <tbody>
              {items.map((e) => (
                <tr key={e.id} className="clickable" onClick={() => navigate(`/engagements/${e.id}`)}>
                  <td>{e.name}</td>
                  {type !== 'Project' && <td>{e.contactEmail ?? '—'}</td>}
                  <td>
                    <span className="badge badge--neutral">{e.status}</span>
                  </td>
                  <td>
                    <div className="completion" style={{ width: 120 }}>
                      <div className="completion__fill" style={{ width: `${e.progress}%` }} />
                    </div>
                    <span className="muted" style={{ fontSize: 12 }}>
                      {e.received}/{e.total}
                    </span>
                  </td>
                  <td>{e.dueDate ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showCreate && (
        <CreateModal type={type} onClose={() => setShowCreate(false)} onCreated={(id) => navigate(`/engagements/${id}`)} />
      )}
    </main>
  );
}

function CreateModal({ type, onClose, onCreated }: { type: string; onClose: () => void; onCreated: (id: string) => void }) {
  const [name, setName] = useState('');
  const [contactEmail, setContactEmail] = useState('');
  const [dueDate, setDueDate] = useState('');
  const [checklistText, setChecklistText] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function create() {
    if (!name) return;
    setBusy(true);
    setError(null);
    try {
      const checklist = checklistText
        .split('\n')
        .map((l) => l.trim())
        .filter(Boolean)
        .map((n) => ({ name: n }));
      const e = await createEngagement({
        type,
        name,
        contactEmail: contactEmail || undefined,
        dueDate: dueDate || undefined,
        checklist,
      });
      onCreated(e.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Create failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal__header">
          <h2>New {type}</h2>
          <button className="modal__close" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>
        <div className="field">
          <label>Name</label>
          <input value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        {type !== 'Project' && (
          <div className="field">
            <label>Contact email</label>
            <input value={contactEmail} onChange={(e) => setContactEmail(e.target.value)} />
          </div>
        )}
        <div className="field">
          <label>Due date</label>
          <input type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
        </div>
        <div className="field">
          <label>Required documents (one per line)</label>
          <textarea
            value={checklistText}
            onChange={(e) => setChecklistText(e.target.value)}
            rows={5}
            style={{
              width: '100%',
              padding: '8px 10px',
              border: '1px solid var(--border)',
              borderRadius: 6,
              fontFamily: 'inherit',
              resize: 'vertical',
            }}
          />
        </div>
        {error && <div className="error-text">{error}</div>}
        <div className="modal__footer">
          <button className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancel
          </button>
          <button className="btn" onClick={create} disabled={busy || !name}>
            {busy ? 'Creating…' : 'Create'}
          </button>
        </div>
      </div>
    </div>
  );
}
