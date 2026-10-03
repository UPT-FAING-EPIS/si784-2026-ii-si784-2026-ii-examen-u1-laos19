import { Navigate, Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout.jsx';
import { useApp } from './components/AppContext.jsx';
import CatalogPage from './pages/CatalogPage.jsx';
import AuthPage from './pages/AuthPage.jsx';
import SubscriptionPage from './pages/SubscriptionPage.jsx';
import PlaylistsPage from './pages/PlaylistsPage.jsx';
import ForYouPage from './pages/ForYouPage.jsx';
import AdminPage from './pages/AdminPage.jsx';

function RequireAuth({ children, admin = false }) {
  const { user, isAdmin } = useApp();
  if (!user) return <Navigate to="/login" replace />;
  if (admin && !isAdmin) return <Navigate to="/" replace />;
  return children;
}

export default function App() {
  return (
    <Layout>
      <Routes>
        <Route path="/" element={<CatalogPage />} />
        <Route path="/login" element={<AuthPage />} />
        <Route path="/para-ti" element={<RequireAuth><ForYouPage /></RequireAuth>} />
        <Route path="/playlists" element={<RequireAuth><PlaylistsPage /></RequireAuth>} />
        <Route path="/suscripcion" element={<RequireAuth><SubscriptionPage /></RequireAuth>} />
        <Route path="/admin" element={<RequireAuth admin><AdminPage /></RequireAuth>} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Layout>
  );
}
