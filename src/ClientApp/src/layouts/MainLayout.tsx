import { useState } from 'react';
import { Outlet, useNavigate } from 'react-router-dom';
import AppBar from '@mui/material/AppBar';
import Box from '@mui/material/Box';
import Toolbar from '@mui/material/Toolbar';
import Typography from '@mui/material/Typography';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Chip from '@mui/material/Chip';
import IconButton from '@mui/material/IconButton';
import Drawer from '@mui/material/Drawer';
import List from '@mui/material/List';
import ListItem from '@mui/material/ListItem';
import ListItemButton from '@mui/material/ListItemButton';
import ListItemIcon from '@mui/material/ListItemIcon';
import ListItemText from '@mui/material/ListItemText';
import Divider from '@mui/material/Divider';
import MenuIcon from '@mui/icons-material/Menu';
import DescriptionIcon from '@mui/icons-material/Description';
import CloudUploadIcon from '@mui/icons-material/CloudUpload';
import FolderIcon from '@mui/icons-material/Folder';
import RateReviewIcon from '@mui/icons-material/RateReview';
import SearchIcon from '@mui/icons-material/Search';
import WorkIcon from '@mui/icons-material/Work';
import DashboardIcon from '@mui/icons-material/Dashboard';
import LogoutIcon from '@mui/icons-material/Logout';
import useAuthStore from '@/stores/authStore';
import queryClient from '@/queryClient';

interface NavItem {
  label: string;
  path: string;
  icon: React.ReactNode;
  testId: string;
  reviewerOnly?: boolean;
}

const NAV_ITEMS: NavItem[] = [
  { label: 'Dashboard', path: '/dashboard', icon: <DashboardIcon />, testId: 'mobile-nav-dashboard' },
  { label: 'Upload', path: '/upload', icon: <CloudUploadIcon />, testId: 'mobile-nav-upload' },
  { label: 'Documents', path: '/documents', icon: <FolderIcon />, testId: 'mobile-nav-documents' },
  { label: 'Templates', path: '/templates', icon: <DescriptionIcon />, testId: 'mobile-nav-templates' },
  { label: 'Search', path: '/search', icon: <SearchIcon />, testId: 'mobile-nav-search' },
  { label: 'Cases', path: '/cases', icon: <WorkIcon />, testId: 'mobile-nav-cases' },
  { label: 'Reviews', path: '/reviews', icon: <RateReviewIcon />, testId: 'mobile-nav-reviews', reviewerOnly: true },
];

function MainLayout() {
  const navigate = useNavigate();
  const { isAuthenticated, user, clearAuth } = useAuthStore();
  const [drawerOpen, setDrawerOpen] = useState(false);

  const handleSignOut = () => {
    queryClient.clear();
    clearAuth();
    navigate('/login');
  };

  const handleNavClick = (path: string) => {
    navigate(path);
    setDrawerOpen(false);
  };

  const visibleNavItems = NAV_ITEMS.filter(
    item => !item.reviewerOnly || user?.roles.includes('Reviewer') || user?.roles.includes('Admin')
  );

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
              {/* Mobile hamburger menu */}
              <IconButton
                color="inherit"
                onClick={() => setDrawerOpen(true)}
                sx={{ display: { xs: 'flex', md: 'none' } }}
                data-testid="mobile-menu-btn"
              >
                <MenuIcon />
              </IconButton>

              {/* Desktop inline nav */}
              <Box sx={{ display: { xs: 'none', md: 'flex' }, alignItems: 'center' }}>
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
                <Button color="inherit" onClick={() => navigate('/search')} sx={{ mr: 1 }} startIcon={<SearchIcon />}>
                  Search
                </Button>
                <Button color="inherit" onClick={() => navigate('/cases')} sx={{ mr: 1 }} startIcon={<WorkIcon />}>
                  Cases
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
              </Box>
            </>
          ) : (
            <Button color="inherit" onClick={() => navigate('/login')}>
              Sign In
            </Button>
          )}
        </Toolbar>
      </AppBar>

      {/* Mobile drawer */}
      <Drawer
        anchor="left"
        open={drawerOpen}
        onClose={() => setDrawerOpen(false)}
        data-testid="mobile-drawer"
        PaperProps={{ sx: { width: 260 } }}
      >
        <Box sx={{ p: 2 }}>
          <Typography variant="h6" fontWeight={700}>
            Menu
          </Typography>
          {user && (
            <Typography variant="body2" color="text.secondary">
              {user.email}
            </Typography>
          )}
        </Box>
        <Divider />
        <List>
          {visibleNavItems.map(item => (
            <ListItem key={item.path} disablePadding>
              <ListItemButton
                onClick={() => handleNavClick(item.path)}
                data-testid={item.testId}
              >
                <ListItemIcon>{item.icon}</ListItemIcon>
                <ListItemText primary={item.label} />
              </ListItemButton>
            </ListItem>
          ))}
        </List>
        <Divider />
        <List>
          <ListItem disablePadding>
            <ListItemButton onClick={handleSignOut}>
              <ListItemIcon><LogoutIcon /></ListItemIcon>
              <ListItemText primary="Sign Out" />
            </ListItemButton>
          </ListItem>
        </List>
      </Drawer>

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
