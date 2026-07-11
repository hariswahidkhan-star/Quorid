import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  deleteDraft,
  finalizeDraft,
  getDraft,
  improveDraft,
  updateDraft,
  type DraftDetail,
} from '../api/client';

const IMPROVE_ACTIONS = [
  { label: 'Polish', instruction: 'make it more professional' },
  { label: 'Make concise', instruction: 'make it concise and shorter' },
  { label: 'More formal', instruction: 'make the tone more formal' },
];

export function DraftEditorPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [draft, setDraft] = useState<DraftDetail | null>(null);
  const [title, setTitle] = useState('');
  const [body, setBody] = useState('');
  const [busy, setBusy] = useState(false);
  const [saved, setSaved] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const apply = useCallback((d: DraftDetail) => {
    setDraft(d);
    setTitle(d.title);
    setBody(d.body);
  }, []);

  useEffect(() => {
    if (!id) return;
    getDraft(id)
      .then(apply)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [id, apply]);

  if (loading) {
    return (
      <main className="page">
        <p className="muted">Loading…</p>
      </main>
    );
  }
  if (!draft) {
    return (
      <main className="page">
        <div className="error-text">{error ?? 'Not found.'}</div>
      </main>
    );
  }

  const finalized = draft.status === 'Finalized';

  async function save() {
    if (!id) return;
    setBusy(true);
    setSaved(false);
    try {
      const d = await updateDraft(id, { title, body });
      apply(d);
      setSaved(true);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Save failed');
    } finally {
      setBusy(false);
    }
  }

  async function improve(instruction: string) {
    if (!id) return;
    setBusy(true);
    setSaved(false);
    try {
      // Persist any pending edits first so the improve pass runs on current text.
      await updateDraft(id, { title, body });
      apply(await improveDraft(id, instruction));
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Improve failed');
    } finally {
      setBusy(false);
    }
  }

  async function finalize() {
    if (!id || !confirm('Finalize this draft and add it to the vault?')) return;
    setBusy(true);
    try {
      await updateDraft(id, { title, body });
      const r = await finalizeDraft(id);
      apply(await getDraft(id));
      navigate(`/documents/${r.documentId}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Finalize failed');
      setBusy(false);
    }
  }

  async function del() {
    if (!id || !confirm('Delete this draft?')) return;
    await deleteDraft(id);
    navigate('/studio');
  }

  return (
    <main className="page" style={{ maxWidth: 900 }}>
      <div className="toolbar">
        <div style={{ flex: 1 }}>
          <input
            className="studio-title"
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            disabled={finalized}
          />
          <p className="page__subtitle" style={{ margin: '4px 0 0' }}>
            <span className={`badge badge--${finalized ? 'positive' : 'neutral'}`}>{draft.status}</span>
            {draft.templateKey && <span className="muted"> · from {draft.templateKey}</span>}
          </p>
        </div>
        <button className="btn btn--ghost btn--inline" onClick={() => navigate('/studio')}>
          ← Studio
        </button>
      </div>

      {error && <div className="error-text">{error}</div>}

      {!finalized && (
        <div className="studio-actions">
          <span className="muted" style={{ fontSize: 13 }}>✨ Improve:</span>
          {IMPROVE_ACTIONS.map((a) => (
            <button
              key={a.label}
              className="btn btn--ghost btn--inline"
              onClick={() => improve(a.instruction)}
              disabled={busy}
            >
              {a.label}
            </button>
          ))}
        </div>
      )}

      <textarea
        className="studio-editor"
        value={body}
        onChange={(e) => setBody(e.target.value)}
        disabled={finalized}
        rows={22}
      />

      <div className="studio-footer">
        <button className="btn btn--ghost btn--inline" onClick={del} disabled={busy}>
          Delete
        </button>
        <div style={{ flex: 1 }} />
        {saved && <span className="muted" style={{ marginRight: 12 }}>Saved.</span>}
        {finalized ? (
          draft.documentId && (
            <button className="btn btn--inline" onClick={() => navigate(`/documents/${draft.documentId}`)}>
              View in vault →
            </button>
          )
        ) : (
          <>
            <button className="btn btn--ghost btn--inline" onClick={save} disabled={busy}>
              {busy ? 'Working…' : 'Save'}
            </button>
            <button className="btn btn--inline" onClick={finalize} disabled={busy}>
              Finalize to vault
            </button>
          </>
        )}
      </div>
    </main>
  );
}
