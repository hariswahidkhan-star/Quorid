import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

export function ShellBar() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const initials =
    user?.fullName
      ?.split(' ')
      .map((p) => p[0])
      .slice(0, 2)
      .join('')
      .toUpperCase() ?? 'Q';

  return (
    <header className="shell-bar">
      <div
        className="shell-bar__logo"
        style={{ cursor: 'pointer' }}
        onClick={() => navigate('/')}
      >
        QUOR<span>ID</span>
      </div>
      <div className="shell-bar__spacer" />
      <button className="shell-bar__button" onClick={logout}>
        Sign out
      </button>
      <div className="shell-bar__user" title={user?.email}>
        {initials}
      </div>
    </header>
  );
}
