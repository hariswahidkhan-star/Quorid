import { useCallback, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import {
  getGuestQuestions,
  getGuestRoom,
  logGuestView,
  signNda,
  submitGuestQuestion,
  type GuestDoc,
  type GuestQuestion,
  type GuestRoom,
} from '../api/client';

const CATEGORIES = ['General', 'Financial', 'Legal', 'Technical', 'Operational', 'Tax'];

export function GuestRoomPage() {
  const { token } = useParams();
  const [room, setRoom] = useState<GuestRoom | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [legalName, setLegalName] = useState('');
  const [agreed, setAgreed] = useState(false);
  const [signing, setSigning] = useState(false);
  const [openDoc, setOpenDoc] = useState<GuestDoc | null>(null);
  const [tab, setTab] = useState<'docs' | 'qa'>('docs');

  const load = useCallback(() => {
    if (!token) return;
    setLoading(true);
    getGuestRoom(token)
      .then(setRoom)
      .catch((e) => setError(e instanceof Error ? e.message : 'This share is unavailable.'))
      .finally(() => setLoading(false));
  }, [token]);

  useEffect(() => {
    load();
  }, [load]);

  async function sign() {
    if (!token || !legalName || !agreed) return;
    setSigning(true);
    try {
      await signNda(token, legalName);
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Signing failed');
    } finally {
      setSigning(false);
    }
  }

  function openDocument(d: GuestDoc) {
    setOpenDoc(d);
    if (token) logGuestView(token, d.documentId).catch(() => undefined);
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

  if (!room) {
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

  if (room.ndaRequired && !room.ndaSigned) {
    return (
      <div className="public-share">
        <div className="public-share__card" style={{ maxWidth: 620, textAlign: 'left' }}>
          <div className="auth__logo" style={{ fontSize: 22, textAlign: 'center' }}>
            QUOR<span>ID</span>
          </div>
          <h2 style={{ marginBottom: 2 }}>{room.roomName}</h2>
          <p className="muted">Hosted by {room.hostEntity}</p>
          <div className="card" style={{ padding: 16, margin: '12px 0', maxHeight: 220, overflow: 'auto' }}>
            <p className="muted">
              Non-Disclosure Agreement — you agree to keep all documents in this data room strictly confidential and
              to use them solely for the stated purpose. Access is logged. (Sample NDA text.)
            </p>
          </div>
          <label className="check">
            <input type="checkbox" checked={agreed} onChange={(e) => setAgreed(e.target.checked)} /> I have read and
            agree to the terms
          </label>
          <div className="field" style={{ marginTop: 12 }}>
            <label>Full legal name</label>
            <input value={legalName} onChange={(e) => setLegalName(e.target.value)} />
          </div>
          {error && <div className="error-text">{error}</div>}
          <button className="btn" onClick={sign} disabled={signing || !agreed || !legalName}>
            {signing ? 'Signing…' : 'Sign & Accept'}
          </button>
        </div>
      </div>
    );
  }

  return (
    <div className="guest-room">
      <header className="guest-room__header">
        <div className="shell-bar__logo" style={{ fontSize: 16 }}>
          QUOR<span>ID</span>
        </div>
        <div style={{ marginTop: 4 }}>
          <div style={{ fontSize: 18, fontWeight: 700 }}>{room.roomName}</div>
          <div style={{ opacity: 0.8, fontSize: 13 }}>
            Hosted by {room.hostEntity} · Expires {room.expiryDate}
          </div>
        </div>
      </header>

      <div className="guest-room__tabs">
        <button className={`tab${tab === 'docs' ? ' tab--active' : ''}`} onClick={() => setTab('docs')}>
          Documents
        </button>
        {room.qaEnabled && (
          <button className={`tab${tab === 'qa' ? ' tab--active' : ''}`} onClick={() => setTab('qa')}>
            Q&amp;A
          </button>
        )}
      </div>

      {tab === 'docs' ? (
        <div className="guest-room__body">
          <aside className="guest-room__folders">
            {room.folders.map((f) => (
              <div key={f.id} style={{ marginBottom: 12 }}>
                <div style={{ fontWeight: 600, fontSize: 13 }}>
                  {f.name} <span className="muted">({f.documents.length})</span>
                </div>
                {f.documents.map((d) => (
                  <button key={d.documentId} className="guest-doc-link" onClick={() => openDocument(d)}>
                    {d.fileName}
                  </button>
                ))}
              </div>
            ))}
          </aside>
          <section className="guest-room__viewer">
            {openDoc ? (
              <div className="viewer-placeholder" style={{ minHeight: 320 }}>
                <div style={{ fontSize: 40 }}>📄</div>
                <div style={{ fontWeight: 600 }}>{openDoc.fileName}</div>
                <p className="muted">
                  Watermarked · {room.downloadPolicy} · no download unless permitted · screenshots blocked
                </p>
                {room.watermark && <div className="watermark">{room.guestName || room.guestEmail}</div>}
              </div>
            ) : (
              <p className="muted">
                Select a document to open the secure viewer. Your name and email appear on every page you view.
              </p>
            )}
          </section>
        </div>
      ) : (
        <GuestQa token={token ?? ''} />
      )}
    </div>
  );
}

function GuestQa({ token }: { token: string }) {
  const [questions, setQuestions] = useState<GuestQuestion[]>([]);
  const [category, setCategory] = useState('General');
  const [text, setText] = useState('');

  const load = useCallback(() => {
    getGuestQuestions(token).then(setQuestions).catch(() => undefined);
  }, [token]);

  useEffect(() => {
    load();
  }, [load]);

  async function submit() {
    if (!text) return;
    await submitGuestQuestion(token, category, text);
    setText('');
    load();
  }

  return (
    <div className="page" style={{ maxWidth: 820 }}>
      <div className="card" style={{ padding: 16, marginBottom: 16 }}>
        <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end', flexWrap: 'wrap' }}>
          <div className="field">
            <label>Category</label>
            <select value={category} onChange={(e) => setCategory(e.target.value)}>
              {CATEGORIES.map((c) => (
                <option key={c}>{c}</option>
              ))}
            </select>
          </div>
          <div className="field" style={{ flex: 1, minWidth: 160 }}>
            <label>Question</label>
            <input value={text} onChange={(e) => setText(e.target.value)} />
          </div>
          <button className="btn btn--inline" onClick={submit} disabled={!text}>
            Submit
          </button>
        </div>
      </div>

      {questions.length === 0 && <p className="muted">No questions yet.</p>}
      {questions.map((q) => (
        <div key={q.id} className="card" style={{ padding: 16, marginBottom: 8 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
            <strong>{q.questionText}</strong>
            <span className="badge badge--neutral">{q.status}</span>
          </div>
          {q.answerText && <p style={{ marginTop: 6 }}>{q.answerText}</p>}
        </div>
      ))}
    </div>
  );
}
