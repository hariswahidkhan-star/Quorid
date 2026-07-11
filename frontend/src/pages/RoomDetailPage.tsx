import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  answerQuestion,
  deleteRoom,
  getRoom,
  getRoomAnalytics,
  inviteGuests,
  listRoomQuestions,
  revokeGuest,
  updateRoom,
  type RoomAnalytics,
  type RoomDetail,
  type RoomQuestion,
} from '../api/client';

type RTab = 'documents' | 'guests' | 'qa' | 'analytics' | 'settings';

const PERMS = ['None', 'FenceView', 'View', 'EncryptedPdf', 'Print', 'Pdf', 'Original', 'Upload'];
const ROLES = ['Reviewer', 'Viewer', 'Contributor'];

export function RoomDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const [room, setRoom] = useState<RoomDetail | null>(null);
  const [tab, setTab] = useState<RTab>('documents');
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    if (!id) return;
    getRoom(id)
      .then(setRoom)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load room'))
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  if (loading) {
    return (
      <main className="page">
        <p className="muted">Loading…</p>
      </main>
    );
  }

  if (!room) {
    return (
      <main className="page">
        <div className="error-text">{error ?? 'Room not found.'}</div>
      </main>
    );
  }

  return (
    <main className="page">
      <div className="toolbar">
        <div>
          <h1 className="page__title">{room.name}</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            {room.roomTypeCode} · <span className="badge badge--positive">{room.status}</span> · expires{' '}
            {room.expiryDate}
          </p>
        </div>
        <button className="btn btn--ghost btn--inline" onClick={() => navigate('/rooms')}>
          ← All rooms
        </button>
      </div>

      <div className="tabs">
        {(['documents', 'guests', 'qa', 'analytics', 'settings'] as RTab[]).map((t) => (
          <button key={t} className={`tab${tab === t ? ' tab--active' : ''}`} onClick={() => setTab(t)}>
            {t === 'documents' && `Documents (${room.documents.length})`}
            {t === 'guests' && `Guests (${room.guests.length})`}
            {t === 'qa' && 'Q&A'}
            {t === 'analytics' && 'Analytics'}
            {t === 'settings' && 'Settings'}
          </button>
        ))}
      </div>

      {tab === 'documents' && <DocumentsTab room={room} />}
      {tab === 'guests' && <GuestsTab room={room} onChanged={load} />}
      {tab === 'qa' && <QaTab roomId={room.id} />}
      {tab === 'analytics' && <AnalyticsTab roomId={room.id} />}
      {tab === 'settings' && <SettingsTab room={room} onChanged={load} />}
    </main>
  );
}

function DocumentsTab({ room }: { room: RoomDetail }) {
  return (
    <div className="card" style={{ padding: 20 }}>
      {room.documents.length === 0 && <p className="muted">No documents in this room.</p>}
      {room.folders.map((f) => {
        const docs = room.documents.filter((d) => d.folderId === f.id);
        if (docs.length === 0) return null;
        return (
          <div key={f.id} style={{ marginBottom: 16 }}>
            <div style={{ fontWeight: 600, marginBottom: 6 }}>
              {f.displayOrder}. {f.name} <span className="muted">({docs.length})</span>
            </div>
            {docs.map((d) => (
              <div key={d.id} className="activity-row">
                <span>{d.title ?? d.fileName}</span>
                <span className="badge badge--neutral">{d.permissionLevel}</span>
              </div>
            ))}
          </div>
        );
      })}
    </div>
  );
}

function GuestsTab({ room, onChanged }: { room: RoomDetail; onChanged: () => void }) {
  const [email, setEmail] = useState('');
  const [name, setName] = useState('');
  const [role, setRole] = useState('Reviewer');
  const [perm, setPerm] = useState('View');
  const [busy, setBusy] = useState(false);

  function copyLink(token: string) {
    navigator.clipboard?.writeText(`${window.location.origin}/guest/${token}`);
  }

  async function invite() {
    if (!email) return;
    setBusy(true);
    try {
      await inviteGuests(room.id, [{ email, name: name || undefined, role, permissionLevel: perm }]);
      setEmail('');
      setName('');
      onChanged();
    } finally {
      setBusy(false);
    }
  }

  async function revoke(guestId: string) {
    await revokeGuest(room.id, guestId);
    onChanged();
  }

  return (
    <div className="card" style={{ padding: 20 }}>
      <div style={{ display: 'flex', gap: 8, alignItems: 'flex-end', flexWrap: 'wrap', marginBottom: 16 }}>
        <div className="field" style={{ flex: 1, minWidth: 140 }}>
          <label>Invite email</label>
          <input value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>
        <div className="field" style={{ flex: 1, minWidth: 120 }}>
          <label>Name</label>
          <input value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="field">
          <label>Role</label>
          <select value={role} onChange={(e) => setRole(e.target.value)}>
            {ROLES.map((r) => (
              <option key={r}>{r}</option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Permission</label>
          <select value={perm} onChange={(e) => setPerm(e.target.value)}>
            {PERMS.map((p) => (
              <option key={p}>{p}</option>
            ))}
          </select>
        </div>
        <button className="btn btn--inline" onClick={invite} disabled={busy || !email}>
          Invite
        </button>
      </div>

      {room.guests.length === 0 && <p className="muted">No guests invited.</p>}
      {room.guests.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th>Email</th>
              <th>Role</th>
              <th>Permission</th>
              <th>Status</th>
              <th>NDA</th>
              <th>Views</th>
              <th></th>
            </tr>
          </thead>
          <tbody>
            {room.guests.map((g) => (
              <tr key={g.id}>
                <td>{g.email}</td>
                <td>{g.role}</td>
                <td>{g.permissionLevel}</td>
                <td>
                  <span className="badge badge--neutral">{g.status}</span>
                </td>
                <td>{g.ndaSigned ? '✓' : '—'}</td>
                <td>{g.views}</td>
                <td style={{ display: 'flex', gap: 6 }}>
                  <button className="btn btn--ghost btn--inline" onClick={() => copyLink(g.accessToken)}>
                    Copy link
                  </button>
                  {g.status !== 'Revoked' && (
                    <button className="btn btn--ghost btn--inline" onClick={() => revoke(g.id)}>
                      Revoke
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

function QaTab({ roomId }: { roomId: string }) {
  const [questions, setQuestions] = useState<RoomQuestion[]>([]);
  const [answers, setAnswers] = useState<Record<string, string>>({});
  const [pub, setPub] = useState<Record<string, boolean>>({});

  const load = useCallback(() => {
    listRoomQuestions(roomId).then(setQuestions).catch(() => undefined);
  }, [roomId]);

  useEffect(() => {
    load();
  }, [load]);

  async function answer(qid: string) {
    const text = answers[qid];
    if (!text) return;
    await answerQuestion(roomId, qid, text, pub[qid] ?? false);
    load();
  }

  return (
    <div className="card" style={{ padding: 20 }}>
      {questions.length === 0 && <p className="muted">No questions yet.</p>}
      {questions.map((q) => (
        <div key={q.id} style={{ borderBottom: '1px solid var(--border)', padding: '12px 0' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 12 }}>
            <strong>{q.questionText}</strong>
            <span className="badge badge--neutral">{q.category}</span>
          </div>
          <div className="muted" style={{ fontSize: 13 }}>
            {q.guestEmail} · {q.status}
          </div>
          {q.answerText ? (
            <p style={{ marginTop: 6 }}>{q.answerText}</p>
          ) : (
            <div style={{ marginTop: 8 }}>
              <input
                placeholder="Draft answer…"
                value={answers[q.id] ?? ''}
                onChange={(e) => setAnswers({ ...answers, [q.id]: e.target.value })}
                style={{ width: '100%', padding: '8px 10px', border: '1px solid var(--border)', borderRadius: 6 }}
              />
              <div style={{ display: 'flex', gap: 12, alignItems: 'center', marginTop: 6 }}>
                <label className="check">
                  <input
                    type="checkbox"
                    checked={pub[q.id] ?? false}
                    onChange={(e) => setPub({ ...pub, [q.id]: e.target.checked })}
                  />{' '}
                  Publish to all guests
                </label>
                <button className="btn btn--inline" onClick={() => answer(q.id)}>
                  Publish answer
                </button>
              </div>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

function AnalyticsTab({ roomId }: { roomId: string }) {
  const [data, setData] = useState<RoomAnalytics | null>(null);

  useEffect(() => {
    getRoomAnalytics(roomId).then(setData).catch(() => undefined);
  }, [roomId]);

  if (!data) {
    return (
      <div className="card" style={{ padding: 20 }}>
        <p className="muted">Loading…</p>
      </div>
    );
  }

  const engagementClass = (e: string) =>
    e === 'Hot' ? 'badge badge--positive' : e === 'Warm' ? 'badge badge--critical' : 'badge badge--neutral';

  return (
    <div className="card" style={{ padding: 20 }}>
      <p className="muted" style={{ marginBottom: 12 }}>
        {data.guestCount} guests · {data.totalViews} total views
      </p>
      <table className="table">
        <thead>
          <tr>
            <th>Guest</th>
            <th>Views</th>
            <th>Last access</th>
            <th>Engagement</th>
          </tr>
        </thead>
        <tbody>
          {data.guests.map((g) => (
            <tr key={g.guestId}>
              <td>{g.email}</td>
              <td>{g.views}</td>
              <td>{g.lastAccessAt ? new Date(g.lastAccessAt).toLocaleDateString() : '—'}</td>
              <td>
                <span className={engagementClass(g.engagement)}>{g.engagement}</span>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function SettingsTab({ room, onChanged }: { room: RoomDetail; onChanged: () => void }) {
  const navigate = useNavigate();

  async function setStatus(status: string) {
    await updateRoom(room.id, { status });
    onChanged();
  }

  async function del() {
    if (!confirm('Delete this room? This cannot be undone.')) return;
    try {
      await deleteRoom(room.id);
      navigate('/rooms');
    } catch (e) {
      alert(e instanceof Error ? e.message : 'Delete failed');
    }
  }

  return (
    <div className="card" style={{ padding: 20, display: 'flex', flexDirection: 'column', gap: 12, alignItems: 'flex-start' }}>
      <div>
        <strong>Status:</strong> {room.status}
      </div>
      <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
        {room.status === 'Active' && (
          <button className="btn btn--ghost btn--inline" onClick={() => setStatus('Closed')}>
            Close room
          </button>
        )}
        {room.status !== 'Archived' && (
          <button className="btn btn--ghost btn--inline" onClick={() => setStatus('Archived')}>
            Archive
          </button>
        )}
        <button className="btn btn--ghost btn--inline" onClick={del}>
          Delete
        </button>
      </div>
      <p className="muted">Active rooms must be closed before deletion.</p>
    </div>
  );
}
