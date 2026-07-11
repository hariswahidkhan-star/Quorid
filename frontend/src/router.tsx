import { createBrowserRouter, Navigate } from 'react-router-dom';
import { AppLayout } from './components/AppLayout';
import { ProtectedRoute } from './auth/ProtectedRoute';
import { LoginPage } from './pages/LoginPage';
import { LaunchpadPage } from './pages/LaunchpadPage';
import { DocumentsPage } from './pages/DocumentsPage';
import { DocumentDetailPage } from './pages/DocumentDetailPage';
import { CaptureReviewPage } from './pages/CaptureReviewPage';
import { VaultPage } from './pages/VaultPage';
import { CompliancePage } from './pages/CompliancePage';
import { SharesPage } from './pages/SharesPage';
import { PublicSharePage } from './pages/PublicSharePage';
import { RoomsPage } from './pages/RoomsPage';
import { RoomBuilderPage } from './pages/RoomBuilderPage';
import { RoomDetailPage } from './pages/RoomDetailPage';
import { GuestRoomPage } from './pages/GuestRoomPage';
import { EngagementsPage } from './pages/EngagementsPage';
import { EngagementDetailPage } from './pages/EngagementDetailPage';
import { PortalPage } from './pages/PortalPage';
import { ProposalsPage } from './pages/ProposalsPage';
import { ProposalDetailPage } from './pages/ProposalDetailPage';
import { AnalyticsPage } from './pages/AnalyticsPage';
import { StudioPage } from './pages/StudioPage';
import { DraftEditorPage } from './pages/DraftEditorPage';
import { AdminPage } from './pages/AdminPage';

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  { path: '/share/:token', element: <PublicSharePage /> },
  { path: '/guest/:token', element: <GuestRoomPage /> },
  { path: '/portal/:token', element: <PortalPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppLayout />,
        children: [
          { path: '/', element: <LaunchpadPage /> },
          { path: '/documents', element: <DocumentsPage /> },
          { path: '/documents/:id', element: <DocumentDetailPage /> },
          { path: '/documents/:id/review', element: <CaptureReviewPage /> },
          { path: '/vault', element: <VaultPage /> },
          { path: '/compliance', element: <CompliancePage /> },
          { path: '/shares', element: <SharesPage /> },
          { path: '/rooms', element: <RoomsPage /> },
          { path: '/rooms/new', element: <RoomBuilderPage /> },
          { path: '/rooms/:id', element: <RoomDetailPage /> },
          {
            path: '/projects',
            element: (
              <EngagementsPage
                type="Project"
                title="Projects"
                subtitle="Project-based document collection for bids and engagements."
              />
            ),
          },
          {
            path: '/vendors',
            element: (
              <EngagementsPage
                type="Vendor"
                title="Vendors"
                subtitle="Track vendor compliance and collect documents."
              />
            ),
          },
          {
            path: '/clients',
            element: (
              <EngagementsPage
                type="Client"
                title="Clients"
                subtitle="Client engagement and document collection."
              />
            ),
          },
          { path: '/engagements/:id', element: <EngagementDetailPage /> },
          { path: '/proposals', element: <ProposalsPage /> },
          { path: '/proposals/:id', element: <ProposalDetailPage /> },
          { path: '/analytics', element: <AnalyticsPage /> },
          { path: '/studio', element: <StudioPage /> },
          { path: '/studio/drafts/:id', element: <DraftEditorPage /> },
          { path: '/admin', element: <AdminPage /> },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
]);
