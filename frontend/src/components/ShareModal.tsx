import { useState } from 'react';
import { createShare, type Share } from '../api/client';

const PERMISSIONS = [
  { value: 'None', label: 'None' },
  { value: 'FenceView', label: 'Fence View (top 25%)' },
  { value: 'View', label: 'View' },
  { value: 'EncryptedPdf', label: 'Encrypted PDF' },
  { value: 'Print', label: 'Print' },
  { value: 'Pdf', label: 'PDF' },
  { value: 'Original', label: 'Original' },
  { value: 'Upload', label: 'Upload' },
];

export function ShareModal({ documentId, onClose }: { documentId: string; onClose: () => void }) {
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [permission, setPermission] = useState('View');
  const [expiry, setExpiry] = useState('');
  const [watermark, setWatermark] = useState(true);
  const [trackViews, setTrackViews] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [created, setCreated] = useState<Share | null>(null);
  const [copied, setCopied] = useState(false);

  const shareUrl = created ? `${window.location.origin}/share/${created.accessToken}` : '';

  async function submit() {
    setBusy(true);
    setError(null);
    try {
      const share = await createShare(documentId, {
        recipientEmail: email,
        recipientName: name || undefined,
        permissionLevel: permission,
        expiryDate: expiry || undefined,
        watermark,
        trackViews,
      });
      setCreated(share);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Share failed');
    } finally {
      setBusy(false);
    }
  }

  function copy() {
    navigator.clipboard?.writeText(shareUrl).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    });
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal__header">
          <h2>Share document</h2>
          <button className="modal__close" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>

        {!created ? (
          <>
            <div className="field">
              <label>Recipient email</label>
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
            </div>
            <div className="field">
              <label>Recipient name</label>
              <input value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="form-grid">
              <div className="field">
                <label>Permission</label>
                <select value={permission} onChange={(e) => setPermission(e.target.value)}>
                  {PERMISSIONS.map((p) => (
                    <option key={p.value} value={p.value}>
                      {p.label}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label>Expiry date</label>
                <input type="date" value={expiry} onChange={(e) => setExpiry(e.target.value)} />
              </div>
            </div>
            <div style={{ display: 'flex', gap: 20, margin: '4px 0 12px' }}>
              <label className="check">
                <input type="checkbox" checked={watermark} onChange={(e) => setWatermark(e.target.checked)} />{' '}
                Watermark
              </label>
              <label className="check">
                <input type="checkbox" checked={trackViews} onChange={(e) => setTrackViews(e.target.checked)} />{' '}
                Track views
              </label>
            </div>
            {error && <div className="error-text">{error}</div>}
            <div className="modal__footer">
              <button className="btn btn--ghost" onClick={onClose} disabled={busy}>
                Cancel
              </button>
              <button className="btn" onClick={submit} disabled={busy || !email}>
                {busy ? 'Sharing…' : 'Create link'}
              </button>
            </div>
          </>
        ) : (
          <>
            <p className="muted">Share link created. Send it to {created.recipientEmail}:</p>
            <div style={{ display: 'flex', gap: 8 }}>
              <input
                readOnly
                value={shareUrl}
                style={{ flex: 1, padding: '8px 10px', border: '1px solid var(--border)', borderRadius: 6 }}
              />
              <button className="btn btn--inline" onClick={copy}>
                {copied ? 'Copied' : 'Copy'}
              </button>
            </div>
            <div className="modal__footer">
              <button className="btn" onClick={onClose}>
                Done
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
