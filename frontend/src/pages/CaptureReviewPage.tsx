import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { apiFetch } from '../api/client';

interface Field {
  id: string;
  fieldName: string;
  fieldValue: string | null;
  confidence: number | null;
  sourcePage: number | null;
}

interface DocDetail {
  id: string;
  fileName: string;
  title: string | null;
  fileType: string;
  fileSizeBytes: number;
  status: string;
  domainCode: string | null;
  categoryCode: string | null;
  typeCode: string | null;
  verificationTier: string;
  extractedFields: Field[];
}

function barColor(c: number | null): string {
  if (c == null) return 'var(--text-secondary)';
  if (c >= 85) return 'var(--positive)';
  if (c >= 60) return 'var(--critical)';
  return 'var(--negative)';
}

export function CaptureReviewPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [doc, setDoc] = useState<DocDetail | null>(null);
  const [values, setValues] = useState<Record<string, string>>({});
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    if (!id) return;
    apiFetch<DocDetail>(`/api/documents/${id}`)
      .then((d) => {
        setDoc(d);
        const init: Record<string, string> = {};
        d.extractedFields.forEach((f) => {
          init[f.id] = f.fieldValue ?? '';
        });
        setValues(init);
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load document'))
      .finally(() => setLoading(false));
  }, [id]);

  async function confirm() {
    if (!doc) return;
    setSaving(true);
    setError(null);
    try {
      const fields = doc.extractedFields.map((f) => ({
        fieldId: f.id,
        value: values[f.id] ?? null,
        overrideReason: null,
      }));
      await apiFetch(`/api/documents/${doc.id}/confirm`, {
        method: 'POST',
        body: JSON.stringify({ fields }),
      });
      navigate('/documents');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to confirm');
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <main className="page">
        <p className="muted">Loading…</p>
      </main>
    );
  }

  if (!doc) {
    return (
      <main className="page">
        <div className="error-text">{error ?? 'Document not found.'}</div>
      </main>
    );
  }

  return (
    <main className="page">
      <h1 className="page__title">Review capture</h1>
      <p className="page__subtitle">
        Confirm the AI-extracted fields for <strong>{doc.title ?? doc.fileName}</strong>.
      </p>

      <div className="review-grid">
        <div className="card review-preview">
          <div className="review-preview__doc">
            <div className="review-preview__icon">📄</div>
            <div className="review-preview__name">{doc.fileName}</div>
            <div className="muted">
              {doc.fileType.toUpperCase()} · {(doc.fileSizeBytes / 1024).toFixed(0)} KB
            </div>
          </div>
          <div className="review-preview__meta">
            <div>
              <span className="muted">Domain</span>
              <div>{doc.domainCode ?? '—'}</div>
            </div>
            <div>
              <span className="muted">Type</span>
              <div>{doc.typeCode ?? '—'}</div>
            </div>
            <div>
              <span className="muted">Tier</span>
              <div>
                <span className="badge badge--positive">{doc.verificationTier}</span>
              </div>
            </div>
          </div>
        </div>

        <div className="card review-fields">
          {doc.extractedFields.length === 0 && (
            <p className="muted" style={{ padding: 16 }}>
              No fields were extracted for this document.
            </p>
          )}

          {doc.extractedFields.map((f) => (
            <div key={f.id} className="review-field">
              <div className="review-field__head">
                <label>{f.fieldName}</label>
                <span className="review-field__conf">
                  {f.confidence != null ? `${Math.round(f.confidence)}%` : '—'}
                </span>
              </div>
              <input
                value={values[f.id] ?? ''}
                placeholder="Enter value…"
                onChange={(e) => setValues((v) => ({ ...v, [f.id]: e.target.value }))}
              />
              <div className="confbar">
                <div
                  className="confbar__fill"
                  style={{ width: `${f.confidence ?? 0}%`, background: barColor(f.confidence) }}
                />
              </div>
            </div>
          ))}

          {error && (
            <div className="error-text" style={{ padding: '12px 0 0' }}>
              {error}
            </div>
          )}

          <div className="review-actions">
            <button className="btn btn--ghost" onClick={() => navigate('/documents')} disabled={saving}>
              Cancel
            </button>
            <button className="btn" onClick={confirm} disabled={saving}>
              {saving ? 'Saving…' : 'Confirm & save'}
            </button>
          </div>
        </div>
      </div>
    </main>
  );
}
