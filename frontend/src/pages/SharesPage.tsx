import { useCallback, useEffect, useState } from 'react';
import { listShares, revokeShare, type Share } from '../api/client';

function statusClass(status: string): string {
  const s = status.toLowerCase();
  if (s === 'active') return 'badge badge--positive';
  if (s === 'revoked') return 'badge badge--negative';
  return 'badge badge--neutral';
}

export function SharesPage() {
  const [shares, setShares] = useState<Share[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(() => {
    setLoading(true);
    listShares()
      .then(setShares)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load shares'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  async function revoke(id: string) {
    if (!confirm('Revoke this share? The link will stop working.')) return;
    await revokeShare(id);
    load();
  }

  function copy(token: string) {
    navigator.clipboard?.writeText(`${window.location.origin}/share/${token}`);
  }

  return (
    <main className="page">
      <h1 className="page__title">Sharing</h1>
      <p className="page__subtitle">External shares and their view analytics.</p>

      {loading && <p className="muted">Loading…</p>}
      {error && <div className="error-text">{error}</div>}

      {!loading && shares.length === 0 && (
        <div className="card" style={{ padding: 32, textAlign: 'center' }}>
          <p className="muted">No shares yet. Share a document from its detail page.</p>
        </div>
      )}

      {shares.length > 0 && (
        <div className="card">
          <table className="table">
            <thead>
              <tr>
                <th>Document</th>
                <th>Recipient</th>
                <th>Permission</th>
                <th>Status</th>
                <th>Views</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {shares.map((s) => (
                <tr key={s.id}>
                  <td>{s.documentTitle ?? '—'}</td>
                  <td>{s.recipientEmail}</td>
                  <td>{s.permissionLevel}</td>
                  <td>
                    <span className={statusClass(s.status)}>{s.status}</span>
                  </td>
                  <td>{s.viewCount}</td>
                  <td style={{ display: 'flex', gap: 8 }}>
                    <button className="btn btn--ghost btn--inline" onClick={() => copy(s.accessToken)}>
                      Copy link
                    </button>
                    {s.status === 'Active' && (
                      <button className="btn btn--ghost btn--inline" onClick={() => revoke(s.id)}>
                        Revoke
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </main>
  );
}
