import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  generateDraft,
  listDrafts,
  listTemplates,
  type DocTemplate,
  type DraftListItem,
} from '../api/client';

export function StudioPage() {
  const navigate = useNavigate();
  const [templates, setTemplates] = useState<DocTemplate[]>([]);
  const [drafts, setDrafts] = useState<DraftListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [seed, setSeed] = useState<DocTemplate | null>(null);
  const [blank, setBlank] = useState(false);

  useEffect(() => {
    Promise.all([listTemplates(), listDrafts()])
      .then(([t, d]) => {
        setTemplates(t);
        setDrafts(d);
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, []);

  const categories = [...new Set(templates.map((t) => t.category))];

  return (
    <main className="page" style={{ maxWidth: 1100 }}>
      <div className="toolbar">
        <div>
          <h1 className="page__title">Document Studio</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            Draft business documents with AI, then finalize them into the vault.
          </p>
        </div>
        <button className="btn btn--inline" onClick={() => setBlank(true)}>
          + Blank draft
        </button>
      </div>

      {error && <div className="error-text">{error}</div>}
      {loading && <p className="muted">Loading…</p>}

      {drafts.length > 0 && (
        <div className="card" style={{ padding: 0, marginBottom: 24 }}>
          <table className="table">
            <thead>
              <tr>
                <th>Draft</th>
                <th>Template</th>
                <th>Status</th>
                <th>Updated</th>
              </tr>
            </thead>
            <tbody>
              {drafts.map((d) => (
                <tr key={d.id} className="clickable" onClick={() => navigate(`/studio/drafts/${d.id}`)}>
                  <td>{d.title}</td>
                  <td className="muted">{d.templateKey ?? '—'}</td>
                  <td>
                    <span className={`badge badge--${d.finalized ? 'positive' : 'neutral'}`}>
                      {d.status}
                    </span>
                  </td>
                  <td className="muted">{formatDate(d.updatedAt)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <h2 style={{ fontSize: 18, margin: '0 0 12px' }}>Start from a template</h2>
      {categories.map((cat) => (
        <div key={cat} style={{ marginBottom: 20 }}>
          <div className="report-cat">{cat}</div>
          <div className="report-grid">
            {templates
              .filter((t) => t.category === cat)
              .map((t) => (
                <button key={t.key} className="report-card" onClick={() => setSeed(t)}>
                  <div className="report-card__title">{t.name}</div>
                  <div className="report-card__desc">{t.description}</div>
                  <div className="report-card__cta">Use template →</div>
                </button>
              ))}
          </div>
        </div>
      ))}

      {(seed || blank) && (
        <GenerateModal
          template={seed}
          onClose={() => {
            setSeed(null);
            setBlank(false);
          }}
          onCreated={(id) => navigate(`/studio/drafts/${id}`)}
        />
      )}
    </main>
  );
}

function GenerateModal({
  template,
  onClose,
  onCreated,
}: {
  template: DocTemplate | null;
  onClose: () => void;
  onCreated: (id: string) => void;
}) {
  const [documentType, setDocumentType] = useState(template?.name ?? '');
  const [title, setTitle] = useState(template?.name ?? '');
  const [recipient, setRecipient] = useState('');
  const [prompt, setPrompt] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function create() {
    if (!prompt.trim() && !template) {
      setError('Describe what the document should say.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const draft = await generateDraft({
        templateKey: template?.key,
        documentType: documentType || template?.name || 'Document',
        title: title || undefined,
        prompt,
        recipient: recipient || undefined,
      });
      onCreated(draft.id);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Generation failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal__header">
          <h2>{template ? `New — ${template.name}` : 'New blank draft'}</h2>
          <button className="modal__close" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>

        {!template && (
          <div className="field">
            <label>Document type</label>
            <input
              value={documentType}
              onChange={(e) => setDocumentType(e.target.value)}
              placeholder="e.g. Letter of Intent"
            />
          </div>
        )}
        <div className="form-grid">
          <div className="field">
            <label>Title</label>
            <input value={title} onChange={(e) => setTitle(e.target.value)} />
          </div>
          <div className="field">
            <label>Recipient (optional)</label>
            <input value={recipient} onChange={(e) => setRecipient(e.target.value)} />
          </div>
        </div>
        <div className="field">
          <label>What should it say?</label>
          <textarea
            value={prompt}
            onChange={(e) => setPrompt(e.target.value)}
            rows={5}
            placeholder="Describe the purpose and key points. The studio drafts the rest."
            style={{
              width: '100%',
              padding: '10px 12px',
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
          <button className="btn" onClick={create} disabled={busy}>
            {busy ? 'Drafting…' : 'Generate draft'}
          </button>
        </div>
      </div>
    </div>
  );
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
}
