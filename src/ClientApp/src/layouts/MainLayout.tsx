import { Outlet, useNavigate } from 'react-router-dom';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Chip from '@mui/material/Chip';
import DescriptionIcon from '@mui/icons-material/Description';
import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import FolderIcon from '@mui/icons-material/Folder';
import RateReviewIcon from '@mui/icons-material/RateReview';
import useAuthStore from '@/stores/authStore';

function MainLayout() {
  const navigate = useNavigate();
  const { isAuthenticated, user, clearAuth } = useAuthStore();

  const handleSignOut = () => {
    clearAuth();
    navigate('/login');
  };

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', minHeight: '100vh' }}>
      <AppBar position="static" color="primary">
        <Toolbar>
          <DescriptionIcon sx={{ mr: 1 }} />
          <Typography variant="h6" component="div" sx={{ flexGrow: 1, fontWeight: 700 }}>
            Intake Document Processor
          </Typography>
          {isAuthenticated && user ? (
            <>
              <Button color="inherit" onClick={() => navigate('/dashboard')} sx={{ mr: 1 }}>
                Dashboard
              </Button>
              <Button color="inherit" onClick={() => navigate('/upload')} sx={{ mr: 1 }} startIcon={<CloudUploadIcon />}>
                Upload
              </Button>
              <Button color="inherit" onClick={() => navigate('/documents')} sx={{ mr: 1 }} startIcon={<FolderIcon />}>
                Documents
              </Button>
              <Button color="inherit" onClick={() => navigate('/templates')} sx={{ mr: 1 }}>
                Templates
              </Button>
              {(user.roles.includes('Reviewer') || user.roles.includes('Admin')) && (
                <Button color="inherit" onClick={() => navigate('/reviews')} sx={{ mr: 2 }} startIcon={<RateReviewIcon />}>
                  Reviews
                </Button>
              )}
              <Typography variant="body2" sx={{ mr: 1 }}>
                {user.email}
              </Typography>
              {user.roles.map(role => (
                <Chip
                  key={role}
                  label={role}
                  size="small"
                  color="secondary"
                  sx={{ mr: 1 }}
                />
              ))}
              <Button color="inherit" onClick={handleSignOut}>
                Sign Out
              </Button>
            </>
          ) : (
            <Button color="inherit" onClick={() => navigate('/login')}>
              Sign In
            </Button>
          )}
        </Toolbar>
      </AppBar>
      <Box component="main" sx={{ flexGrow: 1, py: 3, backgroundColor: 'background.default' }}>
        <Container maxWidth="xl">
          <Outlet />
        </Container>
      </Box>
      <Box
        component="footer"
        sx={{
          py: 2,
          px: 3,
          backgroundColor: 'background.paper',
          borderTop: 1,
          borderColor: 'divider',
        }}
      >
        <Typography variant="body2" color="text.secondary" align="center">
          Intake Document Processor - AI Case Management System
        </Typography>
      </Box>
    </Box>
  );
}

export default MainLayout;
