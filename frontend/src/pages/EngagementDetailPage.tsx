import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  addChecklistItems,
  assignChecklistDoc,
  deleteEngagement,
  getEngagement,
  listDocuments,
  removeChecklistItem,
  sendCollectionRequest,
  updateEngagement,
  type DocumentListItem,
  type EngagementDetail,
} from '../api/client';

function itemBadge(status: string): string {
  return status === 'received' ? 'badge badge--positive' : 'badge badge--neutral';
}

export function EngagementDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [engagement, setEngagement] = useState<EngagementDetail | null>(null);
  const [docs, setDocs] = useState<DocumentListItem[]>([]);
  const [newItem, setNewItem] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [portalToken, setPortalToken] = useState<string | null>(null);

  const load = useCallback(() => {
    if (!id) return;
    getEngagement(id)
      .then((d) => {
        setEngagement(d);
        setPortalToken(d.portalToken);
      })
      .catch((err) => setError(err instanceof Error ? err.message : 'Failed to load'))
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    load();
    listDocuments({ status: 'Active' })
      .then((d) => setDocs(d.items))
      .catch(() => undefined);
  }, [load]);

  if (loading) {
    return (
      <main className="page">
        <p className="muted">Loading…</p>
      </main>
    );
  }

  if (!engagement) {
    return (
      <main className="page">
        <div className="error-text">{error ?? 'Not found.'}</div>
      </main>
    );
  }

  const e = engagement;

  async function assign(itemId: string, docId: string) {
    if (!id) return;
    setEngagement(await assignChecklistDoc(id, itemId, docId || null));
  }
  async function addItem() {
    if (!id || !newItem) return;
    setEngagement(await addChecklistItems(id, [{ name: newItem }]));
    setNewItem('');
  }
  async function removeItem(itemId: string) {
    if (!id) return;
    await removeChecklistItem(id, itemId);
    load();
  }
  async function requestCollection() {
    if (!id) return;
    const r = await sendCollectionRequest(id);
    setPortalToken(r.token);
  }
  function copyPortal() {
    if (portalToken) navigator.clipboard?.writeText(`${window.location.origin}/portal/${portalToken}`);
  }
  async function setStatus(status: string) {
    if (!id) return;
    await updateEngagement(id, { status });
    load();
  }
  async function del() {
    if (!id || !confirm('Delete this engagement?')) return;
    await deleteEngagement(id);
    navigate(-1);
  }

  return (
    <main className="page">
      <div className="toolbar">
        <div>
          <h1 className="page__title">{e.name}</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            {e.type} · <span className="badge badge--neutral">{e.status}</span> · {e.received}/{e.total} received (
            {e.progress}%)
          </p>
        </div>
        <button className="btn btn--ghost btn--inline" onClick={() => navigate(-1)}>
          ← Back
        </button>
      </div>

      {e.type !== 'Project' && (
        <div className="card" style={{ padding: 16, marginBottom: 16 }}>
          <strong>Collection portal</strong>
          <div className="muted" style={{ fontSize: 13, marginBottom: 8 }}>
            Send this link to {e.contactEmail ?? 'the party'} to upload documents.
          </div>
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            {portalToken ? (
              <>
                <input
                  readOnly
                  value={`${window.location.origin}/portal/${portalToken}`}
                  style={{ flex: 1, minWidth: 200, padding: '8px 10px', border: '1px solid var(--border)', borderRadius: 6 }}
                />
                <button className="btn btn--inline" onClick={copyPortal}>
                  Copy link
                </button>
              </>
            ) : (
              <button className="btn btn--inline" onClick={requestCollection}>
                Send collection request
              </button>
            )}
          </div>
        </div>
      )}

      <div className="card" style={{ padding: 20 }}>
        <div style={{ display: 'flex', gap: 8, marginBottom: 16 }}>
          <input
            placeholder="Add a required document…"
            value={newItem}
            onChange={(ev) => setNewItem(ev.target.value)}
            style={{ flex: 1, padding: '8px 10px', border: '1px solid var(--border)', borderRadius: 6 }}
          />
          <button className="btn btn--inline" onClick={addItem} disabled={!newItem}>
            Add
          </button>
        </div>

        {e.checklist.length === 0 && <p className="muted">No required documents yet.</p>}
        {e.checklist.length > 0 && (
          <table className="table">
            <thead>
              <tr>
                <th>Required document</th>
                <th>Status</th>
                <th>Evidence</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {e.checklist.map((c) => (
                <tr key={c.id}>
                  <td>{c.name}</td>
                  <td>
                    <span className={itemBadge(c.status)}>{c.status}</span>
                  </td>
                  <td>
                    {c.documentTitle ? (
                      <span>
                        {c.documentTitle}{' '}
                        <button className="btn btn--ghost btn--inline" onClick={() => assign(c.id, '')}>
                          Clear
                        </button>
                      </span>
                    ) : (
                      <select defaultValue="" onChange={(ev) => assign(c.id, ev.target.value)}>
                        <option value="">Assign vault doc…</option>
                        {docs.map((d) => (
                          <option key={d.id} value={d.id}>
                            {d.title ?? d.fileName}
                          </option>
                        ))}
                      </select>
                    )}
                  </td>
                  <td>
                    <button className="btn btn--ghost btn--inline" onClick={() => removeItem(c.id)}>
                      ✕
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      <div className="card" style={{ padding: 16, marginTop: 16, display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {e.status !== 'Completed' && (
          <button className="btn btn--ghost btn--inline" onClick={() => setStatus('Completed')}>
            Mark completed
          </button>
        )}
        {e.status !== 'Archived' && (
          <button className="btn btn--ghost btn--inline" onClick={() => setStatus('Archived')}>
            Archive
          </button>
        )}
        <button className="btn btn--ghost btn--inline" onClick={del}>
          Delete
        </button>
      </div>
    </main>
  );
}
