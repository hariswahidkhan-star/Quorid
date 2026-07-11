import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { listRooms, type RoomListItem } from '../api/client';

function statusClass(status: string): string {
  const s = status.toLowerCase();
  if (s === 'active') return 'badge badge--positive';
  if (s === 'expired' || s === 'closed') return 'badge badge--critical';
  return 'badge badge--neutral';
}

export function RoomsPage() {
  const navigate = useNavigate();
  const [rooms, setRooms] = useState<RoomListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(() => {
    setLoading(true);
    listRooms()
      .then(setRooms)
      .catch((e) => setError(e instanceof Error ? e.message : 'Failed to load rooms'))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <main className="page">
      <div className="toolbar">
        <div>
          <h1 className="page__title">Data Rooms</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            Secure, time-limited document sharing for external stakeholders.
          </p>
        </div>
        <button className="btn btn--inline" onClick={() => navigate('/rooms/new')}>
          + Create Room
        </button>
      </div>

      {loading && <p className="muted">Loading…</p>}
      {error && <div className="error-text">{error}</div>}

      {!loading && rooms.length === 0 && (
        <div className="card" style={{ padding: 32, textAlign: 'center' }}>
          <p className="muted">
            No rooms yet. Click <strong>Create Room</strong> to launch the builder.
          </p>
        </div>
      )}

      {rooms.length > 0 && (
        <div className="card">
          <table className="table">
            <thead>
              <tr>
                <th>Room</th>
                <th>Type</th>
                <th>Status</th>
                <th>Docs</th>
                <th>Guests</th>
                <th>Expiry</th>
              </tr>
            </thead>
            <tbody>
              {rooms.map((r) => (
                <tr key={r.id} className="clickable" onClick={() => navigate(`/rooms/${r.id}`)}>
                  <td>{r.name}</td>
                  <td>{r.roomTypeCode}</td>
                  <td>
                    <span className={statusClass(r.status)}>{r.status}</span>
                  </td>
                  <td>{r.documentCount}</td>
                  <td>{r.guestCount}</td>
                  <td>{r.expiryDate}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </main>
  );
}
