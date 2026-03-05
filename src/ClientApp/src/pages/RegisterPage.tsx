import { useState, type FormEvent } from 'react';
import { useNavigate, Link as RouterLink } from 'react-router-dom';
import Box from '@mui/material/Box';
import Typography from '@mui/material/Typography';
import Paper from '@mui/material/Paper';
import TextField from '@mui/material/TextField';
import Button from '@mui/material/Button';
import Container from '@mui/material/Container';
import Alert from '@mui/material/Alert';
import MenuItem from '@mui/material/MenuItem';
import CircularProgress from '@mui/material/CircularProgress';
import Link from '@mui/material/Link';
import useAuthStore from '@/stores/authStore';
import { register, DEMO_TENANTS } from '@/services/authService';
import { parseJwt } from '@/utils/jwt';

const AVAILABLE_ROLES = ['IntakeWorker', 'Reviewer', 'Admin'];

function RegisterPage() {
  const navigate = useNavigate();
  const { setAuth } = useAuthStore();
  const [tenantId, setTenantId] = useState(DEMO_TENANTS[0].id);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [role, setRole] = useState('IntakeWorker');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError('');

    if (password !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    if (password.length < 6) {
      setError('Password must be at least 6 characters.');
      return;
    }

    setLoading(true);

    try {
      const res = await register({ tenantId, email, password, roles: [role] });
      if (!res.success) {
        setError(res.error?.message ?? 'Registration failed');
        return;
      }
      const claims = parseJwt(res.data.accessToken);
      setAuth(
        {
          id: res.data.userId,
          email: (claims.email as string) ?? email,
          roles: Array.isArray(claims.role) ? (claims.role as string[]) : [claims.role as string],
          tenantId,
        },
        res.data.accessToken,
        res.data.refreshToken
      );
      navigate('/dashboard', { replace: true });
    } catch {
      setError('Unable to connect to the server.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Container maxWidth="sm">
      <Box
        sx={{
          minHeight: '80vh',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
        }}
      >
        <Paper sx={{ p: 4, width: '100%' }} elevation={2}>
          <Typography variant="h5" component="h1" gutterBottom fontWeight={700} align="center">
            Create Account
          </Typography>
          <Typography variant="body2" color="text.secondary" align="center" sx={{ mb: 3 }}>
            Intake Document Processor
          </Typography>

          {error && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <Box component="form" noValidate onSubmit={handleSubmit}>
            <TextField
              select
              label="Tenant"
              value={tenantId}
              onChange={e => setTenantId(e.target.value)}
              fullWidth
              margin="normal"
            >
              {DEMO_TENANTS.map(t => (
                <MenuItem key={t.id} value={t.id}>
                  {t.name}
                </MenuItem>
              ))}
            </TextField>
            <TextField
              label="Email"
              type="email"
              fullWidth
              margin="normal"
              autoComplete="email"
              autoFocus
              value={email}
              onChange={e => setEmail(e.target.value)}
            />
            <TextField
              label="Password"
              type="password"
              fullWidth
              margin="normal"
              autoComplete="new-password"
              value={password}
              onChange={e => setPassword(e.target.value)}
            />
            <TextField
              label="Confirm Password"
              type="password"
              fullWidth
              margin="normal"
              autoComplete="new-password"
              value={confirmPassword}
              onChange={e => setConfirmPassword(e.target.value)}
            />
            <TextField
              select
              label="Role"
              value={role}
              onChange={e => setRole(e.target.value)}
              fullWidth
              margin="normal"
            >
              {AVAILABLE_ROLES.map(r => (
                <MenuItem key={r} value={r}>
                  {r}
                </MenuItem>
              ))}
            </TextField>
            <Button
              type="submit"
              variant="contained"
              fullWidth
              sx={{ mt: 3, mb: 2 }}
              size="large"
              disabled={loading || !email || !password || !confirmPassword}
            >
              {loading ? <CircularProgress size={24} color="inherit" /> : 'Register'}
            </Button>
          </Box>
          <Typography variant="body2" align="center">
            Already have an account?{' '}
            <Link component={RouterLink} to="/login">
              Sign In
            </Link>
          </Typography>
        </Paper>
      </Box>
    </Container>
  );
}

export default RegisterPage;
