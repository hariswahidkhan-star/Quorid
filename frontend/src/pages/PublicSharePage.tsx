import { useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { getPublicShare, type PublicShare } from '../api/client';

export function PublicSharePage() {
  const { token } = useParams();
  const [share, setShare] = useState<PublicShare | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!token) return;
    getPublicShare(token)
      .then(setShare)
      .catch((e) => setError(e instanceof Error ? e.message : 'This share is unavailable.'))
      .finally(() => setLoading(false));
  }, [token]);

  return (
    <div className="public-share">
      <div className="public-share__card">
        <div className="auth__logo" style={{ fontSize: 22 }}>
          QUOR<span>ID</span>
        </div>

        {loading && <p className="muted">Loading…</p>}
        {error && <div className="error-text">{error}</div>}

        {share && (
          <>
            <h2 style={{ margin: '4px 0' }}>{share.documentTitle}</h2>
            <p className="muted">
              {share.fileType.toUpperCase()} · Permission: {share.permissionLevel}
            </p>
            <div className="viewer-placeholder">
              <div style={{ fontSize: 40 }}>📄</div>
              <p className="muted">{share.message}</p>
              {share.watermark && <div className="watermark">CONFIDENTIAL</div>}
            </div>
          </>
        )}
      </div>
    </div>
  );
}
