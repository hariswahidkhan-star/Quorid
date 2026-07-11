import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  getExpiring,
  listDocuments,
  type BatchUploadResult,
  type DocumentListItem,
  type ExpiringDocument,
} from '../api/client';
import { UploadModal } from '../components/UploadModal';

const DOMAINS = [
  { code: 'IDN', name: 'Identity' },
  { code: 'LGL', name: 'Legal' },
  { code: 'FIN', name: 'Financial' },
  { code: 'CMP', name: 'Compliance' },
  { code: 'INS', name: 'Insurance' },
  { code: 'PPL', name: 'People' },
  { code: 'OPS', name: 'Operations' },
  { code: 'IPR', name: 'Intellectual Property' },
];

const STATUSES = ['Draft', 'Active', 'Expired', 'Archived'];

export function DocumentsPage() {
  const navigate = useNavigate();

  const [items, setItems] = useState<DocumentListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [domain, setDomain] = useState('');
  const [status, setStatus] = useState('');
  const [search, setSearch] = useState('');
  const [searchInput, setSearchInput] = useState('');
  const [expiring, setExpiring] = useState<ExpiringDocument[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showUpload, setShowUpload] = useState(false);

  const load = useCallback(() => {
    setLoading(true);
    listDocuments({
      domain: domain || undefined,
      status: status || undefined,
      search: search || undefined,
    })
      .then((d) => {
        setItems(d.items);
        setTotal(d.total);
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load documents'))
      .finally(() => setLoading(false));
  }, [domain, status, search]);

  useEffect(() => {
    load();
  }, [load]);

  useEffect(() => {
    getExpiring(90).then(setExpiring).catch(() => undefined);
  }, []);

  function handleComplete(result: BatchUploadResult) {
    setShowUpload(false);
    const firstDoc = result.items.find((i) => i.success && i.documentId)?.documentId;
    if (result.total === 1 && firstDoc) {
      navigate(`/documents/${firstDoc}/review`);
    } else {
      load();
    }
  }

  return (
    <main className="page">
      <div className="toolbar">
        <div>
          <h1 className="page__title">Documents</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            The identity vault — {total} document{total === 1 ? '' : 's'}.
          </p>
        </div>
        <button className="btn btn--inline" onClick={() => setShowUpload(true)}>
          + Upload
        </button>
      </div>

      {expiring.length > 0 && (
        <div className="card expiring-strip">
          <strong>
            ⚠ {expiring.length} document{expiring.length === 1 ? '' : 's'} expiring soon
          </strong>
          <span className="muted">
            {' '}—{' '}
            {expiring
              .slice(0, 3)
              .map((e) => `${e.title ?? e.fileName} (${e.daysUntilExpiry}d)`)
              .join(', ')}
            {expiring.length > 3 ? '…' : ''}
          </span>
        </div>
      )}

      <div className="filter-bar">
        <select value={domain} onChange={(e) => setDomain(e.target.value)}>
          <option value="">All domains</option>
          {DOMAINS.map((d) => (
            <option key={d.code} value={d.code}>
              {d.name}
            </option>
          ))}
        </select>
        <select value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="">Active &amp; draft</option>
          {STATUSES.map((s) => (
            <option key={s} value={s}>
              {s}
            </option>
          ))}
        </select>
        <input
          value={searchInput}
          placeholder="Search name… (Enter)"
          onChange={(e) => setSearchInput(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') setSearch(searchInput);
          }}
        />
      </div>

      {loading && <p className="muted">Loading…</p>}
      {error && <div className="error-text">{error}</div>}

      {!loading && items.length === 0 && (
        <div className="card" style={{ padding: 32, textAlign: 'center' }}>
          <p className="muted">
            No documents match. Click <strong>Upload</strong> to capture one.
          </p>
        </div>
      )}

      {items.length > 0 && (
        <div className="card">
          <table className="table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Domain</th>
                <th>Type</th>
                <th>Status</th>
                <th>Tier</th>
              </tr>
            </thead>
            <tbody>
              {items.map((d) => (
                <tr key={d.id} className="clickable" onClick={() => navigate(`/documents/${d.id}`)}>
                  <td>{d.title ?? d.fileName}</td>
                  <td>{d.domainCode ?? '—'}</td>
                  <td>{d.fileType.toUpperCase()}</td>
                  <td>
                    <span className="badge badge--neutral">{d.status}</span>
                  </td>
                  <td>
                    <span className="badge badge--positive">{d.verificationTier}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {showUpload && <UploadModal onClose={() => setShowUpload(false)} onComplete={handleComplete} />}
    </main>
  );
}
