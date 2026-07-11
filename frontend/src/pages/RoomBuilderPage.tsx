import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  createRoom,
  getRoomTypes,
  listDocuments,
  type DocumentListItem,
  type GuestInput,
  type RoomType,
} from '../api/client';

const PERMISSIONS = [
  { value: 'None', label: 'None' },
  { value: 'FenceView', label: 'Fence View' },
  { value: 'View', label: 'View' },
  { value: 'EncryptedPdf', label: 'Encrypted PDF' },
  { value: 'Print', label: 'Print' },
  { value: 'Pdf', label: 'PDF' },
  { value: 'Original', label: 'Original' },
  { value: 'Upload', label: 'Upload' },
];

const ROLES = ['Reviewer', 'Viewer', 'Contributor'];

const DOWNLOAD_POLICIES = [
  { value: 'ViewOnly', label: 'View Only' },
  { value: 'None', label: 'None' },
  { value: 'EncryptedPdf', label: 'Encrypted PDF' },
  { value: 'Pdf', label: 'PDF' },
  { value: 'Original', label: 'Original' },
];

const STEP_LABELS = ['Room Info', 'Folders & Docs', 'Access', 'Review'];

export function RoomBuilderPage() {
  const navigate = useNavigate();
  const [step, setStep] = useState(1);
  const [types, setTypes] = useState<RoomType[]>([]);
  const [docs, setDocs] = useState<DocumentListItem[]>([]);

  const [name, setName] = useState('');
  const [typeCode, setTypeCode] = useState('');
  const [expiry, setExpiry] = useState('');
  const [nda, setNda] = useState(true);
  const [watermark, setWatermark] = useState(true);
  const [qa, setQa] = useState(true);
  const [downloadPolicy, setDownloadPolicy] = useState('ViewOnly');
  const [selectedDocs, setSelectedDocs] = useState<Set<string>>(new Set());
  const [guests, setGuests] = useState<GuestInput[]>([]);
  const [gEmail, setGEmail] = useState('');
  const [gName, setGName] = useState('');
  const [gRole, setGRole] = useState('Reviewer');
  const [gPerm, setGPerm] = useState('View');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    getRoomTypes()
      .then((t) => {
        setTypes(t);
        if (t.length > 0) setTypeCode((c) => c || t[0].code);
      })
      .catch(() => undefined);
    listDocuments({ status: 'Active' })
      .then((d) => setDocs(d.items))
      .catch(() => undefined);
  }, []);

  const selectedType = types.find((t) => t.code === typeCode);
  const canAdvance = name.trim() !== '' && typeCode !== '' && expiry !== '';

  function toggleDoc(id: string) {
    setSelectedDocs((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function addGuest() {
    if (!gEmail) return;
    setGuests([...guests, { email: gEmail, name: gName || undefined, role: gRole, permissionLevel: gPerm }]);
    setGEmail('');
    setGName('');
  }

  async function create() {
    setBusy(true);
    setError(null);
    try {
      const room = await createRoom({
        name,
        roomTypeCode: typeCode,
        expiryDate: expiry,
        ndaRequired: nda,
        watermark,
        qaEnabled: qa,
        downloadPolicy,
        documentIds: Array.from(selectedDocs),
        guests,
      });
      navigate(`/rooms/${room.id}`);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Create failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="page">
      <h1 className="page__title">Create Data Room</h1>

      <div className="wizard-steps">
        {STEP_LABELS.map((label, i) => (
          <div
            key={label}
            className={`wizard-step${step === i + 1 ? ' wizard-step--active' : ''}${
              step > i + 1 ? ' wizard-step--done' : ''
            }`}
          >
            {i + 1}. {label}
          </div>
        ))}
      </div>

      <div className="card" style={{ padding: 20 }}>
        {step === 1 && (
          <>
            <div className="field">
              <label>Room name</label>
              <input value={name} onChange={(e) => setName(e.target.value)} />
            </div>
            <div className="form-grid">
              <div className="field">
                <label>Room type</label>
                <select value={typeCode} onChange={(e) => setTypeCode(e.target.value)}>
                  {types.map((t) => (
                    <option key={t.code} value={t.code}>
                      {t.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label>Expiry date</label>
                <input type="date" value={expiry} onChange={(e) => setExpiry(e.target.value)} />
              </div>
              <div className="field">
                <label>Download policy</label>
                <select value={downloadPolicy} onChange={(e) => setDownloadPolicy(e.target.value)}>
                  {DOWNLOAD_POLICIES.map((p) => (
                    <option key={p.value} value={p.value}>
                      {p.label}
                    </option>
                  ))}
                </select>
              </div>
            </div>
            <div style={{ display: 'flex', gap: 20, marginTop: 8, flexWrap: 'wrap' }}>
              <label className="check">
                <input type="checkbox" checked={nda} onChange={(e) => setNda(e.target.checked)} /> NDA required
              </label>
              <label className="check">
                <input type="checkbox" checked={watermark} onChange={(e) => setWatermark(e.target.checked)} /> Watermark
              </label>
              <label className="check">
                <input type="checkbox" checked={qa} onChange={(e) => setQa(e.target.checked)} /> Q&amp;A enabled
              </label>
            </div>
          </>
        )}

        {step === 2 && (
          <>
            <p className="muted">
              Folders from the <strong>{selectedType?.name}</strong> template:
            </p>
            <div className="chips" style={{ marginBottom: 16 }}>
              {(selectedType?.folders ?? []).map((f, i) => (
                <span key={f} className="chip">
                  {i + 1}. {f}
                </span>
              ))}
            </div>
            <p className="muted">Select documents to include:</p>
            <div className="doc-picker">
              {docs.length === 0 && <p className="muted">No confirmed documents in the vault yet.</p>}
              {docs.map((d) => (
                <label key={d.id} className="doc-pick-row">
                  <input type="checkbox" checked={selectedDocs.has(d.id)} onChange={() => toggleDoc(d.id)} />
                  <span style={{ flex: 1 }}>{d.title ?? d.fileName}</span>
                  <span className="badge badge--positive">{d.verificationTier}</span>
                </label>
              ))}
            </div>
            <p className="muted" style={{ marginTop: 8 }}>
              {selectedDocs.size} selected
            </p>
          </>
        )}

        {step === 3 && (
          <>
            <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end', flexWrap: 'wrap' }}>
              <div className="field" style={{ flex: 1, minWidth: 140 }}>
                <label>Guest email</label>
                <input value={gEmail} onChange={(e) => setGEmail(e.target.value)} />
              </div>
              <div className="field" style={{ flex: 1, minWidth: 120 }}>
                <label>Name</label>
                <input value={gName} onChange={(e) => setGName(e.target.value)} />
              </div>
              <div className="field">
                <label>Role</label>
                <select value={gRole} onChange={(e) => setGRole(e.target.value)}>
                  {ROLES.map((r) => (
                    <option key={r} value={r}>
                      {r}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field">
                <label>Permission</label>
                <select value={gPerm} onChange={(e) => setGPerm(e.target.value)}>
                  {PERMISSIONS.map((p) => (
                    <option key={p.value} value={p.value}>
                      {p.label}
                    </option>
                  ))}
                </select>
              </div>
              <button className="btn btn--inline" onClick={addGuest} type="button">
                + Add
              </button>
            </div>
            {guests.length > 0 && (
              <table className="table" style={{ marginTop: 12 }}>
                <thead>
                  <tr>
                    <th>Email</th>
                    <th>Role</th>
                    <th>Permission</th>
                    <th></th>
                  </tr>
                </thead>
                <tbody>
                  {guests.map((g, i) => (
                    <tr key={`${g.email}-${i}`}>
                      <td>{g.email}</td>
                      <td>{g.role}</td>
                      <td>{g.permissionLevel}</td>
                      <td>
                        <button
                          className="btn btn--ghost btn--inline"
                          onClick={() => setGuests(guests.filter((_, j) => j !== i))}
                        >
                          Remove
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </>
        )}

        {step === 4 && (
          <div className="review-summary">
            <div>
              <span className="muted">Name</span>
              <div>{name}</div>
            </div>
            <div>
              <span className="muted">Type</span>
              <div>{selectedType?.name}</div>
            </div>
            <div>
              <span className="muted">Expiry</span>
              <div>{expiry}</div>
            </div>
            <div>
              <span className="muted">Security</span>
              <div>{[nda && 'NDA', watermark && 'Watermark', qa && 'Q&A'].filter(Boolean).join(' · ') || 'None'}</div>
            </div>
            <div>
              <span className="muted">Documents</span>
              <div>{selectedDocs.size}</div>
            </div>
            <div>
              <span className="muted">Guests</span>
              <div>{guests.length}</div>
            </div>
          </div>
        )}

        {error && <div className="error-text" style={{ marginTop: 12 }}>{error}</div>}

        <div className="wizard-actions">
          {step > 1 && (
            <button className="btn btn--ghost btn--inline" onClick={() => setStep(step - 1)}>
              ← Back
            </button>
          )}
          <div style={{ flex: 1 }} />
          <button className="btn btn--ghost btn--inline" onClick={() => navigate('/rooms')}>
            Cancel
          </button>
          {step < 4 && (
            <button
              className="btn btn--inline"
              disabled={step === 1 && !canAdvance}
              onClick={() => setStep(step + 1)}
            >
              Next →
            </button>
          )}
          {step === 4 && (
            <button className="btn btn--inline" onClick={create} disabled={busy}>
              {busy ? 'Creating…' : 'Create Room'}
            </button>
          )}
        </div>
      </div>
    </main>
  );
}
