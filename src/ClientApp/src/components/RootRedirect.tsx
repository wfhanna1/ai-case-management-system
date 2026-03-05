import { Navigate } from 'react-router-dom';
import useAuthStore from '@/stores/authStore';

function RootRedirect() {
  const isAuthenticated = useAuthStore(state => state.isAuthenticated);
  return <Navigate to={isAuthenticated ? '/dashboard' : '/login'} replace />;
}

export default RootRedirect;
