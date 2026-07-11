import { useCallback, useEffect, useRef, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { ShareModal } from '../components/ShareModal';
import {
  archiveDocument,
  getActivity,
  getDocument,
  listVersions,
  restoreVersion,
  updateDocument,
  uploadVersion,
  type ActivityEntry,
  type DocumentDetail,
  type DocumentVersion,
} from '../api/client';

type Tab = 'metadata' | 'fields' | 'versions' | 'activity';

const DOMAINS: { code: string; name: string }[] = [
  { code: 'IDN', name: 'Identity' },
  { code: 'LGL', name: 'Legal' },
  { code: 'FIN', name: 'Financial' },
  { code: 'CMP', name: 'Compliance' },
  { code: 'INS', name: 'Insurance' },
  { code: 'PPL', name: 'People' },
  { code: 'OPS', name: 'Operations' },
  { code: 'IPR', name: 'Intellectual Property' },
];

const PRIVACY = ['Open', 'Controlled', 'Restricted', 'Locked'];

function confColor(c: number | null): string {
  if (c == null) return 'var(--text-secondary)';
  if (c >= 85) return 'var(--positive)';
  if (c >= 60) return 'var(--critical)';
  return 'var(--negative)';
}

export function DocumentDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [doc, setDoc] = useState<DocumentDetail | null>(null);
  const [tab, setTab] = useState<Tab>('metadata');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [showShare, setShowShare] = useState(false);

  const load = useCallback(() => {
    if (!id) return;
    getDocument(id)
      .then(setDoc)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load document'))
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  async function archive() {
    if (!doc || !confirm('Archive this document? It will be removed from any rooms.')) return;
    await archiveDocument(doc.id);
    navigate('/documents');
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
      <div className="toolbar">
        <div>
          <h1 className="page__title" style={{ wordBreak: 'break-all' }}>
            {doc.title ?? doc.fileName}
          </h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            {doc.fileType.toUpperCase()} · v{doc.version} ·{' '}
            <span className="badge badge--neutral">{doc.status}</span>{' '}
            <span className="badge badge--positive">{doc.verificationTier}</span>
          </p>
        </div>
        <div style={{ display: 'flex', gap: 8 }}>
          <button className="btn btn--inline" onClick={() => setShowShare(true)}>
            Share
          </button>
          <button className="btn btn--inline btn--ghost" onClick={archive}>
            Archive
          </button>
        </div>
      </div>

      <div className="tabs">
        {(['metadata', 'fields', 'versions', 'activity'] as Tab[]).map((t) => (
          <button
            key={t}
            className={`tab${tab === t ? ' tab--active' : ''}`}
            onClick={() => setTab(t)}
          >
            {t === 'metadata' && 'Metadata'}
            {t === 'fields' && `Extracted Fields (${doc.extractedFields.length})`}
            {t === 'versions' && 'Versions'}
            {t === 'activity' && 'Activity'}
          </button>
        ))}
      </div>

      {tab === 'metadata' && <MetadataTab doc={doc} onSaved={load} />}
      {tab === 'fields' && (
        <div className="card">
          <table className="table">
            <thead>
              <tr>
                <th>Field</th>
                <th>Value</th>
                <th>Confidence</th>
              </tr>
            </thead>
            <tbody>
              {doc.extractedFields.length === 0 && (
                <tr>
                  <td colSpan={3} className="muted" style={{ padding: 16 }}>
                    No fields extracted.
                  </td>
                </tr>
              )}
              {doc.extractedFields.map((f) => (
                <tr key={f.id}>
                  <td>{f.fieldName}</td>
                  <td>{f.overrideValue ?? f.fieldValue ?? '—'}</td>
                  <td style={{ color: confColor(f.confidence) }}>
                    {f.confidence != null ? `${Math.round(f.confidence)}%` : '—'}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {tab === 'versions' && <VersionsTab documentId={doc.id} onChanged={load} />}
      {tab === 'activity' && <ActivityTab documentId={doc.id} />}

      {showShare && <ShareModal documentId={doc.id} onClose={() => setShowShare(false)} />}
    </main>
  );
}

function MetadataTab({ doc, onSaved }: { doc: DocumentDetail; onSaved: () => void }) {
  const [title, setTitle] = useState(doc.title ?? '');
  const [description, setDescription] = useState(doc.description ?? '');
  const [domain, setDomain] = useState(doc.domainCode ?? '');
  const [privacy, setPrivacy] = useState(doc.privacyLevel);
  const [expiry, setExpiry] = useState(doc.expiryDate ?? '');
  const [tags, setTags] = useState<string[]>(doc.tags.map((t) => t.tagName));
  const [newTag, setNewTag] = useState('');
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function addTag() {
    const t = newTag.trim();
    if (t && !tags.some((x) => x.toLowerCase() === t.toLowerCase())) setTags([...tags, t]);
    setNewTag('');
  }

  async function save() {
    setSaving(true);
    setSaved(false);
    setError(null);
    try {
      await updateDocument(doc.id, {
        title,
        description,
        domainCode: domain || undefined,
        privacyLevel: privacy,
        expiryDate: expiry || undefined,
        tags,
      });
      setSaved(true);
      onSaved();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Save failed');
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="card" style={{ padding: 20 }}>
      <div className="field">
        <label>Title</label>
        <input value={title} onChange={(e) => setTitle(e.target.value)} />
      </div>
      <div className="field">
        <label>Description</label>
        <input value={description} onChange={(e) => setDescription(e.target.value)} />
      </div>
      <div className="form-grid">
        <div className="field">
          <label>Domain</label>
          <select value={domain} onChange={(e) => setDomain(e.target.value)}>
            <option value="">—</option>
            {DOMAINS.map((d) => (
              <option key={d.code} value={d.code}>
                {d.name}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Privacy</label>
          <select value={privacy} onChange={(e) => setPrivacy(e.target.value)}>
            {PRIVACY.map((p) => (
              <option key={p} value={p}>
                {p}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Expiry date</label>
          <input type="date" value={expiry} onChange={(e) => setExpiry(e.target.value)} />
        </div>
      </div>

      <div className="field">
        <label>Tags</label>
        <div className="chips">
          {tags.map((t) => (
            <span key={t} className="chip">
              {t}
              <button onClick={() => setTags(tags.filter((x) => x !== t))} aria-label={`Remove ${t}`}>
                ✕
              </button>
            </span>
          ))}
        </div>
        <div style={{ display: 'flex', gap: 8, marginTop: 8 }}>
          <input
            value={newTag}
            placeholder="Add a tag…"
            onChange={(e) => setNewTag(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault();
                addTag();
              }
            }}
          />
          <button className="btn btn--ghost btn--inline" onClick={addTag} type="button">
            Add
          </button>
        </div>
      </div>

      {error && <div className="error-text">{error}</div>}
      <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginTop: 8 }}>
        <button className="btn btn--inline" onClick={save} disabled={saving}>
          {saving ? 'Saving…' : 'Save changes'}
        </button>
        {saved && <span className="muted">Saved.</span>}
      </div>
    </div>
  );
}

function VersionsTab({ documentId, onChanged }: { documentId: string; onChanged: () => void }) {
  const [versions, setVersions] = useState<DocumentVersion[]>([]);
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const load = useCallback(() => {
    listVersions(documentId)
      .then(setVersions)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load versions'));
  }, [documentId]);

  useEffect(() => {
    load();
  }, [load]);

  async function upload() {
    const file = fileRef.current?.files?.[0];
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      await uploadVersion(documentId, file, note);
      setNote('');
      if (fileRef.current) fileRef.current.value = '';
      load();
      onChanged();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Upload failed');
    } finally {
      setBusy(false);
    }
  }

  async function restore(versionId: string) {
    if (!confirm('Restore this version? A new version will be created with its content.')) return;
    await restoreVersion(documentId, versionId);
    load();
    onChanged();
  }

  return (
    <div className="card" style={{ padding: 20 }}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end', marginBottom: 16, flexWrap: 'wrap' }}>
        <input ref={fileRef} type="file" />
        <input
          value={note}
          placeholder="Change note…"
          onChange={(e) => setNote(e.target.value)}
          style={{ flex: 1, minWidth: 160, padding: '8px 10px', border: '1px solid var(--border)', borderRadius: 6 }}
        />
        <button className="btn btn--inline" onClick={upload} disabled={busy}>
          {busy ? 'Uploading…' : 'Upload new version'}
        </button>
      </div>

      {error && <div className="error-text">{error}</div>}

      <table className="table">
        <thead>
          <tr>
            <th>Version</th>
            <th>File</th>
            <th>Change note</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          {versions.map((v) => (
            <tr key={v.id}>
              <td>
                v{v.versionNumber} {v.isCurrent && <span className="badge badge--positive">Current</span>}
              </td>
              <td>{v.fileName}</td>
              <td className="muted">{v.changeNote ?? v.aiDiffSummary ?? '—'}</td>
              <td>
                {!v.isCurrent && (
                  <button className="btn btn--ghost btn--inline" onClick={() => restore(v.id)}>
                    Restore
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ActivityTab({ documentId }: { documentId: string }) {
  const [entries, setEntries] = useState<ActivityEntry[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getActivity(documentId)
      .then(setEntries)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load activity'));
  }, [documentId]);

  return (
    <div className="card" style={{ padding: 20 }}>
      {error && <div className="error-text">{error}</div>}
      {entries.length === 0 && <p className="muted">No activity yet.</p>}
      {entries.map((e, i) => (
        <div key={i} className="activity-row">
          <span className="badge badge--neutral">{e.action}</span>
          <span className="muted">{new Date(e.createdAt).toLocaleString()}</span>
        </div>
      ))}
    </div>
  );
}
