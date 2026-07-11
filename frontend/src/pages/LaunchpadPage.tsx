import { useNavigate } from 'react-router-dom';
import { useAuth } from '../auth/AuthContext';

interface Tile {
  icon: string;
  title: string;
  desc: string;
  to: string;
}

const tiles: Tile[] = [
  { icon: '📄', title: 'Documents', desc: 'Capture, classify, and manage the vault', to: '/documents' },
  { icon: '✍️', title: 'Document Studio', desc: 'Draft documents with AI', to: '/studio' },
  { icon: '🔐', title: 'Data Rooms', desc: 'Secure external sharing', to: '/rooms' },
  { icon: '🔗', title: 'Sharing', desc: 'External shares & analytics', to: '/shares' },
  { icon: '🛡️', title: 'Identity Vault', desc: 'Verified business profile', to: '/vault' },
  { icon: '✅', title: 'Compliance', desc: 'Frameworks & readiness', to: '/compliance' },
  { icon: '📁', title: 'Projects', desc: 'Bids & engagements', to: '/projects' },
  { icon: '🏭', title: 'Vendors', desc: 'Vendor compliance', to: '/vendors' },
  { icon: '🤝', title: 'Clients', desc: 'Client collection', to: '/clients' },
  { icon: '💼', title: 'Proposals', desc: 'Pipeline & builder', to: '/proposals' },
  { icon: '👥', title: 'Users & Roles', desc: 'Administration', to: '/admin' },
  { icon: '📊', title: 'Analytics', desc: 'Reports & Ask Quorid', to: '/analytics' },
];

export function LaunchpadPage() {
  const navigate = useNavigate();
  const { user } = useAuth();
  const firstName = user?.fullName.split(' ')[0];

  return (
    <main className="page">
      <h1 className="page__title">Welcome{firstName ? `, ${firstName}` : ''}</h1>
      <p className="page__subtitle">Your verified operating system for business trust.</p>

      <div className="tile-grid">
        {tiles.map((t) => (
          <div key={t.title} className="tile" onClick={() => navigate(t.to)}>
            <div className="tile__icon">{t.icon}</div>
            <div className="tile__title">{t.title}</div>
            <div className="tile__desc">{t.desc}</div>
          </div>
        ))}
      </div>
    </main>
  );
}
