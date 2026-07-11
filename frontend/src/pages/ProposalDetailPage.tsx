import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  addProposalDocuments,
  deleteProposal,
  generateCoverLetter,
  getProposal,
  listDocuments,
  removeProposalDocument,
  reorderProposalDocuments,
  updateProposal,
  type DocumentListItem,
  type ProposalDetail,
} from '../api/client';

const STAGES = ['Lead', 'Qualified', 'Proposal', 'Negotiation', 'Won', 'Lost'];

export function ProposalDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [p, setP] = useState<ProposalDetail | null>(null);
  const [docs, setDocs] = useState<DocumentListItem[]>([]);
  const [coverLetter, setCoverLetter] = useState('');
  const [recipient, setRecipient] = useState('');
  const [value, setValue] = useState('');
  const [addDoc, setAddDoc] = useState('');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const load = useCallback(() => {
    if (!id) return;
    getProposal(id)
      .then((d) => {
        setP(d);
        setCoverLetter(d.coverLetter ?? '');
        setRecipient(d.recipientName ?? '');
        setValue(d.value != null ? String(d.value) : '');
      })
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'))
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

  if (!p) {
    return (
      <main className="page">
        <div className="error-text">{error ?? 'Not found.'}</div>
      </main>
    );
  }

  const proposal = p;

  async function setStage(stage: string) {
    if (!id) return;
    await updateProposal(id, { stage });
    load();
  }

  async function saveDetails() {
    if (!id) return;
    setSaved(false);
    const body: Partial<{ recipientName: string; value: number; coverLetter: string }> = {
      recipientName: recipient,
      coverLetter,
    };
    if (value) body.value = Number(value);
    await updateProposal(id, body);
    setSaved(true);
    load();
  }

  async function genCover() {
    if (!id) return;
    const r = await generateCoverLetter(id);
    setCoverLetter(r.coverLetter);
  }

  async function add() {
    if (!id || !addDoc) return;
    setP(await addProposalDocuments(id, [addDoc]));
    setAddDoc('');
  }

  async function remove(pdId: string) {
    if (!id) return;
    await removeProposalDocument(id, pdId);
    load();
  }

  async function move(index: number, dir: number) {
    if (!id) return;
    const arr = [...proposal.documents];
    const j = index + dir;
    if (j < 0 || j >= arr.length) return;
    [arr[index], arr[j]] = [arr[j], arr[index]];
    setP(await reorderProposalDocuments(id, arr.map((x) => x.id)));
  }

  async function del() {
    if (!id || !confirm('Delete this proposal?')) return;
    await deleteProposal(id);
    navigate('/proposals');
  }

  return (
    <main className="page">
      <div className="toolbar">
        <div>
          <h1 className="page__title">{proposal.title}</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            Stage:{' '}
            <select value={proposal.stage} onChange={(e) => setStage(e.target.value)}>
              {STAGES.map((s) => (
                <option key={s}>{s}</option>
              ))}
            </select>
          </p>
        </div>
        <button className="btn btn--ghost btn--inline" onClick={() => navigate('/proposals')}>
          ← Pipeline
        </button>
      </div>

      <div className="card" style={{ padding: 20, marginBottom: 16 }}>
        <div className="form-grid">
          <div className="field">
            <label>Recipient</label>
            <input value={recipient} onChange={(e) => setRecipient(e.target.value)} />
          </div>
          <div className="field">
            <label>Value ($)</label>
            <input type="number" value={value} onChange={(e) => setValue(e.target.value)} />
          </div>
        </div>
        <div className="field">
          <label>Cover letter</label>
          <textarea
            value={coverLetter}
            onChange={(e) => setCoverLetter(e.target.value)}
            rows={7}
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
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <button className="btn btn--ghost btn--inline" onClick={genCover}>
            Auto-generate cover letter
          </button>
          <button className="btn btn--inline" onClick={saveDetails}>
            Save
          </button>
          {saved && <span className="muted">Saved.</span>}
        </div>
      </div>

      <div className="card" style={{ padding: 20 }}>
        <strong>Proposal documents</strong>
        <div className="muted" style={{ fontSize: 13, marginBottom: 12 }}>
          Ordered package assembled from the vault.
        </div>
        <div style={{ display: 'flex', gap: 8, marginBottom: 12 }}>
          <select value={addDoc} onChange={(e) => setAddDoc(e.target.value)} style={{ flex: 1 }}>
            <option value="">Add a document…</option>
            {docs.map((d) => (
              <option key={d.id} value={d.id}>
                {d.title ?? d.fileName}
              </option>
            ))}
          </select>
          <button className="btn btn--inline" onClick={add} disabled={!addDoc}>
            Add
          </button>
        </div>
        {proposal.documents.length === 0 && <p className="muted">No documents yet.</p>}
        {proposal.documents.map((d, i) => (
          <div key={d.id} className="activity-row">
            <span>
              {i + 1}. {d.title ?? d.fileName}
            </span>
            <span style={{ display: 'flex', gap: 6 }}>
              <button className="btn btn--ghost btn--inline" onClick={() => move(i, -1)} disabled={i === 0}>
                ↑
              </button>
              <button
                className="btn btn--ghost btn--inline"
                onClick={() => move(i, 1)}
                disabled={i === proposal.documents.length - 1}
              >
                ↓
              </button>
              <button className="btn btn--ghost btn--inline" onClick={() => remove(d.id)}>
                ✕
              </button>
            </span>
          </div>
        ))}
      </div>

      <div className="card" style={{ padding: 16, marginTop: 16 }}>
        <button className="btn btn--ghost btn--inline" onClick={del}>
          Delete proposal
        </button>
      </div>
    </main>
  );
}
