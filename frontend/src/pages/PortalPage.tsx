import { useCallback, useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { getPortalInfo, portalUpload, type PortalInfo } from '../api/client';

export function PortalPage() {
  const { token } = useParams();
  const [info, setInfo] = useState<PortalInfo | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [itemId, setItemId] = useState('');
  const fileRef = useRef<HTMLInputElement>(null);

  const load = useCallback(() => {
    if (!token) return;
    setLoading(true);
    getPortalInfo(token)
      .then(setInfo)
      .catch((e) => setError(e instanceof Error ? e.message : 'This request is unavailable.'))
      .finally(() => setLoading(false));
  }, [token]);

  useEffect(() => {
    load();
  }, [load]);

  async function upload() {
    const file = fileRef.current?.files?.[0];
    if (!token || !file) return;
    setBusy(true);
    setError(null);
    try {
      await portalUpload(token, file, itemId || undefined);
      if (fileRef.current) fileRef.current.value = '';
      setItemId('');
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Upload failed');
    } finally {
      setBusy(false);
    }
  }

  if (loading) {
    return (
      <div className="public-share">
        <div className="public-share__card">
          <p className="muted">Loading…</p>
        </div>
      </div>
    );
  }

  if (!info) {
    return (
      <div className="public-share">
        <div className="public-share__card">
          <div className="auth__logo" style={{ fontSize: 22 }}>
            QUOR<span>ID</span>
          </div>
          <div className="error-text">{error}</div>
        </div>
      </div>
    );
  }

  return (
    <div className="public-share">
      <div className="public-share__card" style={{ maxWidth: 560, textAlign: 'left' }}>
        <div className="auth__logo" style={{ fontSize: 22, textAlign: 'center' }}>
          QUOR<span>ID</span>
        </div>
        <h2 style={{ marginBottom: 2 }}>{info.engagementName}</h2>
        <p className="muted">Requested by {info.hostEntity}</p>

        <p style={{ marginTop: 12, fontWeight: 600 }}>Requested documents</p>
        {info.checklist.length === 0 && <p className="muted">No specific documents requested — upload anything relevant.</p>}
        {info.checklist.map((c) => (
          <div key={c.id} className="activity-row">
            <span>{c.name}</span>
            <span className={c.status === 'received' ? 'badge badge--positive' : 'badge badge--neutral'}>{c.status}</span>
          </div>
        ))}

        <div style={{ marginTop: 16, display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
          <select value={itemId} onChange={(e) => setItemId(e.target.value)}>
            <option value="">Auto-match</option>
            {info.checklist
              .filter((c) => c.status !== 'received')
              .map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
          </select>
          <input ref={fileRef} type="file" />
          <button className="btn btn--inline" onClick={upload} disabled={busy}>
            {busy ? 'Uploading…' : 'Upload'}
          </button>
        </div>
        {error && <div className="error-text">{error}</div>}
      </div>
    </div>
  );
}
